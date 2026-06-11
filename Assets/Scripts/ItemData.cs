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

    [Header("Monde")]
    [Tooltip("Prefab instancié quand l'objet est jeté au sol. Si vide, un cube par défaut est utilisé.")]
    public GameObject worldPickupPrefab;
}
