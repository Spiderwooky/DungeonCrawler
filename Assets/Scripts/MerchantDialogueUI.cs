using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI du dialogue marchand : journal de discussion, saisie libre (mode IA) ou boutons
/// d'action classiques (mode hors-ligne), et listes d'achat/vente.
///
/// En mode IA, cliquer une ligne d'objet ne déclenche PAS d'achat/vente directement :
/// ça pré-remplit le champ de texte avec une proposition que le joueur peut éditer avant
/// d'envoyer (la négociation passe par le chat). En mode hors-ligne, cliquer exécute la
/// transaction immédiatement au prix affiché, comme dans une boutique classique.
/// </summary>
public class MerchantDialogueUI : MonoBehaviour
{
    [Header("Panneau")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI merchantNameText;
    [SerializeField] private Button closeButton;

    [Header("Journal de discussion")]
    [SerializeField] private TextMeshProUGUI chatLogText;
    [SerializeField] private ScrollRect chatScrollRect;

    [Header("Mode IA — saisie libre")]
    [SerializeField] private GameObject freeTextPanel;
    [SerializeField] private TMP_InputField playerInputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private GameObject waitingIndicator;

    [Header("Mode hors-ligne — boutons d'action")]
    [SerializeField] private GameObject fallbackButtonsPanel;
    [SerializeField] private Button showShopButton;
    [SerializeField] private Button leaveButton;

    [Header("Listes d'objets (achat/vente)")]
    [SerializeField] private GameObject shopListPanel;
    [SerializeField] private RectTransform shopListContainer;
    [SerializeField] private RectTransform sellListContainer;
    [SerializeField] private MerchantShopRowUI rowPrefab;

    private Merchant merchant;
    private readonly List<MerchantShopRowUI> shopRows = new();
    private readonly List<MerchantShopRowUI> sellRows = new();

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(() => merchant?.CloseDialogue());
        if (sendButton != null) sendButton.onClick.AddListener(OnSendClicked);
        if (playerInputField != null) playerInputField.onSubmit.AddListener(_ => OnSendClicked());
        if (showShopButton != null) showShopButton.onClick.AddListener(() => SetShopListVisible(true));
        if (leaveButton != null) leaveButton.onClick.AddListener(() => merchant?.Farewell());

        Hide();
    }

    public void Show(Merchant owner)
    {
        merchant = owner;

        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        if (merchantNameText != null) merchantNameText.text = owner.Data.merchantName;

        bool ai = owner.AiMode;
        if (freeTextPanel != null) freeTextPanel.SetActive(ai);
        if (fallbackButtonsPanel != null) fallbackButtonsPanel.SetActive(!ai);
        SetWaitingForReply(false);

        RefreshShopLists();
        SetShopListVisible(!ai); // toujours visible en mode hors-ligne ; repliable en mode IA
    }

    public void Hide()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        merchant = null;
    }

    public void AppendMerchantLine(string text)
    {
        if (string.IsNullOrEmpty(text) || chatLogText == null) return;
        chatLogText.text += $"\n<b>{merchant?.Data.merchantName}</b> : {text}";
        ScrollToBottom();
    }

    public void AppendPlayerLine(string text)
    {
        if (chatLogText == null) return;
        chatLogText.text += $"\n<i>Vous</i> : {text}";
        ScrollToBottom();
    }

    public void AppendSystemNote(string text)
    {
        if (chatLogText == null) return;
        chatLogText.text += $"\n<color=#999999>{text}</color>";
        ScrollToBottom();
    }

    public void SetWaitingForReply(bool waiting)
    {
        if (waitingIndicator != null) waitingIndicator.SetActive(waiting);
        if (sendButton != null) sendButton.interactable = !waiting;
        if (playerInputField != null) playerInputField.interactable = !waiting;
    }

    public void RefreshShopLists()
    {
        if (merchant == null || rowPrefab == null) return;
        BuildShopRows();
        BuildSellRows();
    }

    private void BuildShopRows()
    {
        if (shopListContainer == null) return;
        ClearRows(shopListContainer, shopRows);

        foreach (MerchantStockEntry entry in merchant.Data.stock)
        {
            if (entry?.item == null) continue;

            ItemData item = entry.item;
            int price = entry.priceOverride > 0 ? entry.priceOverride : item.baseValue;
            int quantity = merchant.GetDisplayStock(item);

            MerchantShopRowUI row = Instantiate(rowPrefab, shopListContainer);
            shopRows.Add(row);
            row.Setup(item, price, quantity, "Acheter", () => OnShopRowClicked(item, price));
        }
    }

    private void BuildSellRows()
    {
        if (sellListContainer == null) return;
        ClearRows(sellListContainer, sellRows);

        Inventory inv = merchant.GetPlayerInventory();
        if (inv == null) return;

        var seen = new HashSet<ItemData>();
        AddSellRows(inv, InventoryZone.Hotbar, seen);
        AddSellRows(inv, InventoryZone.Backpack, seen);
    }

    private void AddSellRows(Inventory inv, InventoryZone zone, HashSet<ItemData> seen)
    {
        foreach (InventorySlot slot in inv.GetSlots(zone))
        {
            if (slot.IsEmpty || slot.item.baseValue <= 0 || !seen.Add(slot.item)) continue;

            ItemData item = slot.item;
            int price = merchant.GetFallbackSellOffer(item);
            int owned = inv.CountItem(item);

            MerchantShopRowUI row = Instantiate(rowPrefab, sellListContainer);
            sellRows.Add(row);
            row.Setup(item, price, owned, "Vendre", () => OnSellRowClicked(item));
        }
    }

    private void OnShopRowClicked(ItemData item, int defaultPrice)
    {
        if (merchant.AiMode)
        {
            if (playerInputField != null)
                playerInputField.text = $"Je voudrais acheter {item.itemName} pour {defaultPrice} pièces.";
        }
        else
        {
            merchant.FallbackBuy(item);
        }
    }

    private void OnSellRowClicked(ItemData item)
    {
        if (merchant.AiMode)
        {
            if (playerInputField != null)
                playerInputField.text = $"Je voudrais te vendre {item.itemName}.";
        }
        else
        {
            merchant.FallbackSell(item);
        }
    }

    private void SetShopListVisible(bool visible)
    {
        if (shopListPanel != null) shopListPanel.SetActive(visible);
    }

    private void ClearRows(RectTransform container, List<MerchantShopRowUI> rows)
    {
        foreach (MerchantShopRowUI row in rows)
            if (row != null) Destroy(row.gameObject);
        rows.Clear();

        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }

    private void OnSendClicked()
    {
        if (playerInputField == null || merchant == null) return;

        string text = playerInputField.text;
        if (string.IsNullOrWhiteSpace(text)) return;

        playerInputField.text = "";
        merchant.SendPlayerMessage(text);
    }

    private void ScrollToBottom()
    {
        if (chatScrollRect == null) return;
        Canvas.ForceUpdateCanvases();
        chatScrollRect.verticalNormalizedPosition = 0f;
    }
}
