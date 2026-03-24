using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class FloorGridGenerator : MonoBehaviour
{
    public RectTransform gridRoot;
    public RectTransform gridContainer; 
    public GridLayoutGroup gridLayout;
    public GameObject floorCellPrefab;
    public Material stencilWriterMaterial;

    public void GenerateFromUI(List<GridCell> uiCells, int rows, int cols)
    {
        foreach (Transform c in gridContainer)
            Destroy(c.gameObject);

        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = cols;
        gridLayout.padding = new RectOffset(
            10, 
            10, 
            10, 
            10  
        );

        for (int i = 0; i < uiCells.Count; i++)
        {
            GameObject cell = Instantiate(floorCellPrefab, gridContainer);
            Image img = cell.GetComponent<Image>();
            GridCell uiCell = uiCells[i];

            if (uiCell.state == CellPaintMode.None)
            {
                // 🪟 Agujero real (stencil)
                img.enabled = true;
                img.material = stencilWriterMaterial;
            }
            else
            {
                img.enabled = true;
                img.material = null; // UI/Default

                switch (uiCell.state)
                {
                    case CellPaintMode.Prohibited:
                        img.color = ColorPaletteManager.Instance.prohibitedColor;
                        break;

                    case CellPaintMode.Box:
                        img.color = ColorPaletteManager.Instance.boxColor;
                        break;

                    case CellPaintMode.Target:
                        img.color = ColorPaletteManager.Instance.targetColor;
                        break;

                    case CellPaintMode.Robot:
                        img.color = ColorPaletteManager.Instance.robotColor;
                        break;
                }
            }
        }


        LayoutRebuilder.ForceRebuildLayoutImmediate(gridContainer);

        AdjustRootSizeToGrid(rows, cols);

    }
        
    private void AdjustRootSizeToGrid(int rows, int cols)
    {
        float cellW = gridLayout.cellSize.x;
        float cellH = gridLayout.cellSize.y;

        float spacingX = gridLayout.spacing.x;
        float spacingY = gridLayout.spacing.y;

        int padL = gridLayout.padding.left;
        int padR = gridLayout.padding.right;
        int padT = gridLayout.padding.top;
        int padB = gridLayout.padding.bottom;

        float width =
            padL + padR +
            cols * cellW +
            (cols - 1) * spacingX;

        float height =
            padT + padB +
            rows * cellH +
            (rows - 1) * spacingY;

        gridRoot.sizeDelta = new Vector2(width, height);

    }


}
