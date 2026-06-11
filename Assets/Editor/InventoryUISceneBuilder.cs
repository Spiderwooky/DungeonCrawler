#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Crée le Canvas inventaire (conteneurs vides) + prefab de slot.
/// Les slots sont générés au Play selon les compteurs sur InventoryUI.
/// Menu : J5 Griller > Create Inventory UI In Scene
/// </summary>
public static class InventoryUISceneBuilder
{
    private const string MenuPath = "J5 Griller/Create Inventory UI In Scene";
    private const string SlotPrefabPath = "Assets/Prefab/UI/InventorySlot.prefab";

    [MenuItem(MenuPath)]
    public static void CreateInventoryUI()
    {
        InventorySlotUI slotPrefab = EnsureSlotPrefab();

        if (Object.FindFirstObjectByType<InventoryUI>() != null)
        {
            if (!EditorUtility.DisplayDialog("Inventaire UI",
                "Une InventoryUI existe déjà. Continuer quand même ?", "Oui", "Annuler"))
                return;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasGo = new GameObject("InventoryCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        InventoryUI inventoryUI = canvasGo.AddComponent<InventoryUI>();

        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        Inventory inventory = Object.FindFirstObjectByType<Inventory>();

        GameObject hotbar = CreateRect("Hotbar", canvasGo.transform);
        SetAnchors(hotbar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(520, 72), new Vector2(0, 24));
        HorizontalLayoutGroup hotbarLayout = hotbar.AddComponent<HorizontalLayoutGroup>();
        hotbarLayout.spacing = 8;
        hotbarLayout.padding = new RectOffset(8, 8, 8, 8);
        hotbarLayout.childAlignment = TextAnchor.MiddleCenter;
        hotbarLayout.childForceExpandWidth = false;
        hotbarLayout.childForceExpandHeight = false;

        GameObject panel = CreateRect("InventoryPanel", canvasGo.transform);
        SetAnchors(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(560, 440), Vector2.zero);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.92f);
        panel.SetActive(false);

        VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(16, 16, 16, 16);
        panelLayout.spacing = 12;
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        CreateText(panel.transform, font, "Inventaire", 26, FontStyle.Bold, 500, 36);

        GameObject grid = CreateRect("BackpackGrid", panel.transform);
        SetAnchors(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520, 280), Vector2.zero);
        GridLayoutGroup gridLayout = grid.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(64, 64);
        gridLayout.spacing = new Vector2(8, 8);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 8;
        gridLayout.childAlignment = TextAnchor.UpperCenter;

        Text hint = CreateText(panel.transform, font, "", 15, FontStyle.Italic, 500, 28);
        hint.color = new Color(0.8f, 0.8f, 0.8f);

        SerializedObject so = new SerializedObject(inventoryUI);
        so.FindProperty("inventory").objectReferenceValue = inventory;
        so.FindProperty("inventoryPanel").objectReferenceValue = panel;
        so.FindProperty("hintText").objectReferenceValue = hint;
        so.FindProperty("hotbarSlotCount").intValue = 5;
        so.FindProperty("backpackSlotCount").intValue = 16;
        so.FindProperty("backpackColumns").intValue = 8;
        so.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
        so.FindProperty("hotbarContainer").objectReferenceValue = hotbar.GetComponent<RectTransform>();
        so.FindProperty("backpackContainer").objectReferenceValue = grid.GetComponent<RectTransform>();
        so.ApplyModifiedPropertiesWithoutUndo();

        InventoryInputHandler input = Object.FindFirstObjectByType<InventoryInputHandler>();
        if (input != null)
        {
            SerializedObject inputSo = new SerializedObject(input);
            inputSo.FindProperty("inventoryUI").objectReferenceValue = inventoryUI;
            inputSo.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = canvasGo;
        Debug.Log("[InventoryUISceneBuilder] Canvas créé. Modifiez Hotbar/Backpack Slot Count sur InventoryUI, puis Play.");
    }

    [MenuItem("J5 Griller/Create Inventory Slot Prefab")]
    public static InventorySlotUI EnsureSlotPrefabMenu()
    {
        return EnsureSlotPrefab();
    }

    private static InventorySlotUI EnsureSlotPrefab()
    {
        InventorySlotUI existing = AssetDatabase.LoadAssetAtPath<InventorySlotUI>(SlotPrefabPath);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Prefab/UI"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
                AssetDatabase.CreateFolder("Assets", "Prefab");
            AssetDatabase.CreateFolder("Assets/Prefab", "UI");
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject slotGo = BuildSlotTemplate(font);
        InventorySlotUI prefab = PrefabUtility.SaveAsPrefabAsset(slotGo, SlotPrefabPath).GetComponent<InventorySlotUI>();
        Object.DestroyImmediate(slotGo);
        AssetDatabase.SaveAssets();
        Debug.Log($"[InventoryUISceneBuilder] Prefab créé : {SlotPrefabPath}");
        return prefab;
    }

    private static GameObject BuildSlotTemplate(Font font)
    {
        GameObject slotGo = CreateRect("InventorySlot", null);
        SetAnchors(slotGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(64, 64), Vector2.zero);
        Image bg = slotGo.GetComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);

        GameObject pickHl = CreateRect("PickHighlight", slotGo.transform);
        StretchFull(pickHl);
        Image pickImg = pickHl.GetComponent<Image>();
        pickImg.color = new Color(0.2f, 0.7f, 1f, 0.55f);
        pickImg.raycastTarget = false;
        pickHl.SetActive(false);

        GameObject selHl = CreateRect("SelectionHighlight", slotGo.transform);
        StretchFull(selHl);
        Image selImg = selHl.GetComponent<Image>();
        selImg.color = new Color(0.85f, 0.65f, 0.15f, 0.75f);
        selImg.raycastTarget = false;
        selHl.SetActive(false);

        GameObject iconGo = CreateRect("Icon", slotGo.transform);
        SetAnchors(iconGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(48, 48), Vector2.zero);
        Image icon = iconGo.GetComponent<Image>();
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        Text amount = CreateText(slotGo.transform, font, "", 14, FontStyle.Bold, 40, 20);
        RectTransform amountRect = amount.rectTransform;
        amountRect.anchorMin = new Vector2(1f, 0f);
        amountRect.anchorMax = new Vector2(1f, 0f);
        amountRect.pivot = new Vector2(1f, 0f);
        amountRect.anchoredPosition = new Vector2(-4, 4);
        amount.alignment = TextAnchor.LowerRight;
        amount.raycastTarget = false;

        Text label = CreateText(slotGo.transform, font, "", 12, FontStyle.Normal, 60, 20);
        RectTransform keyRect = label.rectTransform;
        keyRect.anchorMin = new Vector2(0f, 1f);
        keyRect.anchorMax = new Vector2(0f, 1f);
        keyRect.pivot = new Vector2(0f, 1f);
        keyRect.anchoredPosition = new Vector2(4, -4);
        label.color = new Color(0.75f, 0.75f, 0.75f);
        label.raycastTarget = false;

        slotGo.AddComponent<Button>().targetGraphic = bg;
        slotGo.AddComponent<CanvasGroup>();
        InventorySlotUI slotUI = slotGo.AddComponent<InventorySlotUI>();

        SerializedObject slotSo = new SerializedObject(slotUI);
        slotSo.FindProperty("background").objectReferenceValue = bg;
        slotSo.FindProperty("icon").objectReferenceValue = icon;
        slotSo.FindProperty("amountText").objectReferenceValue = amount;
        slotSo.FindProperty("keyLabel").objectReferenceValue = label;
        slotSo.FindProperty("pickHighlight").objectReferenceValue = pickImg;
        slotSo.FindProperty("selectionHighlight").objectReferenceValue = selImg;
        slotSo.ApplyModifiedPropertiesWithoutUndo();

        return slotGo;
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
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

    private static void StretchFull(GameObject go)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Text CreateText(Transform parent, Font font, string text, int size, FontStyle style, float w, float h)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.rectTransform.sizeDelta = new Vector2(w, h);
        return t;
    }
}
#endif
