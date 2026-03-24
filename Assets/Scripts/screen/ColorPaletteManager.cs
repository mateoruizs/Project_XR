using UnityEngine;

public class ColorPaletteManager : MonoBehaviour
{
    public static ColorPaletteManager Instance;

    public Color prohibitedColor = Color.red;
    public Color boxColor = Color.green;
    public Color targetColor = Color.yellow;
    public Color robotColor = Color.blue;
    public Color eraseColor = Color.white;

    public CellPaintMode currentMode = CellPaintMode.None;

    private void Awake()
    {
        // NO forzamos aquí el Instance si luego se activan/desactivan paneles.
        // (Awake solo corre una vez en la vida del objeto)
    }

    private void OnEnable()
    {
        // El palette que está activo es el que manda
        Instance = this;
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SelectProhibited() => currentMode = CellPaintMode.Prohibited;
    public void SelectBox()        => currentMode = CellPaintMode.Box;
    public void SelectTarget()     => currentMode = CellPaintMode.Target;
    public void SelectRobot()      => currentMode = CellPaintMode.Robot;
    public void SelectErase()      => currentMode = CellPaintMode.None;
}
