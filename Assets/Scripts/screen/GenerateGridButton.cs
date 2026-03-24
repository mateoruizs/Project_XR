using UnityEngine;
using System.Collections.Generic;

public class GenerateFloorGridButton : MonoBehaviour
{
    public GridGenerator uiGrid;
    public FloorGridGenerator floorGrid;
    public GameObject FloorGridCanvas;
    public ScreenUIStateController uiState;

    public void GenerateFloorGrid()
    {
        FloorGridCanvas.SetActive(true);
        
        List<GridCell> cells = new List<GridCell>(
            uiGrid.gridContainer.GetComponentsInChildren<GridCell>()
        );

        floorGrid.GenerateFromUI(
            cells,
            uiGrid.Rows,
            uiGrid.Columns
        );
        
        if (uiState != null)
            uiState.GoToShowPath();
    }

}

