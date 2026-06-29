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
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        if (hotbarSlots == null || backpackSlots == null)
            Configure(5, 16);
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

    // Ajoute `amount` exemplaires de `item` (empile sur les stacks existants, puis remplit
    // les slots vides). Renvoie true seulement si la totalité a pu être ajoutée — un ajout
    // partiel (inventaire presque plein) renvoie false mais déclenche quand même
    // OnInventoryChanged, car l'état a bien changé.
    public bool AddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;

        int remaining = amount;
        remaining = TryStack(InventoryZone.Hotbar, item, remaining);
        remaining = TryStack(InventoryZone.Backpack, item, remaining);
        remaining = TryFillEmpty(InventoryZone.Hotbar, item, remaining);
        remaining = TryFillEmpty(InventoryZone.Backpack, item, remaining);

        if (remaining == amount) return false; // rien n'a pu être ajouté

        OnInventoryChanged?.Invoke();
        return remaining == 0;
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

    // Compte le nombre total d'exemplaires de `item` détenus (hotbar + sac).
    public int CountItem(ItemData item)
    {
        if (item == null) return 0;
        return CountInZone(InventoryZone.Hotbar, item) + CountInZone(InventoryZone.Backpack, item);
    }

    private int CountInZone(InventoryZone zone, ItemData item)
    {
        InventorySlot[] slots = GetSlots(zone);
        int total = 0;
        for (int i = 0; i < slots.Length; i++)
            if (!slots[i].IsEmpty && slots[i].item == item) total += slots[i].amount;
        return total;
    }

    // Retire `amount` exemplaires de `item` (sac d'abord, puis hotbar). Si le joueur n'en
    // possède pas assez, ne retire rien et renvoie false (transaction tout-ou-rien).
    public bool RemoveItem(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return false;
        if (CountItem(item) < amount) return false;

        int remaining = amount;
        remaining = RemoveFromZone(InventoryZone.Backpack, item, remaining);
        remaining = RemoveFromZone(InventoryZone.Hotbar, item, remaining);

        OnInventoryChanged?.Invoke();
        return remaining == 0;
    }

    private int RemoveFromZone(InventoryZone zone, ItemData item, int remaining)
    {
        InventorySlot[] slots = GetSlots(zone);
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i].IsEmpty || slots[i].item != item) continue;
            int removed = Mathf.Min(slots[i].amount, remaining);
            slots[i].amount -= removed;
            remaining -= removed;
            if (slots[i].amount <= 0) slots[i].Clear();
        }
        return remaining;
    }

    public bool DropFromHotbar(int hotbarIndex)
    {
        if (gameManager == null) return false;
        if (hotbarIndex < 0 || hotbarIndex >= HotbarSlotCount) return false;

        InventorySlot slot = hotbarSlots[hotbarIndex];
        if (slot.IsEmpty) return false;

        Vector2Int grid = GridUtils.WorldToGrid(transform.position, gameManager.GetStep());
        if (PickupManager.Instance != null && PickupManager.Instance.HasPickupAt(grid))
            return false;

        ItemData item = slot.item;
        const int dropAmount = 1;
        slot.amount -= dropAmount;
        if (slot.amount <= 0) slot.Clear();

        Vector3 worldPos = GridUtils.GridToWorld(grid, gameManager.GetStep());
        WorldPickup.Spawn(item, dropAmount, worldPos);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool DropSelectedItem() => DropFromHotbar(selectedHotbarIndex);
}
