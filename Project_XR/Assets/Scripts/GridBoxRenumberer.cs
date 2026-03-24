using System.Collections.Generic;
using UnityEngine;

public class GridBoxRenumberer : MonoBehaviour
{
    [Header("Refs")]
    public GridGenerator uiGrid;

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
        // si aún no hay grid generado, no hacemos nada
        if (uiGrid == null || uiGrid.gridContainer == null) return;
        if (uiGrid.Rows <= 0 || uiGrid.Columns <= 0) return;

        RenumberBoxes();
    }

    private void RenumberBoxes()
    {
        var cells = uiGrid.gridContainer.GetComponentsInChildren<GridCell>();

        // recolectar boxes que existen (guardamos su orden anterior para mantener "orden de creación")
        var boxCells = new List<GridCell>();
        foreach (var c in cells)
        {
            if (c.state == CellPaintMode.Box && c.boxOrder >= 0)
                boxCells.Add(c);
        }

        // ordenar por boxOrder antiguo (así mantenemos el "orden de creación" de las que quedan)
        boxCells.Sort((a, b) => a.boxOrder.CompareTo(b.boxOrder));

        // reasignar 0..N-1
        for (int i = 0; i < boxCells.Count; i++)
        {
            boxCells[i].boxOrder = i;
            boxCells[i].RefreshVisual();
        }

        // 🔥 muy importante: que el siguiente Box salga como B(N+1), no se salte números
        GridCell.SetBoxCounter(boxCells.Count);
    }
}
