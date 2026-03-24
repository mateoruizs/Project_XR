using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GridPathVisualizerUI : MonoBehaviour
{
    [Header("Refs")]
    public PathPlanner pathPlanner;
    public GridGenerator uiGrid;

    [Header("Path Separation")]
    public float laneOffset = 10f; // unidades UI, prueba 8-14

    [Header("Active Segment Highlight")]
    public bool enableActiveHighlight = true;
    public int activeSegmentIndex = -1;      // -1 = ninguno activo
    [Range(0f, 1f)] public float dimAlpha = 0.25f; // alpha de los NO activos

    [Header("Legend")]
    public RectTransform legendRoot;
    public GameObject legendTextPrefab;
    public GameObject legendItemPrefab;


    [Tooltip("RectTransform encima del grid (HERMANO de GridContainer, NO hijo).")]
    public RectTransform overlayRoot;

    [Header("Dash Look (UI units)")]
    public float dashLength = 20f;
    public float gapLength = 14f;
    public float lineWidth = 6f;

    [Header("Arrow")]
    public int arrowEveryNDashes = 4;
    public string arrowChar = "➤";
    public TMP_FontAsset arrowFont;           // opcional
    public float arrowFontSize = 28f;
    public Vector2 arrowOffset = new Vector2(0, 0);

    [Header("Colors per segment")]
    public List<Color> segmentColors = new List<Color>
    {
        new Color(0.2f, 0.6f, 1f, 1f),
        new Color(0.4f, 1f, 0.4f, 1f),
        new Color(1f, 0.6f, 0.2f, 1f),
        new Color(1f, 0.3f, 0.6f, 1f),
        new Color(0.8f, 0.8f, 0.2f, 1f),
    };

    readonly List<GameObject> spawned = new List<GameObject>();

    public void Clear()
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i]);
        spawned.Clear();

        if (legendRoot != null)
        {
            for (int i = legendRoot.childCount - 1; i >= 0; i--)
                Destroy(legendRoot.GetChild(i).gameObject);
        }
    }

    public void RenderPlannedPathPro()
    {
        Clear();

        if (pathPlanner == null || uiGrid == null || overlayRoot == null) return;

        var segments = pathPlanner.BuildPathSegments();
        if (segments == null || segments.Count == 0) return;

        int rows = uiGrid.Rows;
        int cols = uiGrid.Columns;

        var cells = uiGrid.gridContainer.GetComponentsInChildren<GridCell>(true);
        if (cells == null || cells.Length != rows * cols) return;

        Vector2 CellLocal(Vector2Int p)
        {
            int idx = p.x * cols + p.y;
            var rt = cells[idx].GetComponent<RectTransform>();
            Vector3 world = rt.position;
            return (Vector2)overlayRoot.InverseTransformPoint(world);
        }

        for (int s = 0; s < segments.Count; s++)
        {
            var seg = segments[s];
            if (seg.cells == null || seg.cells.Count < 2) continue;

            Color c = segmentColors.Count > 0 ? segmentColors[s % segmentColors.Count] : Color.white;

            if (enableActiveHighlight && activeSegmentIndex >= 0 && s != activeSegmentIndex)
                c.a *= dimAlpha;

            // Leyenda
            SpawnLegendEntry($"{s + 1}º {seg.label}", c);

            // Offset por segmento (carriles): -0.5, +0.5, -1.5, +1.5...
            float lane = ((s % 2 == 0) ? -1f : 1f) * (0.5f + (s / 2)) * laneOffset;

            int dashIndexGlobal = 0;

            for (int i = 1; i < seg.cells.Count; i++)
            {
                Vector2 a = CellLocal(seg.cells[i - 1]);
                Vector2 b = CellLocal(seg.cells[i]);

                Vector2 dir = (b - a);
                if (dir.sqrMagnitude < 0.0001f) continue;
                dir.Normalize();

                // perpendicular (izquierda)
                Vector2 perp = new Vector2(-dir.y, dir.x);

                // aplica carril para que no se pisen
                a += perp * lane;
                b += perp * lane;

                dashIndexGlobal = SpawnDashedSegmentWithArrows(a, b, c, dashIndexGlobal);
            }
        }
    }

    int SpawnDashedSegmentWithArrows(Vector2 a, Vector2 b, Color color, int dashIndexStart)
    {
        Vector2 dir = (b - a);
        float len = dir.magnitude;
        if (len < 0.01f) return dashIndexStart;

        dir /= len;

        float step = dashLength + gapLength;
        int count = Mathf.FloorToInt(len / step);
        float dist = 0f;

        int dashIndex = dashIndexStart;

        for (int i = 0; i < count; i++)
        {
            Vector2 p0 = a + dir * dist;
            Vector2 p1 = a + dir * (dist + dashLength);
            SpawnDash(p0, p1, color);

            dashIndex++;
            if (arrowEveryNDashes > 0 && (dashIndex % arrowEveryNDashes) == 0)
                SpawnArrow(p1 + arrowOffset, dir, color);

            dist += step;
        }

        float remaining = len - dist;
        if (remaining > 1f)
        {
            float d = Mathf.Min(dashLength, remaining);
            Vector2 p0 = a + dir * dist;
            Vector2 p1 = a + dir * (dist + d);
            SpawnDash(p0, p1, color);

            dashIndex++;
            if (arrowEveryNDashes > 0 && (dashIndex % arrowEveryNDashes) == 0)
                SpawnArrow(p1 + arrowOffset, dir, color);
        }

        return dashIndex;
    }

    void SpawnDash(Vector2 p0, Vector2 p1, Color color)
    {
        var go = new GameObject("Dash", typeof(RectTransform), typeof(Image));
        spawned.Add(go);

        var rt = (RectTransform)go.transform;
        rt.SetParent(overlayRoot, false);

        Vector2 mid = (p0 + p1) * 0.5f;
        Vector2 v = (p1 - p0);
        float dashLen = v.magnitude;

        rt.anchoredPosition = mid;
        rt.sizeDelta = new Vector2(dashLen, lineWidth);

        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0, 0, angle);

        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    void SpawnArrow(Vector2 pos, Vector2 dir, Color color)
    {
        var go = new GameObject("Arrow", typeof(RectTransform), typeof(TextMeshProUGUI));
        spawned.Add(go);

        var rt = (RectTransform)go.transform;
        rt.SetParent(overlayRoot, false);
        rt.anchoredPosition = pos;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0, 0, angle);

        // ✅ el TMP está en ESTE go, no en "label"
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = arrowChar;          // "➤"
        tmp.fontSize = arrowFontSize;
        tmp.color = color;
        if (arrowFont != null) tmp.font = arrowFont;
        tmp.raycastTarget = false;
        tmp.alignment = TextAlignmentOptions.Center;

        // tamaño cómodo para que se vea
        rt.sizeDelta = new Vector2(40, 40);
    }


    void SpawnLegendEntry(string labelText, Color color)
    {
        if (legendRoot == null || legendTextPrefab == null) return;

        // Instancia TU prefab (con tu fuente/estilo)
        var go = Instantiate(legendTextPrefab, legendRoot);
        spawned.Add(go);

        var rt = go.GetComponent<RectTransform>();
        var tmp = go.GetComponent<TMPro.TextMeshProUGUI>();
        if (tmp == null) return;

        // Texto y color del path
        tmp.text = labelText;
        tmp.color = color;

        // Asegura que ocupa el ancho del legend (lo estira)
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);

        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, rt.sizeDelta.y); // ancho controlado por anchors
    }

    public void SetActiveSegment(int index)
    {
        activeSegmentIndex = index;
        RenderPlannedPathPro();
    }

    
}
