using System;
using UnityEngine;

public enum InventoryZone
{
    Hotbar,
    Backpack,
    Equipment
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
    private InventorySlot[] equipmentSlots;

    private int selectedHotbarIndex;
    private bool hasPick;
    private InventoryZone pickedZone;
    private int pickedIndex;

    public event Action OnInventoryChanged;

    public int HotbarSlotCount => hotbarSlots?.Length ?? 0;
    public int BackpackSlotCount => backpackSlots?.Length ?? 0;
    public int EquipmentSlotCount => equipmentSlots?.Length ?? 0;
    public int SelectedHotbarIndex => selectedHotbarIndex;
    public bool HasPick => hasPick;
    public InventoryZone PickedZone => pickedZone;
    public int PickedIndex => pickedIndex;

    public void Configure(int hotbarCount, int backpackCount, int equipmentCount = 1)
    {
        hotbarCount = Mathf.Max(1, hotbarCount);
        backpackCount = Mathf.Max(1, backpackCount);
        equipmentCount = Mathf.Max(1, equipmentCount);

        hotbarSlots = CreateSlots(hotbarCount);
        backpackSlots = CreateSlots(backpackCount);
        equipmentSlots = CreateSlots(equipmentCount);
        ClearPick();
    }

    private void Awake()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        if (hotbarSlots == null || backpackSlots == null)
            Configure(5, 16, 1);
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

    public InventorySlot[] GetSlots(InventoryZone zone)
    {
        switch (zone)
        {
            case InventoryZone.Hotbar: return hotbarSlots;
            case InventoryZone.Equipment: return equipmentSlots;
            default: return backpackSlots;
        }
    }

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

        // Le slot d'équipement n'accepte que les objets équipables.
        if (toZone == InventoryZone.Equipment && !(from.item is EquipmentItemData)) return;

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

    // Détermine quel slot vise une action "rapide" (G pour jeter, F pour utiliser/équiper) :
    // l'objet actuellement épinglé (cliqué) dans le sac s'il y en a un, sinon le slot hotbar
    // sélectionné. Permet de jeter/utiliser un objet du sac sans devoir le déplacer en hotbar.
    private (InventoryZone zone, int index) ResolveActiveSlot()
    {
        if (hasPick && pickedZone == InventoryZone.Backpack)
            return (InventoryZone.Backpack, pickedIndex);

        return (InventoryZone.Hotbar, selectedHotbarIndex);
    }

    public bool DropSelectedItem()
    {
        (InventoryZone zone, int index) = ResolveActiveSlot();
        return DropFromSlot(zone, index);
    }

    private bool DropFromSlot(InventoryZone zone, int index)
    {
        if (gameManager == null) return false;

        InventorySlot slot = GetSlot(zone, index);
        if (slot == null || slot.IsEmpty) return false;

        Vector2Int grid = GridUtils.WorldToGrid(transform.position, gameManager.GetStep());
        if (PickupManager.Instance != null && PickupManager.Instance.HasPickupAt(grid))
            return false;

        ItemData item = slot.item;
        const int dropAmount = 1;
        slot.amount -= dropAmount;
        if (slot.amount <= 0) slot.Clear();

        if (hasPick && pickedZone == zone && pickedIndex == index)
            ClearPick();

        Vector3 worldPos = GridUtils.GridToWorld(grid, gameManager.GetStep());
        WorldPickup.Spawn(item, dropAmount, worldPos, launchFrom: transform.position);
        OnInventoryChanged?.Invoke();
        return true;
    }

    // Équipe l'objet équipable situé à (fromZone, fromIndex) : prend le premier slot
    // d'équipement libre, ou échange avec le premier slot équipé si tous sont occupés.
    public void Equip(InventoryZone fromZone, int fromIndex)
    {
        if (equipmentSlots == null || equipmentSlots.Length == 0) return;

        for (int i = 0; i < equipmentSlots.Length; i++)
        {
            if (equipmentSlots[i].IsEmpty)
            {
                MoveOrSwap(fromZone, fromIndex, InventoryZone.Equipment, i);
                return;
            }
        }

        MoveOrSwap(fromZone, fromIndex, InventoryZone.Equipment, 0);
    }

    // Somme des bonus d'attaque de tout ce qui est actuellement équipé.
    public int GetEquipmentAttackBonus()
    {
        if (equipmentSlots == null) return 0;

        int total = 0;
        foreach (InventorySlot slot in equipmentSlots)
        {
            if (!slot.IsEmpty && slot.item is EquipmentItemData equipment)
                total += equipment.attackBonus;
        }
        return total;
    }

    // Utilise l'objet visé par ResolveActiveSlot (potion → soin + consommation, équipement →
    // s'équipe). Renvoie false si le slot est vide (rien à utiliser, pas d'action).
    public bool UseSelectedItem(GameObject user)
    {
        (InventoryZone zone, int index) = ResolveActiveSlot();
        InventorySlot slot = GetSlot(zone, index);
        if (slot == null || slot.IsEmpty) return false;

        bool consumed = slot.item.OnUse(user, this, zone, index);
        if (consumed)
        {
            slot.amount -= 1;
            if (slot.amount <= 0) slot.Clear();
        }

        if (hasPick && pickedZone == zone && pickedIndex == index)
            ClearPick();

        OnInventoryChanged?.Invoke();
        return true;
    }
}
