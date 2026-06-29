using UnityEngine;

/// <summary>
/// Objet équipable (armes, etc.) : s'équipe dans le slot d'équipement au lieu de se consommer.
/// Créer via : clic droit > Create > Items > Equipment Item
/// </summary>
[CreateAssetMenu(fileName = "NewEquipmentItem", menuName = "Items/Equipment Item")]
public class EquipmentItemData : ItemData
{
    [Header("Effet")]
    [Tooltip("Bonus de dégâts d'attaque ajouté tant que cet objet est équipé.")]
    public int attackBonus = 0;

    public override bool OnUse(GameObject user, Inventory inventory, InventoryZone fromZone, int fromIndex)
    {
        inventory.Equip(fromZone, fromIndex);
        return false;
    }
}
