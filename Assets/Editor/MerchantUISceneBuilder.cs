#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Crée le Canvas de dialogue marchand (conteneurs vides, TextMeshPro) + prefab de ligne
/// d'objet. Les listes d'achat/vente sont générées au Play par MerchantDialogueUI.
/// Mise en page approximative : ajustez tailles/espacements dans l'éditeur après génération.
/// Menu : J5 Griller > Marchande > ...
/// </summary>
public static class MerchantUISceneBuilder
{
    private const string DialogueMenuPath = "J5 Griller/Marchande/Create Merchant Dialogue UI In Scene";
    private const string RowMenuPath = "J5 Griller/Marchande/Create Merchant Shop Row Prefab";
    private const string RowPrefabPath = "Assets/Prefab/UI/MerchantShopRow.prefab";

    [MenuItem(DialogueMenuPath)]
    public static void CreateMerchantDialogueUI()
    {
        MerchantShopRowUI rowPrefab = EnsureRowPrefab();

        if (Object.FindFirstObjectByType<MerchantDialogueUI>() != null)
        {
            if (!EditorUtility.DisplayDialog("Dialogue marchand",
                "Une MerchantDialogueUI existe déjà dans la scène. Continuer quand même ?", "Oui", "Annuler"))
                return;
        }

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
            Debug.LogWarning("[MerchantUISceneBuilder] Aucune police TMP par défaut trouvée (TMP_Settings.defaultFontAsset). Les textes utiliseront la police TMP par défaut interne.");

        GameObject canvasGo = new GameObject("MerchantDialogueCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15; // au-dessus du Canvas inventaire (10)

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        GameObject panel = CreateRect("DialoguePanel", canvasGo.transform);
        SetAnchors(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(900, 720), Vector2.zero);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.94f);
        MerchantDialogueUI dialogueUI = panel.AddComponent<MerchantDialogueUI>();

        VerticalLayoutGroup rootLayout = panel.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(16, 16, 16, 16);
        rootLayout.spacing = 10;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = false;

        // ── En-tête : nom + bouton fermer ──
        GameObject header = CreateRectOnly("Header", panel.transform);
        header.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 44);

        TextMeshProUGUI nameText = CreateTMPText(header.transform, font, "Marchande", 22, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, 0, 0);
        StretchWithMargins(nameText.gameObject, 0, 0, 56, 0);

        (Button closeButton, _) = CreateButtonWithLabel(header.transform, font, "X");
        SetAnchors(closeButton.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(36, 36), Vector2.zero);

