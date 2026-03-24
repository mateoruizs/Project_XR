using System.Collections;
using UnityEngine;
using TMPro;

public class UIMessageManager : MonoBehaviour
{
    public static UIMessageManager Instance;

    [Header("Text Blocks")]
    [Tooltip("Fixed header text (always visible). Optional.")]
    public TMP_Text headerText;

    [Tooltip("Dynamic instructions text shown under the header. Optional.")]
    public TMP_Text messageText;

    public Color infoColor = Color.white;
    public Color errorColor = Color.red;

    [Header("Error Timing")]
    public float errorSeconds = 3f;

    private Coroutine _hideRoutine;

    // Cache para restaurar tras errores temporales
    private string _cachedHeader;
    private string _cachedMessage;
    private Color _cachedHeaderColor;
    private Color _cachedMessageColor;
    private bool _hasCache;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        ClearImmediate();
    }

    // --------------------------------------------------------------------
    // EditGrid panel helpers (by delivery mode)
    // --------------------------------------------------------------------
    public static void ShowEditGridNearestTarget()
    {
        if (Instance == null) return;

        Instance.SetHeader("<b>NEAREST TARGET MODE:</b> The robot will carry the box to the nearest aviable target");
        Info("Configure the grid by first clicking on the buttons and then on a grid cell. When done press GENERATE GRID");
    }

    public static void ShowEditGridFixedPriority()
    {
        if (Instance == null) return;

        Instance.SetHeader("<b>FIXED PRIORITY MODE:</b> The robot will carry the box to the first target painted. If he can't reach that target the box will be placed in the next target painted");
        Info("Configure the grid by first clicking on the buttons and then on a grid cell. When done press GENERATE GRID");
    }
    
    public static void ShowEditGridBoxToTarget()
    {
        if (Instance == null) return;

        Instance.SetHeader("<b>BOX TO TARGET MODE:</b> The target of each box is determined by the user.");
        Info("Configure the grid by first clicking on the buttons and then on a grid cell. When configured, assigne a target to each box in the menu of the right by grabing and droping each box to his target. When done press GENERATE GRID");
    }


    // --------------------------------------------------------------------
    // Dimensions panel helpers
    // --------------------------------------------------------------------
    public const string DimensionsHeader = "INSERT DIMENSIONS OF THE GRID AND CHOSE DELIVERY MODE";

    public static void ShowDimensionsHeader()
    {
        if (Instance == null) return;
        Instance.SetHeader(DimensionsHeader);
    }

    public static void UpdateDimensionsInstructions(int? rows, int? cols, DeliveryMode deliveryMode)
    {
        if (Instance == null) return;

        if (!rows.HasValue)
        {
            Info("Insert number of rows and press OK");
            return;
        }

        if (!cols.HasValue)
        {
            Info("Insert number of colums and pres OK");
            return;
        }

        if (deliveryMode == DeliveryMode.None)
        {
            Info("Chose delivery mode");
            return;
        }

        Info("Press GENERATE EMPTY GRID to continue");
    }

    // INFO: se queda hasta que lo limpies manualmente
    public static void Info(string msg)
    {
        if (Instance == null) return;
        Instance.ShowPersistent(msg, Instance.infoColor);
    }

    // ERROR: dura X segundos y luego vuelve a lo anterior
    public static void Error(string msg)
    {
        if (Instance == null) return;
        Instance.ShowTimed(msg, Instance.errorColor, Instance.errorSeconds);
    }

    public static void Clear()
    {
        if (Instance == null) return;
        Instance.ClearImmediate();
    }

    private void ShowPersistent(string msg, Color color)
    {
        StopHideRoutineIfAny();

        // Si estábamos mostrando un error temporal, al mostrar info “normal”
        // ya no queremos restaurar lo cacheado.
        ClearCacheOnly();

        if (messageText == null) return;
        messageText.text = msg;
        messageText.color = color;
        messageText.enabled = true;
    }

    private void ShowTimed(string msg, Color color, float seconds)
    {
        StopHideRoutineIfAny();

        // ✅ IMPORTANTE:
        // Cachear SOLO si aún no hay cache.
        // Así evitamos cachear el propio error cuando el usuario lo dispara varias veces.
        if (!_hasCache)
            CacheCurrentTexts();

        if (messageText == null) return;
        messageText.text = msg;
        messageText.color = color;
        messageText.enabled = true;

        _hideRoutine = StartCoroutine(RestoreAfterSeconds(seconds));
    }

    private IEnumerator RestoreAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        // Si alguien hizo Clear() o Info() durante el error, no restauramos nada viejo.
        if (!_hasCache)
        {
            _hideRoutine = null;
            yield break;
        }

        RestoreCachedTexts();
        _hideRoutine = null;
    }

    private void StopHideRoutineIfAny()
    {
        if (_hideRoutine != null)
        {
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }
    }

    private void ClearImmediate()
    {
        StopHideRoutineIfAny();

        if (headerText != null)
        {
            headerText.text = string.Empty;
            headerText.enabled = false;
        }

        if (messageText != null)
        {
            messageText.text = string.Empty;
            messageText.enabled = false;
        }

        ClearCacheOnly();
    }

    private void SetHeader(string msg)
    {
        if (headerText == null) return;
        headerText.text = msg;
        headerText.enabled = true;
        headerText.color = infoColor;
    }

    private void CacheCurrentTexts()
    {
        _hasCache = true;

        if (headerText != null)
        {
            _cachedHeader = headerText.text;
            _cachedHeaderColor = headerText.color;
        }
        else
        {
            _cachedHeader = null;
        }

        if (messageText != null)
        {
            _cachedMessage = messageText.text;
            _cachedMessageColor = messageText.color;
        }
        else
        {
            _cachedMessage = null;
        }
    }

    private void RestoreCachedTexts()
    {
        if (headerText != null)
        {
            headerText.text = _cachedHeader ?? string.Empty;
            headerText.color = _cachedHeaderColor;
            headerText.enabled = !string.IsNullOrEmpty(headerText.text);
        }

        if (messageText != null)
        {
            messageText.text = _cachedMessage ?? string.Empty;
            messageText.color = _cachedMessageColor;
            messageText.enabled = !string.IsNullOrEmpty(messageText.text);
        }

        ClearCacheOnly();
    }

    private void ClearCacheOnly()
    {
        _hasCache = false;
        _cachedHeader = null;
        _cachedMessage = null;
        _cachedHeaderColor = infoColor;
        _cachedMessageColor = infoColor;
    }

    // --------------------------------------------------------------------
    // ShowPath panel helpers
    // --------------------------------------------------------------------

    public static void ShowShowPath()
    {
        if (Instance == null) return;

        Instance.SetHeader("");
        Info("When pressing COMPUTE PATH the robot will make the path shown in the grid.");
    }

}
