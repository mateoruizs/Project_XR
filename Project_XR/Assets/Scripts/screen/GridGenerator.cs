using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GridGenerator : MonoBehaviour
{
    public GameObject cellPrefab;
    public RectTransform gridContainer;
    public GridLayoutGroup gridLayout;
    public FloorGridGenerator floorGrid;
    public int Rows { get; private set; }
    public int Columns { get; private set; }
    private Vector2 maxContainerSize;
    public Color obstacleColor = Color.red;
    public Color boxColor = Color.green;
    public Color targetColor = Color.yellow;
    public Color robotColor = Color.blue;

    public void GenerateGrid(int rows, int cols)
    {
        Rows = rows;
        Columns = cols; 

     foreach (Transform c in gridContainer)
        Destroy(c.gameObject);

        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = cols;
        
        AdjustCellSizeAndContainerToFit(rows, cols);

     for (int x = 0; x < rows; x++)
        {
            for (int y = 0; y < cols; y++)
            {
                Instantiate(cellPrefab, gridContainer);
            }
        }

    }

    private void AdjustCellSizeAndContainerToFit(int rows, int cols)
    {
        // Padding que quieres
        gridLayout.padding = new RectOffset(
            10, // left
            10, // right
            10, // top
            10  // bottom
        );

        float spacingX = gridLayout.spacing.x;
        float spacingY = gridLayout.spacing.y;

        int padL = gridLayout.padding.left;
        int padR = gridLayout.padding.right;
        int padT = gridLayout.padding.top;
        int padB = gridLayout.padding.bottom;

        // Tamaño máximo permitido del contenedor (lo guardaste en maxContainerSize)
        float maxW = maxContainerSize.x;
        float maxH = maxContainerSize.y;

        // ✅ OJO: ahora restamos también padding del espacio disponible
        float availableWidth  = maxW - padL - padR - (cols - 1) * spacingX;
        float availableHeight = maxH - padT - padB - (rows - 1) * spacingY;

        float cellW = availableWidth / cols;
        float cellH = availableHeight / rows;

        float finalSize = Mathf.Max(0f, Mathf.Min(cellW, cellH));
        gridLayout.cellSize = new Vector2(finalSize, finalSize);

        // Tamaño real ocupado por el grid (incluyendo padding)
        float requiredW = padL + padR + cols * finalSize + (cols - 1) * spacingX;
        float requiredH = padT + padB + rows * finalSize + (rows - 1) * spacingY;

        // ✅ El contenedor SOLO ENCOGE (nunca crece por encima del máximo)
        float newW = Mathf.Min(requiredW, maxW);
        float newH = Mathf.Min(requiredH, maxH);

        gridContainer.sizeDelta = new Vector2(newW, newH);

        // ✅ A veces Unity no recalcula al vuelo: forzamos rebuild
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(gridContainer);
    }



    private void Awake()
    {
        // Tamaño máximo permitido (el que ya tiene el contenedor en tu UI)
        maxContainerSize = gridContainer.rect.size;
    }



}
