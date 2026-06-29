using UnityEngine;

/// <summary>
/// Définition d'un type d'objet ramassable.
/// Créer via : clic droit > Create > Items > Item Data
/// </summary>
[CreateAssetMenu(fileName = "NewItemData", menuName = "Items/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Identité")]
    public string itemName = "Objet";
    [TextArea(2, 4)]
    public string description = "";

    [Header("Affichage")]
    public Sprite icon;

    [Header("Inventaire")]
    [Min(1)] public int maxStack = 1;

    [Header("Économie")]
    [Tooltip("Valeur marchande de référence (en pièces). Utilisée par les marchands pour calculer leurs prix d'achat/vente. 0 = objet sans valeur marchande (les marchands ne l'achètent pas).")]
    [Min(0)] public int baseValue = 0;

    [Header("Monde")]
    [Tooltip("Prefab instancié quand l'objet est jeté au sol. Si vide, un cube par défaut est utilisé.")]
    public GameObject worldPickupPrefab;

    /// <summary>
    /// Appelé quand le joueur utilise cet objet depuis le slot hotbar sélectionné (action "Use").
    /// Retourne true si l'objet doit être consommé (1 exemplaire retiré du stack).
    /// </summary>
    public virtual bool OnUse(GameObject user, Inventory inventory, InventoryZone fromZone, int fromIndex)
    {
        return false;
    }
}
