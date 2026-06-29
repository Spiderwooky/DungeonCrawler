using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Une ligne d'objet dans la liste "elle vend" ou "elle achète" du dialogue marchand.
/// Instanciée dynamiquement par MerchantDialogueUI (même principe que InventorySlotUI).
/// </summary>
[RequireComponent(typeof(Button))]
public class MerchantShopRowUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI actionButtonLabel;

    public void Setup(ItemData item, int price, int quantity, string buttonLabel, System.Action onClick)
    {
        if (icon != null)
        {
            icon.sprite = item.icon;
            icon.enabled = item.icon != null;
        }

        if (nameText != null) nameText.text = item.itemName;
        if (priceText != null) priceText.text = $"{price} or";
        if (quantityText != null) quantityText.text = quantity < 0 ? "" : $"x{quantity}";
        if (actionButtonLabel != null) actionButtonLabel.text = buttonLabel;

        Button button = actionButton != null ? actionButton : GetComponent<Button>();
        button.onClick.RemoveAllListeners();

        bool canAct = quantity != 0; // 0 = rupture de stock / plus rien à vendre
        button.interactable = canAct;
        if (canAct)
            button.onClick.AddListener(() => onClick?.Invoke());
    }
}
