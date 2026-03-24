using System.Collections.Generic;
using UnityEngine;

public class GridTargetRenumberer : MonoBehaviour
{
    [Header("Refs")]
    public GridGenerator uiGrid;
    public PathPlanner pathPlanner; // opcional pero recomendado

    private void OnEnable()
    {
        GridCell.AnyCellClicked += OnAnyCellClicked;
    }

    private void OnDisable()
    {
        GridCell.AnyCellClicked -= OnAnyCellClicked;
    }

    private void OnAnyCellClicked(GridCell _)
    {
        if (uiGrid == null || uiGrid.gridContainer == null) return;
        if (uiGrid.Rows <= 0 || uiGrid.Columns <= 0) return;

        // ✅ SOLO en modo C
        var planner = pathPlanner != null ? pathPlanner : PathPlanner.Instance;
        if (planner == null ||
            (planner.deliveryMode != DeliveryMode.IdMapping &&
            planner.deliveryMode != DeliveryMode.FixedPriority))
            return;
        RenumberTargets();
    }

    private void RenumberTargets()
    {
        var cells = uiGrid.gridContainer.GetComponentsInChildren<GridCell>();

        // recolectar targets existentes
        var targetCells = new List<GridCell>();
        foreach (var c in cells)
        {
            if (c.state == CellPaintMode.Target && c.targetOrder >= 0)
                targetCells.Add(c);
        }

        // ordenar por targetOrder antiguo (mantiene orden de creación de los que quedan)
        targetCells.Sort((a, b) => a.targetOrder.CompareTo(b.targetOrder));

        // reasignar 0..N-1
        for (int i = 0; i < targetCells.Count; i++)
        {
            targetCells[i].targetOrder = i;
            targetCells[i].RefreshVisual();
        }

        // 🔥 que el siguiente Target sea T(N+1) y no se salte números
        GridCell.SetTargetCounter(targetCells.Count);
    }
}
