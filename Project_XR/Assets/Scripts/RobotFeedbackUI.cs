using System.Collections;
using UnityEngine;
using TMPro;
using System.Text;
using System.Collections.Generic;

public class RobotFeedbackUI : MonoBehaviour
{
    [Header("Refs")]
    public ScreenUIStateController uiState;
    public PathPlanner pathPlanner;

    [Header("Feedback Path Overlay")]
    public GridPathVisualizerUI feedbackVisualizer;   // el componente en FeedbackPathLines
    public RectTransform feedbackPathLines;           // el RectTransform de FeedbackPathLines
    public RectTransform legendRootOptional;          // opcional: leyenda en feedback (puede ser null)

    [Header("Buttons / Groups")]
    public GameObject abortButton;
    public GameObject groupAfterAbort; // contiene Repeat Dimensions, Repeat Grid, Resume Path

    [Header("Robot Outputs UI (TMP)")]
    public TMP_Text statusText;
    public TMP_Text speedText;
    public TMP_Text cmToBoxText;
    public TMP_Text cmToTargetText;
    public TMP_Text deliveredListText;

    // Todas las boxes del plan (en orden visual)
    private List<string> _allBoxLabels = new List<string>();
    private List<PathPlanner.PathSegment> _segments;
    private int _currentSegmentIndex = -1;
    private int _currentCellIndex = 0;   // progreso dentro del segmento
    private bool _hasBox = false;

    private HashSet<string> _deliveredBoxes = new HashSet<string>();

    [Header("Telemetry Params")]
    public float cmPerCell = 30f;   // en tu .ino DISTANCE_cm = 30
    public int nominalSpeed = 30;   // en tu .ino SPEED = 30 (puedes sincronizarlo)

    // ✅ SOLO UNO
    private void OnEnable()
    {
        SubscribeToPlanner();

        // ✅ Evita que ABORT desaparezca por estar dentro del grupo
        EnsureAbortButtonIsNotInsideAfterAbortGroup();

        // ✅ Estado inicial
        ShowAbortOnly();

        StartCoroutine(SetupAndRenderFeedbackPathNextFrame());
    }

    private void OnDisable()
    {
        if (pathPlanner != null)
            pathPlanner.OnActiveSegmentChanged -= HandleActiveSegmentChanged;
    }

    IEnumerator SetupAndRenderFeedbackPathNextFrame()
    {
        yield return null; // esperar 1 frame a que el grid esté listo

        if (pathPlanner == null || pathPlanner.uiGrid == null)
        {
            Debug.LogWarning("RobotFeedbackUI: pathPlanner o pathPlanner.uiGrid es null.");
            yield break;
        }

        if (feedbackVisualizer == null)
        {
            Debug.LogWarning("RobotFeedbackUI: feedbackVisualizer no asignado.");
            yield break;
        }

        // 1) Conectar el grid REAL (aunque se redimensione por código)
        feedbackVisualizer.pathPlanner = pathPlanner;
        feedbackVisualizer.uiGrid = pathPlanner.uiGrid;

        // 2) OverlayRoot (donde dibujamos)
        if (feedbackPathLines != null)
            feedbackVisualizer.overlayRoot = feedbackPathLines;

        // 3) Leyenda opcional
        feedbackVisualizer.legendRoot = legendRootOptional;

        // 4) Hacer que FeedbackPathLines copie el rect del GridContainer para alinearse perfecto
        SyncOverlayToGridContainer();

        // 5) Dibujar
        feedbackVisualizer.RenderPlannedPathPro();

        if (pathPlanner == null || pathPlanner.uiGrid == null ||
            pathPlanner.uiGrid.Rows <= 0 || pathPlanner.uiGrid.Columns <= 0 ||
            pathPlanner.uiGrid.gridContainer == null)
        {
            yield break;
        }

        _segments = pathPlanner.BuildPathSegments();
        _currentSegmentIndex = 0;
        _currentCellIndex = 0;
        _hasBox = false;
        _deliveredBoxes.Clear();

        RefreshOutputsUI();

        yield return null;

        if (pathPlanner == null || pathPlanner.uiGrid == null)
            yield break;

        // ✅ Si el grid aún no existe, no renderices paths
        if (pathPlanner.uiGrid.Rows <= 0 || pathPlanner.uiGrid.Columns <= 0 ||
            pathPlanner.uiGrid.gridContainer == null)
            yield break;

        if (feedbackVisualizer != null) feedbackVisualizer.SetActiveSegment(_currentSegmentIndex);
    }

