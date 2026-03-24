using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class XRKeypad : MonoBehaviour
{
    [Header("References")]
    public PathPlanner pathPlanner;
    public GridGenerator gridGenerator;
    public ScreenUIStateController uiState;

    [Header("Pantalla __ x __")]
    public TMP_Text dimensionLabel;

    [Header("Opcional: texto para avisos")]
    public TMP_Text warningLabel;

    [Header("Botón OK (solo keypad)")]
    public Button okButton;

    [Header("Botón Generate Empty Grid (avanzar)")]
    public Button generateButton;
    public CanvasGroup generateCanvasGroup;
    public float generateDimAlpha = 0.35f;
    public float generateFullAlpha = 1.0f;

    [Header("Delivery Mode Buttons (para resaltar)")]
    public Button btnModeA;
    public Button btnModeB;
    public Button btnModeC;

    [Header("Resaltado modo seleccionado")]
    public Color selectedModeColor = new Color(0.25f, 0.85f, 1f, 1f);
    private Color _aDefault, _bDefault, _cDefault;

    // ✅ Usamos el enum GLOBAL DeliveryMode (el del PathPlanner.cs)
    public DeliveryMode selectedDeliveryMode = DeliveryMode.None;

    private string currentNumber = "";
    private int? rows = null;
    private int? columns = null;

    private enum Mode { Rows, Columns }
    private Mode mode = Mode.Rows;

    private void Awake()
    {
        CacheDefaultModeButtonColors();
        AutoBindGenerateCanvasGroupIfMissing();
        RefreshAllUI();
        HighlightSelectedMode();
        UIMessageManager.ShowDimensionsHeader();
        UIMessageManager.UpdateDimensionsInstructions(rows, columns, selectedDeliveryMode); 

    }

    public void OnNumberPress(string n)
    {
        currentNumber += n;
        ClearWarning();
        RefreshAllUI();
    }

    public void OnDelete()
    {
        if (currentNumber.Length > 0)
            currentNumber = currentNumber.Substring(0, currentNumber.Length - 1);

        RefreshAllUI();
    }

    public void ResetKeypad()
    {
        currentNumber = "";
        rows = null;
        columns = null;
        mode = Mode.Rows;

        selectedDeliveryMode = DeliveryMode.None;

        ClearWarning();
        RefreshAllUI();
        HighlightSelectedMode();

        UIMessageManager.ShowDimensionsHeader();
        UIMessageManager.UpdateDimensionsInstructions(rows, columns, selectedDeliveryMode);

    }

    public void OnOk()
    {
        if (string.IsNullOrEmpty(currentNumber))
            return;

        int value = int.Parse(currentNumber);

        if (mode == Mode.Rows)
        {
            rows = value;
            mode = Mode.Columns;
        }
        else
        {
            columns = value;
        }

        currentNumber = "";
        RefreshAllUI();
    }

    public void OnGenerateEmptyGrid()
    {
    GridCell.ResetTargetOrderCounter();
    GridCell.ResetBoxOrderCounter();

    if (pathPlanner != null)
        pathPlanner.ClearIdMappings();

    if (!IsReadyToGenerate())
    {
        ShowWarning("Introduce dimensiones y elige A/B/C antes de generar el grid.");
        RefreshAllUI();
        return;
    }

    if (pathPlanner != null)
        pathPlanner.deliveryMode = selectedDeliveryMode;

    // ⬇️⬇️⬇️ ÚNICA LLAMADA (NO coroutines aquí)
    if (uiState != null)
        uiState.GoToEditGridAndGenerate(gridGenerator, rows.Value, columns.Value);
    }



    // ✅ Botón Repeat Dimensions
    public void OnRepeatDimensions()
    {
        // 1️⃣ Vaciar grid visual (destruir celdas)
        if (gridGenerator != null && gridGenerator.gridContainer != null)
        {
            for (int i = gridGenerator.gridContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(gridGenerator.gridContainer.GetChild(i).gameObject);
            }
        }

        // 2️⃣ Resetear contadores globales
        GridCell.ResetTargetOrderCounter();
        GridCell.ResetBoxOrderCounter();

        // 3️⃣ Limpiar mappings modo C (por si venías de IdMapping)
        if (pathPlanner != null) pathPlanner.ClearIdMappings();

        // 4️⃣ Resetear estado del keypad
        ResetKeypad();

        // 5️⃣ Volver a la pantalla de dimensiones
        if (uiState != null)
            uiState.GoToDimensions();
    }

    // ---- Botones A/B/C ----
    public void SelectModeA_NearestTarget()
    {
        selectedDeliveryMode = DeliveryMode.NearestTarget;
        ClearWarning();
        HighlightSelectedMode();
        RefreshAllUI();
    }

    public void SelectModeB_FixedPriority()
    {
        selectedDeliveryMode = DeliveryMode.FixedPriority;
        ClearWarning();
        HighlightSelectedMode();
        RefreshAllUI();
    }

    public void SelectModeC_IdMapping()
    {
        selectedDeliveryMode = DeliveryMode.IdMapping;
        ClearWarning();
        HighlightSelectedMode();
        RefreshAllUI();
    }

    // ---- UI ----
    private void RefreshAllUI()
    {
        UpdateLabel();
        UpdateOkInteractable();
        UpdateGenerateInteractableAndVisual();
        UIMessageManager.ShowDimensionsHeader();
        UIMessageManager.UpdateDimensionsInstructions(rows, columns, selectedDeliveryMode);

    }

    private void UpdateLabel()
    {
        string r = rows.HasValue ? rows.Value.ToString()
            : (mode == Mode.Rows ? (string.IsNullOrEmpty(currentNumber) ? "__" : currentNumber) : "__");

        string c = columns.HasValue ? columns.Value.ToString()
            : (mode == Mode.Columns ? (string.IsNullOrEmpty(currentNumber) ? "__" : currentNumber) : "__");

        if (dimensionLabel != null) dimensionLabel.text = $"{r} x {c}";
    }

    private void UpdateOkInteractable()
    {
        if (okButton == null) return;
        okButton.interactable = !string.IsNullOrEmpty(currentNumber);
    }

    private void UpdateGenerateInteractableAndVisual()
    {
        bool ready = IsReadyToGenerate();

        if (generateButton != null) generateButton.interactable = ready;

        float alpha = ready ? generateFullAlpha : generateDimAlpha;

        if (generateCanvasGroup != null)
        {
            generateCanvasGroup.alpha = alpha;
            generateCanvasGroup.interactable = ready;
            generateCanvasGroup.blocksRaycasts = ready;
        }
        else if (generateButton != null && generateButton.image != null)
        {
            var col = generateButton.image.color;
            col.a = alpha;
            generateButton.image.color = col;
        }
    }

    private bool IsReadyToGenerate()
    {
        return rows.HasValue && columns.HasValue && selectedDeliveryMode != DeliveryMode.None;
    }

    private void HighlightSelectedMode()
    {
        SetButtonColor(btnModeA, selectedDeliveryMode == DeliveryMode.NearestTarget ? selectedModeColor : _aDefault);
        SetButtonColor(btnModeB, selectedDeliveryMode == DeliveryMode.FixedPriority ? selectedModeColor : _bDefault);
        SetButtonColor(btnModeC, selectedDeliveryMode == DeliveryMode.IdMapping ? selectedModeColor : _cDefault);
    }

    private void SetButtonColor(Button btn, Color c)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    private void CacheDefaultModeButtonColors()
    {
        if (btnModeA != null && btnModeA.GetComponent<Image>() != null) _aDefault = btnModeA.GetComponent<Image>().color;
        if (btnModeB != null && btnModeB.GetComponent<Image>() != null) _bDefault = btnModeB.GetComponent<Image>().color;
        if (btnModeC != null && btnModeC.GetComponent<Image>() != null) _cDefault = btnModeC.GetComponent<Image>().color;
    }

    private void AutoBindGenerateCanvasGroupIfMissing()
    {
        if (generateCanvasGroup == null && generateButton != null)
            generateCanvasGroup = generateButton.GetComponent<CanvasGroup>();
    }

    private void ShowWarning(string msg)
    {
        if (warningLabel != null) warningLabel.text = msg;
        else Debug.LogWarning(msg);
    }

    private void ClearWarning()
    {
        if (warningLabel != null) warningLabel.text = "";
    }
}
