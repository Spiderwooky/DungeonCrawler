using UnityEngine;

// Affiche les visuels 3D des équipements dans les mains du joueur.
// S'abonne à Inventory.OnInventoryChanged pour mettre à jour les instances
// dès qu'un item est équipé, déséquipé ou cassé.
public class PlayerEquipmentVisuals : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [Tooltip("Bone de la main droite du modèle joueur (main portant l'arme).")]
    [SerializeField] private Transform weaponBone;
    [Tooltip("Bone de la main gauche ou de l'avant-bras gauche (main portant le bouclier).")]
    [SerializeField] private Transform shieldBone;

    private GameObject currentWeaponInstance;
    private GameObject currentShieldInstance;
    private EquipmentItemData currentWeaponData;
    private EquipmentItemData currentShieldData;

    private void Start()
    {
        if (inventory == null)
            inventory = GetComponent<Inventory>();

        if (inventory != null)
        {
            inventory.OnInventoryChanged += RefreshVisuals;
            RefreshVisuals();
        }
        else
        {
            Debug.LogWarning("[PlayerEquipmentVisuals] Inventory introuvable.");
        }
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= RefreshVisuals;
    }

    private void RefreshVisuals()
    {
        EquipmentItemData weaponData = null;
        EquipmentItemData shieldData = null;

        InventorySlot[] slots = inventory.GetSlots(InventoryZone.Equipment);
        if (slots != null)
        {
            foreach (InventorySlot slot in slots)
            {
                if (slot.IsEmpty || !(slot.item is EquipmentItemData eq)) continue;
                if (eq.equipmentType == EquipmentType.Weapon && weaponData == null)
                    weaponData = eq;
                else if (eq.equipmentType == EquipmentType.Shield && shieldData == null)
                    shieldData = eq;
            }
        }

        UpdateVisual(ref currentWeaponInstance, ref currentWeaponData, weaponData, weaponBone);
        UpdateVisual(ref currentShieldInstance, ref currentShieldData, shieldData, shieldBone);
    }

    private static void UpdateVisual(
        ref GameObject instance,
        ref EquipmentItemData current,
        EquipmentItemData next,
        Transform bone)
    {
        if (next == current) return;

        if (instance != null)
        {
            Destroy(instance);
            instance = null;
        }

        current = next;

        if (next == null || next.visualPrefab == null || bone == null) return;

        instance = Instantiate(next.visualPrefab, bone);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale    = Vector3.one;
    }
}
