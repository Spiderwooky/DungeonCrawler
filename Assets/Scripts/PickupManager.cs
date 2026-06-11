using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registre des objets au sol, indexés par case de grille.
/// </summary>
public class PickupManager : MonoBehaviour
{
    public static PickupManager Instance { get; private set; }

    private readonly Dictionary<Vector2Int, WorldPickup> pickups = new Dictionary<Vector2Int, WorldPickup>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Register(WorldPickup pickup, Vector2Int grid)
    {
        if (pickup == null) return;

        if (pickups.TryGetValue(grid, out WorldPickup existing) && existing != null && existing != pickup)
            Debug.LogWarning($"[PickupManager] Case {grid} déjà occupée par un objet.");

        pickups[grid] = pickup;
    }

    public void Unregister(Vector2Int grid, WorldPickup pickup)
    {
        if (pickups.TryGetValue(grid, out WorldPickup existing) && existing == pickup)
            pickups.Remove(grid);
    }

    public bool HasPickupAt(Vector2Int grid) =>
        pickups.TryGetValue(grid, out WorldPickup p) && p != null;

    public bool TryCollectAt(Vector2Int grid, Inventory inventory)
    {
        if (inventory == null) return false;
        if (!pickups.TryGetValue(grid, out WorldPickup pickup) || pickup == null)
            return false;

        if (!inventory.AddItem(pickup.ItemData, pickup.Amount))
            return false;

        pickup.Collect();
        return true;
    }
}
