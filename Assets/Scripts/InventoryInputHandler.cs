using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Entrées inventaire — méthodes publiques à câbler dans le PlayerInput (Inspector).
/// </summary>
public class InventoryInputHandler : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private InventoryUI inventoryUI;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<Inventory>();
    }

    public void OnToggleInventory(InputAction.CallbackContext context)
    {
        if (!context.performed || inventoryUI == null) return;
        inventoryUI.ToggleInventory();
    }

    public void OnDropItem(InputAction.CallbackContext context)
    {
        if (!context.performed || inventory == null) return;

        if (!inventory.DropSelectedItem())
            Debug.Log("[Inventaire] Impossible de jeter l'objet (slot hotbar vide ou case occupée).");
    }

    public void OnHotbarSelect(InputAction.CallbackContext context)
    {
        if (!context.performed || inventory == null) return;

        string key = context.control.name;
        if (!int.TryParse(key, out int number)) return;

        inventory.SelectHotbar(number - 1);
    }
}
