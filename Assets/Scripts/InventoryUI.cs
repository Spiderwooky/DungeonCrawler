using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// UI d'inventaire : slots générés depuis un prefab selon les compteurs Inspector.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private TextMeshProUGUI hintText;

    [Header("Nombre de slots (Inspector)")]
    [SerializeField] private int hotbarSlotCount = 5;
    [SerializeField] private int backpackSlotCount = 16;
    [SerializeField] private int backpackColumns = 8;

    [Header("Génération des slots")]
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private RectTransform hotbarContainer;
    [SerializeField] private RectTransform backpackContainer;

    private readonly List<InventorySlotUI> hotbarSlots = new List<InventorySlotUI>();
    private readonly List<InventorySlotUI> backpackSlots = new List<InventorySlotUI>();

    private bool isOpen;
    private bool isDragging;
    private InventoryZone dragZone;
    private int dragIndex;
    private InventorySlotUI dragSourceSlot;
    private Image dragGhost;

    private void Awake()
    {
        if (inventory == null)
            inventory = FindFirstObjectByType<Inventory>();

        if (slotPrefab == null || hotbarContainer == null || backpackContainer == null)
        {
            Debug.LogError("[InventoryUI] Assignez Slot Prefab, Hotbar Container et Backpack Container.");
            return;
        }

        BuildSlots();
        CreateDragGhost();

        if (inventory != null)
            inventory.OnInventoryChanged += Refresh;

        SetInventoryOpen(false);
        Refresh();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= Refresh;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        hotbarSlotCount = Mathf.Max(1, hotbarSlotCount);
        backpackSlotCount = Mathf.Max(1, backpackSlotCount);
        backpackColumns = Mathf.Max(1, backpackColumns);
    }

    [ContextMenu("Rebuild Slot UI")]
    private void RebuildSlotsInEditor()
    {
        if (slotPrefab == null || hotbarContainer == null || backpackContainer == null) return;
        BuildSlots();
    }
#endif

    private void BuildSlots()
    {
        ClearContainer(hotbarContainer);
        ClearContainer(backpackContainer);
        hotbarSlots.Clear();
        backpackSlots.Clear();

        GridLayoutGroup grid = backpackContainer.GetComponent<GridLayoutGroup>();
        if (grid != null)
            grid.constraintCount = backpackColumns;

        for (int i = 0; i < hotbarSlotCount; i++)
            hotbarSlots.Add(CreateSlot(hotbarContainer, InventoryZone.Hotbar, i));

        for (int i = 0; i < backpackSlotCount; i++)
            backpackSlots.Add(CreateSlot(backpackContainer, InventoryZone.Backpack, i));

        if (inventory != null)
            inventory.Configure(hotbarSlotCount, backpackSlotCount);
    }

    private InventorySlotUI CreateSlot(Transform parent, InventoryZone zone, int index)
    {
        InventorySlotUI slot = Instantiate(slotPrefab, parent);
        slot.Initialize(this, zone, index);
        return slot;
    }

    private void ClearContainer(Transform container)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child.gameObject);
            else