    void SyncOverlayToGridContainer()
    {
        if (feedbackPathLines == null) return;
        if (pathPlanner == null || pathPlanner.uiGrid == null || pathPlanner.uiGrid.gridContainer == null) return;

        var gridRT = pathPlanner.uiGrid.gridContainer.GetComponent<RectTransform>();
        if (gridRT == null) return;

        // Mismo padre que el gridContainer (recomendado para que coincida)
        feedbackPathLines.SetParent(gridRT.parent, false);

        // Copiar anchors/pos/size
        feedbackPathLines.anchorMin = gridRT.anchorMin;
        feedbackPathLines.anchorMax = gridRT.anchorMax;
        feedbackPathLines.pivot = gridRT.pivot;

        feedbackPathLines.anchoredPosition = gridRT.anchoredPosition;
        feedbackPathLines.sizeDelta = gridRT.sizeDelta;
        feedbackPathLines.localRotation = Quaternion.identity;
        feedbackPathLines.localScale = Vector3.one;

        // Encima del grid
        feedbackPathLines.SetAsLastSibling();
    }

    // Esto lo podrás llamar cuando sepas qué segmento está ejecutando el robot
    public void SetActiveSegment(int index)
    {
        if (feedbackVisualizer != null)
            feedbackVisualizer.SetActiveSegment(index);
    }

    public void OnAbortPressed()
    {
        if (pathPlanner != null) pathPlanner.AbortCurrentPath();
        ShowAfterAbort();
    }

    public void OnResumePressed()
    {
        if (pathPlanner != null) pathPlanner.ResumeCurrentPath();

        // ✅ en Resume: vuelve ABORT y desaparecen los otros
        ShowAbortOnly();
    }

