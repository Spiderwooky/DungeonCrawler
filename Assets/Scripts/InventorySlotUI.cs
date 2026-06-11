using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Un slot d'UI (hotbar ou sac) : clic, drag & drop.
/// </summary>
[RequireComponent(typeof(Button))]
public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private Text amountText;
    [SerializeField] private Text keyLabel;
    [SerializeField] private Image pickHighlight;
    [SerializeField] private Image selectionHighlight;

    private InventoryUI inventoryUI;
    private InventoryZone zone;
    private int slotIndex;
    private CanvasGroup canvasGroup;

    public InventoryZone Zone => zone;
    public int SlotIndex => slotIndex;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    public void Initialize(InventoryUI ui, InventoryZone slotZone, int index)
    {
        inventoryUI = ui;
        zone = slotZone;
        slotIndex = index;
        gameObject.name = $"Slot_{slotZone}_{index}";
    }

    public void OnClick()
    {
        inventoryUI?.HandleSlotClick(zone, slotIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanDrag()) return;
        inventoryUI?.BeginSlotDrag(zone, slotIndex, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        inventoryUI?.UpdateSlotDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        inventoryUI?.EndSlotDrag(eventData);
    }

    public void SetRaycastBlocked(bool blocked)
    {
        canvasGroup.blocksRaycasts = blocked;
    }

    private bool CanDrag()
    {
        if (inventoryUI == null) return false;
        InventorySlot slot = inventoryUI.GetInventorySlot(zone, slotIndex);
        if (slot == null || slot.IsEmpty) return false;
        if (zone == InventoryZone.Backpack && !inventoryUI.IsOpen) return false;
        return true;
    }

    public void Refresh(
        InventorySlot slot,
        bool isHotbarSelected,
        bool isPicked,
        string hotbarKeyLabel,
        bool hideIconWhileDragging)
    {
        if (background != null)
            background.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);

        if (selectionHighlight != null)
            selectionHighlight.gameObject.SetActive(isHotbarSelected);

        if (pickHighlight != null)
            pickHighlight.gameObject.SetActive(isPicked);

        if (keyLabel != null)
            keyLabel.text = hotbarKeyLabel ?? string.Empty;

        if (slot == null || slot.IsEmpty || hideIconWhileDragging)
        {
            if (icon != null) icon.enabled = false;
            if (amountText != null) amountText.text = string.Empty;
            if (slot == null || slot.IsEmpty)
            {
                if (keyLabel != null && zone == InventoryZone.Backpack)
                    keyLabel.text = string.Empty;
            }
            return;
        }

        if (icon != null)
        {
            if (slot.item.icon != null)
            {
                icon.sprite = slot.item.icon;
                icon.enabled = true;
            }
            else
            {
                icon.enabled = false;
            }
        }

        if (amountText != null)
            amountText.text = slot.amount > 1 ? slot.amount.ToString() : string.Empty;

        if (keyLabel != null && zone == InventoryZone.Backpack && slot.item.icon == null)
        {
            keyLabel.text = slot.item.itemName.Length > 0
                ? slot.item.itemName.Substring(0, Mathf.Min(3, slot.item.itemName.Length))
                : "?";
        }
    }
}
