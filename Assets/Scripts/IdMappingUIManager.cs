using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IdMappingUIManager : MonoBehaviour
{
    [Header("Refs")]
    public PathPlanner pathPlanner;
    public GridGenerator uiGrid;

    [Header("Drag Layer (UI)")]
    public RectTransform dragLayer; // arrastra aquí tu DragLayer (dentro del Canvas)

    [Header("UI Root (solo Modo C)")]
    public GameObject modeCRoot;

    [Header("UI Containers")]
    public RectTransform targetsContent;   // ScrollView/Viewport/Content
    public RectTransform boxesTray;        // Bandeja "Boxes:"
    public TMP_Text hintLabel;
    public Button mainActionButton; // ← aquí arrastras Btn_Grid_Generator


    [Header("Prefabs")]
    public TargetBinUI targetBinPrefab;
    public BoxChipUI boxChipPrefab;

    public Dictionary<int, int> GetMappingCopy()
    {
        return new Dictionary<int, int>(mapping);
    }


    // mapping final: boxOrder -> targetOrder
    private readonly Dictionary<int, int> mapping = new Dictionary<int, int>();

    // cache UI
    private readonly Dictionary<int, TargetBinUI> bins = new Dictionary<int, TargetBinUI>();
    private readonly Dictionary<int, BoxChipUI> chips = new Dictionary<int, BoxChipUI>();

    private void OnEnable()
    {
        GridCell.AnyCellClicked += OnAnyCellClicked;
    }

    private void OnDisable()
    {
        GridCell.AnyCellClicked -= OnAnyCellClicked;
    }

   public void SetVisibleForModeC(bool visible)
    {
        if (modeCRoot != null) modeCRoot.SetActive(visible);

        // ✅ IMPORTANTE: al ocultar NO limpiamos mapping, porque lo necesitamos en ShowPath
        if (!visible) return;

        RefreshFromGrid();
    }


    private void OnAnyCellClicked(GridCell cell)
    {
        if (modeCRoot == null || !modeCRoot.activeSelf) return;
        if (pathPlanner == null || pathPlanner.deliveryMode != DeliveryMode.IdMapping) return;

        // ✅ robusto: refresca SIEMPRE al cambiar el grid
        RefreshFromGrid();
    }

    public void RefreshFromGrid()
    {
        if (pathPlanner == null || uiGrid == null)
        {
            SetHint("Faltan referencias (PathPlanner o GridGenerator).");
            ClearAllUIAndState();
            return;
        }

        // ✅ Si no hay grid generado aún, limpiamos y salimos
        if (uiGrid.Rows <= 0 || uiGrid.Columns <= 0 || uiGrid.gridContainer == null)
        {
            ClearAllUIAndState();
            return;
        }

        var cells = uiGrid.gridContainer.GetComponentsInChildren<GridCell>();
        if (cells == null || cells.Length == 0)
        {
            ClearAllUIAndState();
            return;
        }

        // Targets por order (T1, T2, ...)
        var targetOrders = cells
            .Where(c => c.state == CellPaintMode.Target && c.targetOrder >= 0)
            .Select(c => c.targetOrder)
            .Distinct()
            .OrderBy(o => o)
            .ToList();

        // Boxes por order (B1, B2, ...)
        var boxOrders = cells
            .Where(c => c.state == CellPaintMode.Box && c.boxOrder >= 0)
            .Select(c => c.boxOrder)
            .Distinct()
            .OrderBy(o => o)
            .ToList();

        // limpieza de mapping inválido
        var boxSet = new HashSet<int>(boxOrders);
        var keysToRemove = mapping.Keys.Where(k => !boxSet.Contains(k)).ToList();
        foreach (var k in keysToRemove) mapping.Remove(k);

        var targetSet = new HashSet<int>(targetOrders);
        var badTargets = mapping.Where(kv => !targetSet.Contains(kv.Value)).Select(kv => kv.Key).ToList();
        foreach (var b in badTargets) mapping.Remove(b);

        // reconstruir UI
        ClearContainer(targetsContent);
        ClearContainer(boxesTray);
        bins.Clear();
        chips.Clear();

        // bins
        foreach (var tOrder in targetOrders)
        {
            var bin = Instantiate(targetBinPrefab, targetsContent);
            bin.transform.localScale = Vector3.one;
            bin.Init(tOrder, this);
            bins[tOrder] = bin;
        }

        // chips
        foreach (var bOrder in boxOrders)
        {
            var chip = Instantiate(boxChipPrefab, boxesTray);
            chip.transform.localScale = Vector3.one;

            // ✅ pasar dragLayer al chip (para que NO desaparezca mientras arrastras)
            chip.Init(bOrder, this, dragLayer);
            chips[bOrder] = chip;

            // si ya estaba asignada, recolocar
            if (mapping.TryGetValue(bOrder, out int mappedTarget) && bins.TryGetValue(mappedTarget, out var bin))
            {
                chip.transform.SetParent(bin.dropArea, false);
                chip.transform.localScale = Vector3.one;
                chip.SetAssigned(true, mappedTarget);
            }
            else
            {
                chip.SetAssigned(false, -1);
            }
        }

        UpdateCompileUI(boxOrders, targetOrders);
        foreach (var b in bins.Values)
        b.RefreshDropHint();
    }

    private void ClearContainer(RectTransform rt)
    {
        if (rt == null) return;
        for (int i = rt.childCount - 1; i >= 0; i--)
            Destroy(rt.GetChild(i).gameObject);
    }

    public void AssignBoxToTarget(int boxOrder, int targetOrder)
    {
        int oldTarget = -1;
        if (mapping.TryGetValue(boxOrder, out int prev))
            oldTarget = prev;

        mapping[boxOrder] = targetOrder;

        if (chips.TryGetValue(boxOrder, out var chip) && bins.TryGetValue(targetOrder, out var bin))
        {
            chip.transform.SetParent(bin.dropArea, false);
            chip.transform.localScale = Vector3.one;
            chip.SetAssigned(true, targetOrder);
            chip.MarkDropped();
        }

        // refrescar hint del bin anterior (si cambió) y del actual
        if (oldTarget != -1 && oldTarget != targetOrder && bins.TryGetValue(oldTarget, out var oldBin))
            oldBin.RefreshDropHint();

        if (bins.TryGetValue(targetOrder, out var newBin))
            newBin.RefreshDropHint();

        UpdateCompileUI(chips.Keys.OrderBy(x => x).ToList(), bins.Keys.OrderBy(x => x).ToList());
    }


    public void ReturnBoxToTray(int boxOrder)
    {
        int oldTarget = -1;
        if (mapping.TryGetValue(boxOrder, out int prev))
            oldTarget = prev;

        mapping.Remove(boxOrder);

        if (chips.TryGetValue(boxOrder, out var chip))
        {
            chip.transform.SetParent(boxesTray, false);
            chip.transform.localScale = Vector3.one;
            chip.SetAssigned(false, -1);
        }

        if (oldTarget != -1 && bins.TryGetValue(oldTarget, out var oldBin))
            oldBin.RefreshDropHint();

        UpdateCompileUI(chips.Keys.OrderBy(x => x).ToList(), bins.Keys.OrderBy(x => x).ToList());
    }
    

    private void UpdateCompileUI(List<int> boxOrders, List<int> targetOrders)
    {
        // Este botón es el mismo que usas en A/B (Btn_Grid_Generator).
        // En modo C lo bloqueamos hasta que TODO esté bien.
        if (mainActionButton == null)
        {
            if (targetOrders.Count == 0) SetHint("Añade al menos 1 TARGET en el grid para asignar boxes.");
            else if (boxOrders.Count == 0) SetHint("Añade al menos 1 BOX en el grid para asignar.");
            else SetHint("Arrastra cada Box (B1, B2...) al Target correspondiente (T1, T2...).");
            return;
        }

        bool isModeCActive =
            (modeCRoot != null && modeCRoot.activeSelf) &&
            pathPlanner != null &&
            pathPlanner.deliveryMode == DeliveryMode.IdMapping;

        // Si NO estamos en modo C, no tocamos el botón (A/B)
        if (!isModeCActive)
        {
            mainActionButton.interactable = true;
            return;
        }

        if (targetOrders.Count == 0)
        {
            mainActionButton.interactable = false;
            SetHint("Añade al menos 1 TARGET en el grid (modo C).");
            return;
        }

        if (boxOrders.Count == 0)
        {
            mainActionButton.interactable = false;
            SetHint("Añade al menos 1 BOX en el grid (modo C).");
            return;
        }

        // 1) Todas las boxes asignadas
        bool allBoxesAssigned = boxOrders.All(b => mapping.ContainsKey(b));

        // 2) Todos los targets tienen al menos 1 box
        var assignedTargets = new HashSet<int>(mapping.Values);
        bool allTargetsHaveBox = targetOrders.All(t => assignedTargets.Contains(t));

        mainActionButton.interactable = allBoxesAssigned && allTargetsHaveBox;

        if (!allBoxesAssigned)
            SetHint("Te faltan asignaciones: arrastra cada Box (B1, B2...) a un Target (T1, T2...).");
        else if (!allTargetsHaveBox)
            SetHint("Falta al menos 1 Target vacío: todos los Targets deben tener al menos una Box.");
        else
            SetHint("Todo correcto. Pulsa GENERATE GRID para compilar el modo C.");
    }


    private void ClearAllUIAndState()
    {
        mapping.Clear();
        bins.Clear();
        chips.Clear();

        ClearContainer(targetsContent);
        ClearContainer(boxesTray);

        // Solo bloqueamos el botón principal si estamos en modo C
        bool isModeCActive =
            (modeCRoot != null && modeCRoot.activeSelf) &&
            pathPlanner != null &&
            pathPlanner.deliveryMode == DeliveryMode.IdMapping;

        if (isModeCActive && mainActionButton != null)
            mainActionButton.interactable = false;

        if (hintLabel != null) hintLabel.text = "";
    }

    private void SetHint(string message)
    {
        if (hintLabel == null) return;
        hintLabel.text = message;
    }

    // Permite que otros scripts (GridCell) muestren avisos en el texto de modo C
    public void ShowHintExternal(string msg)
    {
        if (hintLabel != null) hintLabel.text = msg;
    }


}