#endif
                Destroy(child.gameObject);
        }
    }

    private void CreateDragGhost()
    {
        GameObject ghostGo = new GameObject("DragGhost", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        ghostGo.transform.SetParent(transform, false);

        RectTransform rect = ghostGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(48f, 48f);

        dragGhost = ghostGo.GetComponent<Image>();
        dragGhost.preserveAspect = true;
        dragGhost.raycastTarget = false;

        CanvasGroup cg = ghostGo.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        ghostGo.SetActive(false);
    }

    public bool IsOpen => isOpen;

    public InventorySlot GetInventorySlot(InventoryZone zone, int index) =>
        inventory?.GetSlot(zone, index);

    public void ToggleInventory()
    {
        SetInventoryOpen(!isOpen);
    }

    public void SetInventoryOpen(bool open)
    {
        isOpen = open;
        if (inventoryPanel != null)
            inventoryPanel.SetActive(open);

        if (!isOpen && inventory != null)
            inventory.ClearPick();

        Refresh();
    }

    public void HandleSlotClick(InventoryZone zone, int index)
    {
        if (inventory == null || isDragging) return;

        if (zone == InventoryZone.Backpack && !isOpen)
            return;

        if (zone == InventoryZone.Hotbar)
            inventory.SelectHotbar(index);

        if (!isOpen)
        {
            Refresh();
            return;
        }

        inventory.HandleMoveClick(zone, index);
    }

    public void BeginSlotDrag(InventoryZone zone, int index, PointerEventData eventData)
    {
        InventorySlot slot = inventory?.GetSlot(zone, index);
        if (slot == null || slot.IsEmpty) return;
        if (zone == InventoryZone.Backpack && !isOpen) return;

        isDragging = true;
        dragZone = zone;
        dragIndex = index;
        dragSourceSlot = FindSlotUI(zone, index);

        inventory?.ClearPick();

        if (dragGhost != null)
        {
            if (slot.item.icon != null)
            {
                dragGhost.sprite = slot.item.icon;
                dragGhost.enabled = true;
            }
            else
            {
                dragGhost.sprite = null;
                dragGhost.enabled = false;
            }
            dragGhost.gameObject.SetActive(true);
        }

        if (dragSourceSlot != null)
            dragSourceSlot.SetRaycastBlocked(false);

        UpdateSlotDrag(eventData);
        Refresh();
    }

    public void UpdateSlotDrag(PointerEventData eventData)
    {
        if (!isDragging || dragGhost == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint);

        dragGhost.rectTransform.anchoredPosition = localPoint;
    }

    public void EndSlotDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        InventorySlotUI target = RaycastSlot(eventData);
        if (target != null && inventory != null
            && !(target.Zone == dragZone && target.SlotIndex == dragIndex))
        {
            inventory.MoveSlots(dragZone, dragIndex, target.Zone, target.SlotIndex);
        }

        if (dragSourceSlot != null)
            dragSourceSlot.SetRaycastBlocked(true);

        dragSourceSlot = null;
        isDragging = false;

        if (dragGhost != null)
            dragGhost.gameObject.SetActive(false);

        Refresh();
    }

    private InventorySlotUI RaycastSlot(PointerEventData eventData)
    {
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (RaycastResult result in results)
        {
            InventorySlotUI slot = result.gameObject.GetComponentInParent<InventorySlotUI>();
            if (slot == null) continue;
            if (slot.Zone == InventoryZone.Backpack && !isOpen) continue;
            return slot;
        }

        return null;
    }

    private InventorySlotUI FindSlotUI(InventoryZone zone, int index)
    {
        List<InventorySlotUI> list = zone == InventoryZone.Hotbar ? hotbarSlots : backpackSlots;
        foreach (InventorySlotUI slot in list)
        {
            if (slot != null && slot.Zone == zone && slot.SlotIndex == index)
                return slot;
        }
        return null;
    }

    private void Refresh()
    {
        if (inventory == null) return;

        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            if (hotbarSlots[i] == null) continue;

            bool selected = i == inventory.SelectedHotbarIndex;
            bool picked = inventory.HasPick
                && inventory.PickedZone == InventoryZone.Hotbar
                && inventory.PickedIndex == i;
            bool hideIcon = isDragging && dragZone == InventoryZone.Hotbar && dragIndex == i;

            hotbarSlots[i].Refresh(
                inventory.GetSlot(InventoryZone.Hotbar, i),
                selected,
                picked,
                (i + 1).ToString(),
                hideIcon);
        }

        if (!isOpen) return;

        for (int i = 0; i < backpackSlots.Count; i++)
        {
            if (backpackSlots[i] == null) continue;

            bool picked = inventory.HasPick
                && inventory.PickedZone == InventoryZone.Backpack
                && inventory.PickedIndex == i;
            bool hideIcon = isDragging && dragZone == InventoryZone.Backpack && dragIndex == i;

            backpackSlots[i].Refresh(
                inventory.GetSlot(InventoryZone.Backpack, i),
                false,
                picked,
                null,
                hideIcon);
        }

        if (hintText != null)
        {
            hintText.text = inventory.HasPick
                ? "Clic ou glisser-déposer  |  G pour jeter (hotbar)  |  I pour fermer"
                : "Clic / drag & drop  |  G : jeter le slot hotbar actif  |  I : fermer";
        }
    }
}
