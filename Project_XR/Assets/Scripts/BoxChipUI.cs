using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class BoxChipUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public TMP_Text label;

    [Header("Drag Visuals")]
    public float dragScale = 1.15f;
    public float dragAlpha = 1f;

    public int BoxOrder { get; private set; }

    private IdMappingUIManager _manager;
    private RectTransform _rect;
    private CanvasGroup _cg;

    private Transform _originalParent;
    private RectTransform _dragLayer;

    private bool _assigned;
    private int _assignedTargetOrder = -1;

    private bool _droppedSuccessfully;
    private Vector2 _dragOffsetLocal;

    public void Init(int boxOrder, IdMappingUIManager manager, RectTransform dragLayer)
    {
        BoxOrder = boxOrder;
        _manager = manager;
        _dragLayer = dragLayer;

        _rect = GetComponent<RectTransform>();

        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

        SetAssigned(false, -1);
        _droppedSuccessfully = false;
        _dragOffsetLocal = Vector2.zero;
    }

    public void SetAssigned(bool assigned, int targetOrder)
    {
        _assigned = assigned;
        _assignedTargetOrder = targetOrder;
        UpdateLabel();
    }

    public void MarkDropped()
    {
        _droppedSuccessfully = true;
    }

    private void UpdateLabel()
    {
        if (label == null) return;
        label.text = $"B{BoxOrder + 1}";    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _droppedSuccessfully = false;
        _originalParent = transform.parent;

        // IMPORTANTE: para que el Drop reciba el raycast
        _cg.blocksRaycasts = false;

        // subir al dragLayer manteniendo posición en mundo (evita saltos raros)
        if (_dragLayer != null)
        {
            transform.SetParent(_dragLayer, true);
            transform.SetAsLastSibling();
        }

        // visual
        transform.localScale = Vector3.one * dragScale;
        _cg.alpha = dragAlpha;

        // calcular offset en coords del nuevo parent (dragLayer)
        RectTransform parentRT = _rect.parent as RectTransform;
        if (parentRT != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            _dragOffsetLocal = _rect.anchoredPosition - localPoint;
        }
        else
        {
            _dragOffsetLocal = Vector2.zero;
        }

        MoveToPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        MoveToPointer(eventData);
    }

    private void MoveToPointer(PointerEventData eventData)
    {
        RectTransform parentRT = _rect.parent as RectTransform;
        if (parentRT == null) return;

        // Si en XR tu camera viene null, cambia pressEventCamera -> enterEventCamera
        var cam = eventData.pressEventCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRT,
            eventData.position,
            cam,
            out Vector2 localPoint))
        {
            _rect.anchoredPosition = localPoint + _dragOffsetLocal;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _cg.blocksRaycasts = true;
        transform.localScale = Vector3.one;
        _cg.alpha = 1f;

        // Si ningún bin lo aceptó, volver al sitio original
        if (!_droppedSuccessfully && _originalParent != null)
        {
            transform.SetParent(_originalParent, false);
            transform.localScale = Vector3.one;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_assigned) return;
        _manager.ReturnBoxToTray(BoxOrder);
    }
}
