using System;
using UnityEngine;

public enum InventoryZone
{
    Hotbar,
    Backpack
}

[Serializable]
public class InventorySlot
{
    public ItemData item;
    public int amount;

    public bool IsEmpty => item == null || amount <= 0;

    public void Clear()
    {
        item = null;
        amount = 0;
    }

    public void CopyFrom(InventorySlot other)
    {
        item = other.item;
        amount = other.amount;
    }
}

/// <summary>
/// Inventaire : hotbar et sac séparés, déplacement par clic (source puis destination).
/// </summary>
public class Inventory : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private GameManager gameManager;

    [Header("Audio")]
    [SerializeField] private AudioClip pickupClip;
    [SerializeField] private AudioClip dropClip;

    private AudioSource audioSource;
    private InventorySlot[] hotbarSlots;
    private InventorySlot[] backpackSlots;

    private int selectedHotbarIndex;
    private bool hasPick;
    private InventoryZone pickedZone;
    private int pickedIndex;

    public event Action OnInventoryChanged;

    public int HotbarSlotCount => hotbarSlots?.Length ?? 0;
    public int BackpackSlotCount => backpackSlots?.Length ?? 0;
    public int SelectedHotbarIndex => selectedHotbarIndex;
    public bool HasPick => hasPick;
    public InventoryZone PickedZone => pickedZone;
    public int PickedIndex => pickedIndex;

    public void Configure(int hotbarCount, int backpackCount)
    {
        hotbarCount = Mathf.Max(1, hotbarCount);
        backpackCount = Mathf.Max(1, backpackCount);

        hotbarSlots = CreateSlots(hotbarCount);
        backpackSlots = CreateSlots(backpackCount);
        ClearPick();
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        if (hotbarSlots == null || backpackSlots == null)
            Configure(5, 16);
    }

    public bool TryCollectAt(Vector2Int grid)
    {
        if (PickupManager.Instance == null) return false;
        if (!PickupManager.Instance.TryCollectAt(grid, this)) return false;

        PlayClip(pickupClip);
        return true;
    }

    private static InventorySlot[] CreateSlots(int count)
    {
        var slots = new InventorySlot[count];
        for (int i = 0; i < count; i++)
            slots[i] = new InventorySlot();
        return slots;
    }

    public InventorySlot GetSlot(InventoryZone zone, int index)
    {
        InventorySlot[] slots = GetSlots(zone);
        if (slots == null || index < 0 || index >= slots.Length) return null;
        return slots[index];
    }

    public InventorySlot[] GetSlots(InventoryZone zone) =>
        zone == InventoryZone.Hotbar ? hotbarSlots : backpackSlots;

    public void SelectHotbar(int index)
    {
        if (index < 0 || index >= HotbarSlotCount) return;
        selectedHotbarIndex = index;
        OnInventoryChanged?.Invoke();
    }

    public void HandleMoveClick(InventoryZone zone, int index)
    {
        if (GetSlot(zone, index) == null) return;

        if (!hasPick)
        {
            if (GetSlot(zone, index).IsEmpty) return;

            hasPick = true;
            pickedZone = zone;
            pickedIndex = index;
            OnInventoryChanged?.Invoke();
            return;
        }

        if (pickedZone == zone && pickedIndex == index)
        {
            ClearPick();
            OnInventoryChanged?.Invoke();
            return;
        }

        MoveOrSwap(pickedZone, pickedIndex, zone, index);
        ClearPick();
        OnInventoryChanged?.Invoke();
    }

    public void ClearPick()
    {
        hasPick = false;
        pickedIndex = -1;
    }

    public void MoveSlots(InventoryZone fromZone, int fromIndex, InventoryZone toZone, int toIndex)
    {
        MoveOrSwap(fromZone, fromIndex, toZone, toIndex);
        ClearPick();
        OnInventoryChanged?.Invoke();
    }

    private void MoveOrSwap(InventoryZone fromZone, int fromIndex, InventoryZone toZone, int toIndex)
    {
        InventorySlot from = GetSlot(fromZone, fromIndex);
        InventorySlot to = GetSlot(toZone, toIndex);
        if (from == null || to == null || from.IsEmpty) return;

        if (!to.IsEmpty && to.item == from.item)
        {
            int space = from.item.maxStack - to.amount;
            if (space > 0)
            {
                int moved = Mathf.Min(space, from.amount);
                to.amount += moved;
                from.amount -= moved;
                if (from.amount <= 0) from.Clear();
            }
            return;
        }

        if (to.IsEmpty)
        {
            to.CopyFrom(from);
            from.Clear();
            return;
        }

        ItemData tempItem = to.item;
        int tempAmount = to.amount;
        to.CopyFrom(from);
        from.item = tempItem;
        from.amount = tempAmount;
    }

    public bool AddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;

        int remaining = amount;
        remaining = TryStack(InventoryZone.Hotbar, item, remaining);
        remaining = TryStack(InventoryZone.Backpack, item, remaining);
        remaining = TryFillEmpty(InventoryZone.Hotbar, item, remaining);
        remaining = TryFillEmpty(InventoryZone.Backpack, item, remaining);

        if (remaining < amount)
        {
            OnInventoryChanged?.Invoke();
            return remaining == 0;
        }

        return false;
    }

    private int TryStack(InventoryZone zone, ItemData item, int remaining)
    {
        InventorySlot[] slots = GetSlots(zone);
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i].item != item) continue;
            int space = item.maxStack - slots[i].amount;
            if (space <= 0) continue;
            int added = Mathf.Min(space, remaining);
            slots[i].amount += added;
            remaining -= added;
        }
        return remaining;
    }

    private int TryFillEmpty(InventoryZone zone, ItemData item, int remaining)
    {
        InventorySlot[] slots = GetSlots(zone);
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (!slots[i].IsEmpty) continue;
            int added = Mathf.Min(item.maxStack, remaining);
            slots[i].item = item;
            slots[i].amount = added;
            remaining -= added;
        }
        return remaining;
    }

    public bool DropFromHotbar(int hotbarIndex)
    {
        if (gameManager == null) return false;
        if (hotbarIndex < 0 || hotbarIndex >= HotbarSlotCount) return false;

        InventorySlot slot = hotbarSlots[hotbarIndex];
        if (slot.IsEmpty) return false;

        Vector2Int grid = WorldToGrid(transform.position);
        if (PickupManager.Instance != null && PickupManager.Instance.HasPickupAt(grid))
            return false;

        ItemData item = slot.item;
        int dropAmount = 1;
        slot.amount -= dropAmount;
        if (slot.amount <= 0) slot.Clear();

        float step = gameManager.GetStep();
        Vector3 worldPos = new Vector3(grid.x * step, 0f, grid.y * step);
        WorldPickup.Spawn(item, dropAmount, worldPos);

        PlayClip(dropClip);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool DropSelectedItem() => DropFromHotbar(selectedHotbarIndex);

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        float step = gameManager != null ? gameManager.GetStep() : 5f;
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x / step),
            Mathf.RoundToInt(worldPos.z / step)
        );
    }
}
