using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Un slot d'UI (hotbar ou sac) : clic, drag & drop.
/// </summary>
[RequireComponent(typeof(Button))]
public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI keyLabel;
    [SerializeField] private Image pickHighlight;
    [SerializeField] private Image selectionHighlight;

    [Header("Durabilité")]
    [SerializeField] private GameObject durabilityBarRoot;
    [SerializeField] private Image durabilityBarFill;

    private static readonly Color DuraColorFull   = new Color(0.20f, 0.75f, 0.20f); // vert
    private static readonly Color DuraColorMedium = new Color(0.95f, 0.60f, 0.10f); // orange
    private static readonly Color DuraColorLow    = new Color(0.85f, 0.15f, 0.15f); // rouge

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
        if ((zone == InventoryZone.Backpack || zone == InventoryZone.Equipment) && !inventoryUI.IsOpen) return false;
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
            background.color = new Color(1f, 1f, 1f, 1f);

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
            if (durabilityBarRoot != null) durabilityBarRoot.SetActive(false);
            if (slot == null || slot.IsEmpty)
            {
                if (keyLabel != null && (zone == InventoryZone.Backpack || zone == InventoryZone.Equipment))
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

        if (keyLabel != null && (zone == InventoryZone.Backpack || zone == InventoryZone.Equipment) && slot.item.icon == null)
        {
            keyLabel.text = slot.item.itemName.Length > 0
                ? slot.item.itemName.Substring(0, Mathf.Min(3, slot.item.itemName.Length))
                : "?";
        }

        // Barre de durabilité : visible seulement si l'item a une durabilité < son max
        if (durabilityBarRoot != null)
        {
            bool showBar = slot.item is EquipmentItemData eq
                           && eq.maxDurability > 0
                           && slot.currentDurability < eq.maxDurability;
            durabilityBarRoot.SetActive(showBar);

            if (showBar && durabilityBarFill != null)
            {
                float ratio = (float)slot.currentDurability / ((EquipmentItemData)slot.item).maxDurability;
                durabilityBarFill.fillAmount = ratio;
                durabilityBarFill.color = ratio > 0.6f ? DuraColorFull
                                        : ratio > 0.3f ? DuraColorMedium
                                        : DuraColorLow;
            }
        }
    }
}
