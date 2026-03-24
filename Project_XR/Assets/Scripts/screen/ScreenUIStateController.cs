using System.Collections;
using UnityEngine;

public enum ScreenUIState
{
    Start,
    Dimensions,
    EditGrid,
    ShowPath,
    RobotFeedback
}

public class ScreenUIStateController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject panelStart;
    public GameObject panelDimensions;
    public GameObject panelEditGrid;
    public GameObject panelShowPath;
    public GameObject panelRobotFeedback;

    [Header("Grid Area Root (container)")]
    public GameObject gridArea;

    [Header("GridArea Layout By State")]
    public RectTransform gridAreaRect;

    // Valores para estados normales (EditGrid / ShowPath)
    public Vector3 gridScaleNormal = Vector3.one;
    public Vector2 gridAnchoredPosNormal;

    // Valores SOLO para RobotFeedback
    public Vector3 gridScaleRobotFeedback = new Vector3(0.6f, 0.6f, 0.6f);
    public Vector2 gridAnchoredPosRobotFeedback;

    [Header("Color Palette Panels")]
    public GameObject colorPaletteAB;   // el grande (A/B)
    public GameObject colorPaletteC;    // el pequeño (C)

    [Header("Mode C UI (optional)")]
    public PathPlanner pathPlanner;
    public IdMappingUIManager idMappingUIManager;

    [Header("Path Overlays")]
    public PathOverlayManager pathOverlayManager;

    private ScreenUIState _state;

    private void Awake()
    {
        // Apaga todo al inicio (incluye Start, luego lo encendemos con SetState)
        if (panelStart != null) panelStart.SetActive(false);
        if (panelDimensions != null) panelDimensions.SetActive(false);
        if (panelEditGrid != null) panelEditGrid.SetActive(false);
        if (panelShowPath != null) panelShowPath.SetActive(false);
        if (panelRobotFeedback != null) panelRobotFeedback.SetActive(false);

        if (gridArea != null) gridArea.SetActive(false);

        if (colorPaletteAB != null) colorPaletteAB.SetActive(false);
        if (colorPaletteC != null) colorPaletteC.SetActive(false);

        if (idMappingUIManager != null)
            idMappingUIManager.SetVisibleForModeC(false);

        if (gridAreaRect == null && gridArea != null)
            gridAreaRect = gridArea.GetComponent<RectTransform>();

        // Guarda layout "normal" desde inspector/estado inicial
        if (gridAreaRect != null)
        {
            gridScaleNormal = gridAreaRect.localScale;
            gridAnchoredPosNormal = gridAreaRect.anchoredPosition;
        }
    }

    private void Start()
    {
        // ✅ Arranque en pantalla inicial
        SetState(ScreenUIState.Start);
    }

    public void SetState(ScreenUIState newState)
    {
        _state = newState;

        // ========= MENSAJES =========
        UIMessageManager.Clear();

        if (newState == ScreenUIState.Dimensions)
        {
            UIMessageManager.ShowDimensionsHeader();
        }
        else if (newState == ScreenUIState.EditGrid && pathPlanner != null)
        {
            switch (pathPlanner.deliveryMode)
            {
                case DeliveryMode.NearestTarget:   // modo A
                    UIMessageManager.ShowEditGridNearestTarget();
                    break;

                case DeliveryMode.FixedPriority:   // modo B
                    UIMessageManager.ShowEditGridFixedPriority();
                    break;

                case DeliveryMode.IdMapping:       // modo C
                    UIMessageManager.ShowEditGridBoxToTarget();
                    break;
            }
        }
        else if (newState == ScreenUIState.ShowPath)
        {
            UIMessageManager.ShowShowPath();
        }
        // Para Start y RobotFeedback (si quieres texto ahí, lo añadimos luego)

        // ========= OVERLAYS =========
        // ✅ Si vamos a EditGrid, limpiamos overlays anteriores (ShowPath/Feedback)
        if (newState == ScreenUIState.EditGrid && pathOverlayManager != null)
            pathOverlayManager.ClearAllPaths();

        // ========= PANELES =========
        if (newState == ScreenUIState.Dimensions && panelStart != null)
            panelStart.SetActive(false);
        if (panelStart != null) panelStart.SetActive(newState == ScreenUIState.Start);
        if (panelDimensions != null) panelDimensions.SetActive(newState == ScreenUIState.Dimensions);
        if (panelEditGrid != null) panelEditGrid.SetActive(newState == ScreenUIState.EditGrid);
        if (panelShowPath != null) panelShowPath.SetActive(newState == ScreenUIState.ShowPath);
        if (panelRobotFeedback != null) panelRobotFeedback.SetActive(newState == ScreenUIState.RobotFeedback);

        // GridArea solo visible fuera de Start/Dimensions
        if (gridArea != null)
            gridArea.SetActive(newState != ScreenUIState.Start && newState != ScreenUIState.Dimensions);

        ApplyGridAreaLayout(newState);

        // ========= PALETAS =========
        bool isEditGrid = (newState == ScreenUIState.EditGrid);
        bool isModeC = (pathPlanner != null && pathPlanner.deliveryMode == DeliveryMode.IdMapping);

        if (colorPaletteAB != null) colorPaletteAB.SetActive(isEditGrid && !isModeC);
        if (colorPaletteC != null) colorPaletteC.SetActive(isEditGrid && isModeC);

        // ========= UI MODO C / MAPPING =========
        if (idMappingUIManager != null && pathPlanner != null)
        {
            // ✅ Si vamos a ShowPath en modo C, guardamos mapping ANTES de ocultar UI
            if (newState == ScreenUIState.ShowPath &&
                pathPlanner.deliveryMode == DeliveryMode.IdMapping)
            {
                pathPlanner.SetIdMappings(idMappingUIManager.GetMappingCopy());
            }

            bool showModeCUI = isEditGrid && isModeC;
            idMappingUIManager.SetVisibleForModeC(showModeCUI);
        }
        else
        {
            if (idMappingUIManager != null)
                idMappingUIManager.SetVisibleForModeC(false);
        }
    }

    public void GoToEditGridAndGenerate(GridGenerator generator, int rows, int cols)
    {
        SetState(ScreenUIState.EditGrid);
        StartCoroutine(GenerateGridNextFrame(generator, rows, cols));
    }

    private IEnumerator GenerateGridNextFrame(GridGenerator generator, int rows, int cols)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (generator != null)
            generator.GenerateGrid(rows, cols);
    }

    private void ApplyGridAreaLayout(ScreenUIState state)
    {
        if (gridAreaRect == null) return;

        bool isRobotFeedback = (state == ScreenUIState.RobotFeedback);

        gridAreaRect.localScale = isRobotFeedback ? gridScaleRobotFeedback : gridScaleNormal;
        gridAreaRect.anchoredPosition = isRobotFeedback ? gridAnchoredPosRobotFeedback : gridAnchoredPosNormal;
    }

    // Botones UI
    public void GoToStart() => SetState(ScreenUIState.Start);
    public void GoToDimensions() => SetState(ScreenUIState.Dimensions);
    public void GoToEditGrid() => SetState(ScreenUIState.EditGrid);
    public void GoToShowPath() => SetState(ScreenUIState.ShowPath);
    public void GoToRobotFeedback() => SetState(ScreenUIState.RobotFeedback);
}