        // ── Journal de discussion ──
        GameObject scrollGo = CreateRect("ChatScrollRect", panel.transform);
        scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.25f);
        scrollGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 280);
        ScrollRect scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = CreateRectOnly("Viewport", scrollGo.transform);
        StretchWithMargins(viewport, 8, 8, 8, 8);
        viewport.AddComponent<RectMask2D>();
        scrollRect.viewport = viewport.GetComponent<RectTransform>();

        GameObject chatLogGo = CreateRectOnly("ChatLogText", viewport.transform);
        RectTransform chatLogRect = chatLogGo.GetComponent<RectTransform>();
        chatLogRect.anchorMin = new Vector2(0f, 1f);
        chatLogRect.anchorMax = new Vector2(1f, 1f);
        chatLogRect.pivot = new Vector2(0.5f, 1f);
        chatLogRect.anchoredPosition = Vector2.zero;
        chatLogRect.sizeDelta = new Vector2(0f, 0f);
        TextMeshProUGUI chatLogText = chatLogGo.AddComponent<TextMeshProUGUI>();
        if (font != null) chatLogText.font = font;
        chatLogText.fontSize = 16;
        chatLogText.alignment = TextAlignmentOptions.TopLeft;
        chatLogText.textWrappingMode = TMPro.TextWrappingModes.Normal;
        chatLogText.text = "";
        ContentSizeFitter fitter = chatLogGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = chatLogRect;

        // ── Mode IA : saisie libre ──
        GameObject freeTextPanel = CreateRectOnly("FreeTextPanel", panel.transform);
        freeTextPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 44);

        TMP_InputField inputField = CreateInputField(freeTextPanel.transform, font, "Écris quelque chose...");
        StretchWithMargins(inputField.gameObject, 0, 0, 96, 0);

        (Button sendButton, _) = CreateButtonWithLabel(freeTextPanel.transform, font, "Envoyer");
        SetAnchors(sendButton.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(88, 40), Vector2.zero);

        TextMeshProUGUI waitingIndicator = CreateTMPText(freeTextPanel.transform, font, "En attente de réponse...", 14, FontStyles.Italic, TextAlignmentOptions.Midline, 0, 0);
        StretchWithMargins(waitingIndicator.gameObject, 0, 0, 96, 0);
        waitingIndicator.raycastTarget = false;
        waitingIndicator.gameObject.SetActive(false);

        // ── Mode hors-ligne : boutons d'action ──
        GameObject fallbackPanel = CreateRectOnly("FallbackButtonsPanel", panel.transform);
        fallbackPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 44);
        HorizontalLayoutGroup fallbackLayout = fallbackPanel.AddComponent<HorizontalLayoutGroup>();
        fallbackLayout.spacing = 10;
        fallbackLayout.childForceExpandWidth = true;
        fallbackLayout.childForceExpandHeight = true;
        fallbackLayout.childControlWidth = true;
        fallbackLayout.childControlHeight = true;

        (Button showShopButton, _) = CreateButtonWithLabel(fallbackPanel.transform, font, "Voir ses articles");
        (Button leaveButton, _) = CreateButtonWithLabel(fallbackPanel.transform, font, "Partir");

        // ── Listes d'objets ──
        GameObject shopListPanel = CreateRectOnly("ShopListPanel", panel.transform);
        shopListPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 240);
        HorizontalLayoutGroup shopListLayout = shopListPanel.AddComponent<HorizontalLayoutGroup>();
        shopListLayout.spacing = 16;
        shopListLayout.childForceExpandWidth = true;
        shopListLayout.childForceExpandHeight = true;
        shopListLayout.childControlWidth = true;
        shopListLayout.childControlHeight = true;

        BuildShopColumn(shopListPanel.transform, font, "Elle vend", out RectTransform shopListContainer);
        BuildShopColumn(shopListPanel.transform, font, "Tu peux vendre", out RectTransform sellListContainer);

        SerializedObject so = new SerializedObject(dialogueUI);
        so.FindProperty("dialoguePanel").objectReferenceValue = panel;
        so.FindProperty("merchantNameText").objectReferenceValue = nameText;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("chatLogText").objectReferenceValue = chatLogText;
        so.FindProperty("chatScrollRect").objectReferenceValue = scrollRect;
        so.FindProperty("freeTextPanel").objectReferenceValue = freeTextPanel;
        so.FindProperty("playerInputField").objectReferenceValue = inputField;
        so.FindProperty("sendButton").objectReferenceValue = sendButton;
        so.FindProperty("waitingIndicator").objectReferenceValue = waitingIndicator.gameObject;
        so.FindProperty("fallbackButtonsPanel").objectReferenceValue = fallbackPanel;
        so.FindProperty("showShopButton").objectReferenceValue = showShopButton;
        so.FindProperty("leaveButton").objectReferenceValue = leaveButton;
        so.FindProperty("shopListPanel").objectReferenceValue = shopListPanel;
        so.FindProperty("shopListContainer").objectReferenceValue = shopListContainer;
        so.FindProperty("sellListContainer").objectReferenceValue = sellListContainer;
        so.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false); // MerchantDialogueUI.Awake() le cache déjà, mais plus clair dans l'éditeur

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = canvasGo;
        Debug.Log("[MerchantUISceneBuilder] Canvas de dialogue marchand créé. Ajustez tailles/espacements si besoin, puis Play.");
    }

    [MenuItem(RowMenuPath)]
    public static MerchantShopRowUI EnsureRowPrefabMenu() => EnsureRowPrefab();

    private static MerchantShopRowUI EnsureRowPrefab()
    {
        MerchantShopRowUI existing = AssetDatabase.LoadAssetAtPath<MerchantShopRowUI>(RowPrefabPath);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Prefab/UI"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
                AssetDatabase.CreateFolder("Assets", "Prefab");
            AssetDatabase.CreateFolder("Assets/Prefab", "UI");
        }

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        GameObject rowGo = BuildRowTemplate(font);
        MerchantShopRowUI prefab = PrefabUtility.SaveAsPrefabAsset(rowGo, RowPrefabPath).GetComponent<MerchantShopRowUI>();
        Object.DestroyImmediate(rowGo);
        AssetDatabase.SaveAssets();
        Debug.Log($"[MerchantUISceneBuilder] Prefab créé : {RowPrefabPath}");
        return prefab;
    }

    private static GameObject BuildRowTemplate(TMP_FontAsset font)
    {
        GameObject rowGo = CreateRect("MerchantShopRow", null);
        rowGo.GetComponent<RectTransform>().sizeDelta = new Vector2(340, 56);
        rowGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);

        GameObject iconGo = CreateRect("Icon", rowGo.transform);
        Image icon = iconGo.GetComponent<Image>();
        icon.color = Color.white;
        icon.preserveAspect = true;
        SetAnchors(iconGo, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40, 40), new Vector2(8, 0));

        TextMeshProUGUI nameText = CreateTMPText(rowGo.transform, font, "Objet", 16, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, 0, 0);
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = new Vector2(56, 0f);
        nameRect.offsetMax = new Vector2(-150, 0f);

        TextMeshProUGUI priceText = CreateTMPText(rowGo.transform, font, "0 or", 14, FontStyles.Normal, TextAlignmentOptions.MidlineRight, 60, 24);
        SetAnchors(priceText.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(60, 24), new Vector2(-82, 8));

        TextMeshProUGUI quantityText = CreateTMPText(rowGo.transform, font, "", 12, FontStyles.Normal, TextAlignmentOptions.MidlineRight, 60, 18);
        SetAnchors(quantityText.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(60, 18), new Vector2(-82, -10));

        (Button actionButton, TextMeshProUGUI actionLabel) = CreateButtonWithLabel(rowGo.transform, font, "Acheter");
        SetAnchors(actionButton.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(72, 36), new Vector2(-4, 0));

        MerchantShopRowUI rowUI = rowGo.AddComponent<MerchantShopRowUI>();
        SerializedObject so = new SerializedObject(rowUI);
        so.FindProperty("icon").objectReferenceValue = icon;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("priceText").objectReferenceValue = priceText;
        so.FindProperty("quantityText").objectReferenceValue = quantityText;
        so.FindProperty("actionButton").objectReferenceValue = actionButton;
        so.FindProperty("actionButtonLabel").objectReferenceValue = actionLabel;
        so.ApplyModifiedPropertiesWithoutUndo();

        return rowGo;
    }

    private static GameObject BuildShopColumn(Transform parent, TMP_FontAsset font, string title, out RectTransform listContainer)
    {
        GameObject column = CreateRectOnly(title, parent);
        VerticalLayoutGroup columnLayout = column.AddComponent<VerticalLayoutGroup>();
        columnLayout.spacing = 6;
        columnLayout.childForceExpandWidth = true;
        columnLayout.childForceExpandHeight = false;
        columnLayout.childControlWidth = true;
        columnLayout.childControlHeight = false;

        CreateTMPText(column.transform, font, title, 15, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, 0, 24);

        GameObject listGo = CreateRectOnly("ListContainer", column.transform);
        listGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 210);
        VerticalLayoutGroup listLayout = listGo.AddComponent<VerticalLayoutGroup>();
        listLayout.spacing = 4;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = false;

        listContainer = listGo.GetComponent<RectTransform>();
        return column;
    }

    // ──────────────────────────────────────────
    // Helpers de construction UI (RectTransform / TMP)
    // ──────────────────────────────────────────

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        if (parent != null)
            go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreateRectOnly(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetAnchors(GameObject go, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 pos)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
    }

    private static void StretchWithMargins(GameObject go, float left, float top, float right, float bottom)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static TextMeshProUGUI CreateTMPText(
        Transform parent, TMP_FontAsset font, string text, int size, FontStyles style,
        TextAlignmentOptions alignment, float w, float h)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = alignment;
        t.color = Color.white;
        t.textWrappingMode = TMPro.TextWrappingModes.Normal;
        t.rectTransform.sizeDelta = new Vector2(w, h);
        return t;
    }

    private static (Button button, TextMeshProUGUI label) CreateButtonWithLabel(Transform parent, TMP_FontAsset font, string label)
    {
        GameObject go = CreateRect("Button_" + label, parent);
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();

        TextMeshProUGUI text = CreateTMPText(go.transform, font, label, 16, FontStyles.Normal, TextAlignmentOptions.Midline, 0, 0);
        text.raycastTarget = false;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return (button, text);
    }

    private static TMP_InputField CreateInputField(Transform parent, TMP_FontAsset font, string placeholderText)
    {
        GameObject root = CreateRect("InputField", parent);
        root.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
        TMP_InputField inputField = root.AddComponent<TMP_InputField>();

        GameObject textArea = CreateRectOnly("Text Area", root.transform);
        StretchWithMargins(textArea, 10, 6, 10, 6);
        textArea.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = CreateTMPText(textArea.transform, font, placeholderText, 16, FontStyles.Italic, TextAlignmentOptions.MidlineLeft, 0, 0);
        StretchWithMargins(placeholder.gameObject, 0, 0, 0, 0);
        placeholder.color = new Color(1f, 1f, 1f, 0.5f);
        placeholder.raycastTarget = false;

        TextMeshProUGUI text = CreateTMPText(textArea.transform, font, "", 16, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, 0, 0);
        StretchWithMargins(text.gameObject, 0, 0, 0, 0);
        text.raycastTarget = false;

        inputField.textViewport = textArea.GetComponent<RectTransform>();
        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        return inputField;
    }
}
#endif
