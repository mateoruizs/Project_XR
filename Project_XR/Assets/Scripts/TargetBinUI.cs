using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class TargetBinUI : MonoBehaviour, IDropHandler
{
    public TMP_Text title;
    public RectTransform dropArea;

    [Header("Hint text inside DropArea")]
    public TMP_Text dropHint; // <- arrastra aquí PropArea/Text

    private int _targetOrder;
    private IdMappingUIManager _manager;

    public void Init(int targetOrder, IdMappingUIManager manager)
    {
        _targetOrder = targetOrder;
        _manager = manager;

        if (title != null)
            title.text = $"T{targetOrder + 1}";

        if (dropHint != null)
            dropHint.text = $"Drop the boxes you want in T{targetOrder + 1}";

        RefreshDropHint();
    }

    public void OnDrop(PointerEventData eventData)
    {
        var chip = eventData.pointerDrag?.GetComponent<BoxChipUI>();
        if (chip == null) return;

        _manager.AssignBoxToTarget(chip.BoxOrder, _targetOrder);
        // (el manager moverá el chip; él llamará a RefreshDropHint en ambos bins)
    }

    public void RefreshDropHint()
    {
        if (dropHint == null || dropArea == null) return;

        bool hasAnyChip = dropArea.GetComponentInChildren<BoxChipUI>(true) != null;

        dropHint.gameObject.SetActive(!hasAnyChip);
    }
}
