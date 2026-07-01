using UnityEngine;

/// <summary>
/// Objet équipable (armes, etc.) : s'équipe dans le slot d'équipement au lieu de se consommer.
/// Créer via : clic droit > Create > Items > Equipment Item
/// </summary>
[CreateAssetMenu(fileName = "NewEquipmentItem", menuName = "Items/Equipment Item")]
public class EquipmentItemData : ItemData
{
    [Header("Type")]
    [Tooltip("Catégorie de l'équipement. Contrôle dans quel slot il peut être équipé et combien du même type peuvent l'être simultanément (configuré dans Inventory).")]
    public EquipmentType equipmentType = EquipmentType.Weapon;

    [Header("Durabilité")]
    [Tooltip("Durabilité maximale. L'item se casse quand elle atteint 0. Mettre à 0 pour un item indestructible.")]
    public int maxDurability = 10;

    [Header("Visuel en main")]
    [Tooltip("Prefab 3D instancié dans la main du joueur quand cet item est équipé. Laisser vide pour aucun visuel.")]
    public GameObject visualPrefab;

    [Header("Effet")]
    [Tooltip("Bonus de dégâts d'attaque ajouté tant que cet objet est équipé.")]
    public int attackBonus  = 0;
    [Tooltip("Réduction de dégâts reçus (flat) tant que cet objet est équipé. Au moins 1 dégât passe toujours.")]
    public int defenseBonus = 0;

    public override bool OnUse(GameObject user, Inventory inventory, InventoryZone fromZone, int fromIndex)
    {
        inventory.Equip(fromZone, fromIndex);
        return false;
    }
}
