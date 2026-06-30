using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Crée la scène MainMenu avec son Canvas et ses boutons câblés au MainMenuManager.
// Menu Unity : Tools > Create Main Menu Scene
public static class MainMenuSceneBuilder
{
    [MenuItem("Tools/Create Main Menu Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // ── Manager ──────────────────────────────────────────
        var managerGo = new GameObject("MainMenuManager");
        var manager = managerGo.AddComponent<MainMenuManager>();

        // ── EventSystem ───────────────────────────────────────
        if (Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 0)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }

        // ── Canvas ────────────────────────────────────────────
        var canvasGo = new GameObject("Canvas");
        var canvas   = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // ── Fond ──────────────────────────────────────────────
        var bg = CreateImage(canvasGo.transform, "Background", new Color(0.06f, 0.06f, 0.10f, 1f));
        Stretch(bg);

        // ── Titre ─────────────────────────────────────────────
        var titleGo  = new GameObject("TitleText");
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.SetParent(bg.transform, false);
        titleRect.anchorMin       = new Vector2(0.5f, 0.62f);
        titleRect.anchorMax       = new Vector2(0.5f, 0.62f);
        titleRect.sizeDelta       = new Vector2(900f, 130f);
        titleRect.anchoredPosition = Vector2.zero;

        var titleTMP = titleGo.AddComponent<TextMeshProUGUI>();
        titleTMP.text             = "Mon Jeu";   // à modifier dans l'Inspector
        titleTMP.fontSize         = 90f;
        titleTMP.font             = TMP_Settings.defaultFontAsset;
        titleTMP.color            = Color.white;
        titleTMP.alignment        = TextAlignmentOptions.Center;
        titleTMP.textWrappingMode = TextWrappingModes.Normal;
        titleTMP.raycastTarget    = false;

        // ── Conteneur boutons ─────────────────────────────────
        var btnContainer     = new GameObject("Buttons");
        var btnContainerRect = btnContainer.AddComponent<RectTransform>();
        btnContainerRect.SetParent(bg.transform, false);
        btnContainerRect.anchorMin       = new Vector2(0.5f, 0.32f);
        btnContainerRect.anchorMax       = new Vector2(0.5f, 0.32f);
        btnContainerRect.sizeDelta       = new Vector2(320f, 160f);
        btnContainerRect.anchoredPosition = Vector2.zero;

        var vLayout = btnContainer.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing             = 20f;
        vLayout.childControlWidth   = true;
        vLayout.childControlHeight  = true;
        vLayout.childForceExpandWidth  = true;
        vLayout.childForceExpandHeight = true;

        // ── Bouton Jouer ──────────────────────────────────────
        var playBtn = CreateButton(btnContainer.transform, "PlayButton", "Jouer");
        UnityEventTools.AddPersistentListener(
            playBtn.GetComponent<Button>().onClick,
            manager.OnPlayClicked);

        // ── Bouton Quitter ────────────────────────────────────
        var quitBtn = CreateButton(btnContainer.transform, "QuitButton", "Quitter");
        UnityEventTools.AddPersistentListener(
            quitBtn.GetComponent<Button>().onClick,
            manager.OnQuitClicked);

        // ── Sauvegarde ────────────────────────────────────────
        const string path = "Assets/Scenes/MainMenu.unity";
        EditorSceneManager.SaveScene(scene, path);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Scène créée",
            $"MainMenu.unity sauvegardée dans {path}\n\n" +
            "Prochaines étapes :\n" +
            "1. Modifier le titre « Mon Jeu » dans l'Inspector du TitleText\n" +
            "2. File > Build Settings → ajouter MainMenu (index 0) et DungeonScene (index 1)",
            "OK");
    }

    // ─────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────

    private static RectTransform CreateImage(Transform parent, string name, Color color)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return rect;
    }

    private static GameObject CreateButton(Transform parent, string name, string label)
    {
        var go   = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.12f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var colors             = btn.colors;
        colors.normalColor     = new Color(1f, 1f, 1f, 0.12f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.28f);
        colors.pressedColor    = new Color(1f, 1f, 1f, 0.45f);
        colors.selectedColor   = new Color(1f, 1f, 1f, 0.20f);
        btn.colors             = colors;

        // Label
        var labelGo   = new GameObject("Label");
        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.SetParent(go.transform, false);
        Stretch(labelRect);

        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text             = label;
        tmp.fontSize         = 36f;
        tmp.font             = TMP_Settings.defaultFontAsset;
        tmp.color            = Color.white;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget    = false;

        return go;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin  = Vector2.zero;
        rect.anchorMax  = Vector2.one;
        rect.offsetMin  = Vector2.zero;
        rect.offsetMax  = Vector2.zero;
    }
}
