using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GridCell : MonoBehaviour
{
    public static event Action<GridCell> AnyCellClicked;

    [Header("State")]
    public CellPaintMode state = CellPaintMode.None;

    [Header("Orders")]
    public int targetOrder = -1;
    public int boxOrder = -1;

    [Header("UI")]
    public TMP_Text label;

    // contadores globales
    private static int _targetCounter = 0;
    private static int _boxCounter = 0;

    private const int MAX_BOXES_MODE_C = 4;
    private const int MAX_TARGETS_MODE_C = 4;

    private Image _img;

    private void Awake()
    {
        _img = GetComponent<Image>();
        RefreshVisual();
    }

    public static void ResetTargetOrderCounter() => _targetCounter = 0;
    public static void ResetBoxOrderCounter() => _boxCounter = 0;

    // Llamado desde el OnClick del Button del cell
    public void OnCellClicked()
    {
        var palette = ColorPaletteManager.Instance;
        if (palette == null) return;

        // ✅ Limitar SOLO en modo C (IdMapping): máximo 4 BOX y 4 TARGET
        if (PathPlanner.Instance != null &&
            PathPlanner.Instance.deliveryMode == DeliveryMode.IdMapping)
        {
            var desired = palette.currentMode;

            if (desired == CellPaintMode.Box && state != CellPaintMode.Box)
            {
                CountCurrent(out int b, out int t);
                if (b >= MAX_BOXES_MODE_C)
                {
                    UIMessageManager.Error(
                        $"Limit reached: maximum {MAX_BOXES_MODE_C} boxes allowed in Mode BOX TO TARGET."
                    );
                    return;
                }
            }

            if (desired == CellPaintMode.Target && state != CellPaintMode.Target)
            {
                CountCurrent(out int b, out int t);
                if (t >= MAX_TARGETS_MODE_C)
                {
                    UIMessageManager.Error(
                        $"Limit reached: maximum {MAX_TARGETS_MODE_C} targets allowed in Mode BOX TO TARGET"
                    );
                    return;
                }
            }
        }

        // Aplicar “pintura” según modo
        switch (palette.currentMode)
        {
            case CellPaintMode.Target:
                SetState(CellPaintMode.Target);
                break;

            case CellPaintMode.Box:
                SetState(CellPaintMode.Box);
                break;

            case CellPaintMode.Robot:
            {
                // ✅ Si ya hay un robot en otra celda, no permitimos otro
                var all = FindObjectsOfType<GridCell>(true);
                foreach (var c in all)
                {
                    if (c != this && c.state == CellPaintMode.Robot)
                    {
                        UIMessageManager.Error("There can only be one robot");
                        return; // ⛔ no cambiamos nada, dejamos el robot existente
                    }
                }

                SetState(CellPaintMode.Robot);
                break;
            }


            case CellPaintMode.Prohibited:
                SetState(CellPaintMode.Prohibited);
                break;

            // "Erase" = None
            case CellPaintMode.None:
            default:
                SetState(CellPaintMode.None);
                break;
        }

        // color + visual
        ApplyColorFromPalette(palette);
        RefreshVisual();
        AnyCellClicked?.Invoke(this);
    }

    private void ApplyColorFromPalette(ColorPaletteManager palette)
    {
        if (_img == null) return;

        _img.color = state switch
        {
            CellPaintMode.Prohibited => palette.prohibitedColor,
            CellPaintMode.Box => palette.boxColor,
            CellPaintMode.Target => palette.targetColor,
            CellPaintMode.Robot => palette.robotColor,
            _ => palette.eraseColor,
        };
    }

    public void SetState(CellPaintMode newState)
    {
        state = newState;

        if (state == CellPaintMode.Target)
        {
            if (targetOrder < 0) targetOrder = _targetCounter++;
            boxOrder = -1;
        }
        else if (state == CellPaintMode.Box)
        {
            if (boxOrder < 0) boxOrder = _boxCounter++;
            targetOrder = -1;
        }
        else
        {
            targetOrder = -1;
            boxOrder = -1;
        }
    }

    private static void CountCurrent(out int boxes, out int targets)
    {
        boxes = 0;
        targets = 0;

        var all = FindObjectsOfType<GridCell>(true);
        foreach (var c in all)
        {
            if (c.state == CellPaintMode.Box) boxes++;
            else if (c.state == CellPaintMode.Target) targets++;
        }
    }

    public void RefreshVisual()
    {
        if (label == null) return;

        label.color = Color.black;
        label.text = string.Empty;

        switch (state)
        {
            case CellPaintMode.Box:
                label.text = boxOrder >= 0 ? $"B{boxOrder + 1}" : string.Empty;
                break;

            case CellPaintMode.Target:
                RefreshTargetLabel();
                break;

            case CellPaintMode.Robot:
                label.text = "R";
                break;

            default:
                label.text = string.Empty;
                break;
        }
    }

    private void RefreshTargetLabel()
    {
        // Si no hay planner, no mostramos nada
        if (PathPlanner.Instance == null)
        {
            label.text = string.Empty;
            return;
        }

        var mode = PathPlanner.Instance.deliveryMode;

        // 🔴 SOLO modo B: PRIORITY / 2º / 3º...
        if (mode == DeliveryMode.FixedPriority)
        {
            if (targetOrder == 0) label.text = "PRIORITY";
            else label.text = $"{targetOrder + 1}º";
            label.color = Color.black;
            return;
        }

        // 🟢 modo C: T1 / T2...
        if (mode == DeliveryMode.IdMapping)
        {
            label.text = $"T{targetOrder + 1}";
            label.color = Color.black;
            return;
        }

        // 🟡 modo A: también mostrar T1 / T2...
        if (mode == DeliveryMode.NearestTarget)
        {
            label.text = $"T{targetOrder + 1}";
            label.color = Color.black;
            return;
        }

        // ⚪ otros: sin texto
        label.text = string.Empty;

    }

    public static void SetBoxCounter(int nextValue) => _boxCounter = nextValue;
    public static void SetTargetCounter(int nextValue) => _targetCounter = nextValue;
}