    private void ForceEnableParents(GameObject go)
    {
        if (go == null) return;
        Transform t = go.transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            t = t.parent;
        }
    }

    private void ShowAbortOnly()
    {
        // ✅ los otros fuera
        if (groupAfterAbort != null) groupAfterAbort.SetActive(false);

        // ✅ vuelve ABORT
        if (abortButton != null)
        {
            ForceEnableParents(abortButton);
            abortButton.SetActive(true);
        }
    }

    private void ShowAfterAbort()
    {
        if (abortButton != null) abortButton.SetActive(false);
        if (groupAfterAbort != null) groupAfterAbort.SetActive(true);
    }

    private void SubscribeToPlanner()
    {
        if (pathPlanner == null) return;
        pathPlanner.OnActiveSegmentChanged -= HandleActiveSegmentChanged; // evita doble suscripción
        pathPlanner.OnActiveSegmentChanged += HandleActiveSegmentChanged;
    }

    private void HandleActiveSegmentChanged(int index)
    {
        if (feedbackVisualizer != null)
            feedbackVisualizer.SetActiveSegment(index);
    }

    private int currentSegment = -1;

    public void StartExecutingPlan()
    {
        currentSegment = 0;
        if (feedbackVisualizer != null)
            feedbackVisualizer.SetActiveSegment(currentSegment);
    }

    public void OnRobotDropped()
    {
        currentSegment++;
        if (feedbackVisualizer != null)
            feedbackVisualizer.SetActiveSegment(currentSegment);
    }

    public void OnRobotStoppedOrFinished()
    {
        currentSegment = -1;
        if (feedbackVisualizer != null)
            feedbackVisualizer.SetActiveSegment(-1);
    }

    public void NotifyPlanStarted()
    {
        _segments = (pathPlanner != null) ? pathPlanner.BuildPathSegments() : null;

        _currentSegmentIndex = 0;
        _currentCellIndex = 0;
        _hasBox = false;
        _deliveredBoxes.Clear();
        _allBoxLabels.Clear();

        // Guardamos TODAS las boxes del plan (una vez)
        if (_segments != null)
        {
            foreach (var seg in _segments)
            {
                if (!string.IsNullOrEmpty(seg.boxLabel) && !_allBoxLabels.Contains(seg.boxLabel))
                    _allBoxLabels.Add(seg.boxLabel);
            }
        }

        if (feedbackVisualizer != null)
            feedbackVisualizer.SetActiveSegment(_currentSegmentIndex);

        RefreshOutputsUI();
    }

    public void NotifyMovedOneCell()
    {
        if (_segments == null || _segments.Count == 0) return;
        if (_currentSegmentIndex < 0 || _currentSegmentIndex >= _segments.Count) return;

        _currentCellIndex++;
        _currentCellIndex = Mathf.Min(_currentCellIndex, _segments[_currentSegmentIndex].cells.Count - 1);

        RefreshOutputsUI();
    }

    public void NotifyPickedBox()
    {
        _hasBox = true;
        RefreshOutputsUI();
    }

    public void NotifyDroppedBox()
    {
        _hasBox = false;

        if (_segments != null && _currentSegmentIndex >= 0 && _currentSegmentIndex < _segments.Count)
            _deliveredBoxes.Add(_segments[_currentSegmentIndex].boxLabel);

        _currentSegmentIndex++;
        _currentCellIndex = 0;

        if (_currentSegmentIndex >= (_segments?.Count ?? 0))
        {
            _currentSegmentIndex = -1;
            if (feedbackVisualizer != null) feedbackVisualizer.SetActiveSegment(-1);
        }
        else
        {
            if (feedbackVisualizer != null) feedbackVisualizer.SetActiveSegment(_currentSegmentIndex);
        }

        RefreshOutputsUI();
    }

    private void RefreshOutputsUI()
    {
        if (speedText != null) speedText.text = $"SPEED: {nominalSpeed}";

        if (_segments == null || _segments.Count == 0 || _currentSegmentIndex < 0)
        {
            if (statusText != null) statusText.text = "STATUS: IDLE";
            if (cmToBoxText != null) cmToBoxText.text = "CM TO ARRIVE TO BOX: -";
            if (cmToTargetText != null) cmToTargetText.text = "CM TO ARRIVE TO TARGET: -";
            if (deliveredListText != null) deliveredListText.text = BuildDeliveredListText();
            return;
        }

        var seg = _segments[_currentSegmentIndex];

        if (statusText != null)
            statusText.text = _hasBox ? "STATUS: WITH BOX" : "STATUS: WITHOUT BOX";

        int remainingToBoxCells = 0;
        int remainingToTargetCells = 0;

        int targetIndex = Mathf.Max(0, seg.cells.Count - 1);
        int pickIndex = Mathf.Clamp(seg.pickIndex, 0, targetIndex);

        if (!_hasBox)
        {
            remainingToBoxCells = Mathf.Max(0, pickIndex - _currentCellIndex);
            remainingToTargetCells = Mathf.Max(0, targetIndex - pickIndex);
        }
        else
        {
            remainingToBoxCells = 0;
            remainingToTargetCells = Mathf.Max(0, targetIndex - _currentCellIndex);
        }

        if (cmToBoxText != null)
            cmToBoxText.text = $"CM TO ARRIVE TO BOX: {remainingToBoxCells * cmPerCell:0}";

        if (cmToTargetText != null)
            cmToTargetText.text = $"CM TO ARRIVE TO TARGET: {remainingToTargetCells * cmPerCell:0}";

        if (deliveredListText != null)
            deliveredListText.text = BuildDeliveredListText();
    }

    private string BuildDeliveredListText()
    {
        if (_allBoxLabels == null || _allBoxLabels.Count == 0)
            return "DELIVERED BOXES:\n-";

        var sb = new StringBuilder();
        sb.AppendLine("DELIVERED BOXES:");

        foreach (var box in _allBoxLabels)
        {
            bool delivered = _deliveredBoxes.Contains(box);
            sb.AppendLine(delivered ? $"{box} (delivered)" : box);
        }

        return sb.ToString();
    }

    private void EnsureAbortButtonIsNotInsideAfterAbortGroup()
    {
        if (abortButton == null || groupAfterAbort == null) return;

        var abortT = abortButton.transform;
        var groupT = groupAfterAbort.transform;

        if (abortT.IsChildOf(groupT))
        {
            abortT.SetParent(groupT.parent, worldPositionStays: false);
            abortT.SetAsLastSibling();
            Debug.Log("[RobotFeedbackUI] ABORT button was inside groupAfterAbort. Reparented outside automatically.");
        }
    }
}
