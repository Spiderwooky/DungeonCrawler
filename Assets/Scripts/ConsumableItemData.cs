using UnityEngine;

/// <summary>
/// Objet consommable (potions, etc.) : soigne le joueur et se consomme à l'utilisation.
/// Créer via : clic droit > Create > Items > Consumable Item
/// </summary>
[CreateAssetMenu(fileName = "NewConsumableItem", menuName = "Items/Consumable Item")]
public class ConsumableItemData : ItemData
{
    [Header("Effet")]
    [Tooltip("Points de vie rendus au joueur à l'utilisation.")]
    public int healAmount = 0;

    public override bool OnUse(GameObject user, Inventory inventory, InventoryZone fromZone, int fromIndex)
    {
        HealthSystem health = user.GetComponent<HealthSystem>();
        if (health == null) return false;

        health.Heal(healAmount);
        return true;
    }
}
