using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RhythmLoadingSceneBuilder
{
    private const string ScenePath = "Assets/0.MainProject/Scenes/LoadingScene.unity";
    private const string UiRoot = "Assets/Resources/UI/UI_Lodaing";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Tools/RhythmRPG/Build Loading Scene UI")]
    public static void Build()
    {
        ConfigureSpriteImports();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Canvas canvas = GetOrCreateComponent<Canvas>("Canvas");
        GameObject canvasObject = canvas.gameObject;
        canvasObject.layer = LayerMask.NameToLayer("UI");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32767;

        CanvasScaler scaler = GetOrAdd<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GetOrAdd<GraphicRaycaster>(canvasObject);
        CanvasGroup canvasGroup = GetOrAdd<CanvasGroup>(canvasObject);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        ClearChildren(canvasObject.transform);

        Image backdrop = CreateSolidImage("LoadingOpaqueBackdrop", canvasObject.transform, Color.black);
        Stretch(backdrop.rectTransform);

        Image background = CreateImage("LoadingExampleBackground", canvasObject.transform, "Loading_example", Vector2.zero, new Vector2(1280f, 720f));
        Stretch(background.rectTransform);
        background.preserveAspect = false;
        background.color = new Color(1f, 1f, 1f, 0.92f);

        Image dim = CreateSolidImage("SceneDim", canvasObject.transform, new Color(0.04f, 0.035f, 0.028f, 0.28f));
        Stretch(dim.rectTransform);

        RectTransform content = CreateRect("LoadingContent", canvasObject.transform, Vector2.zero, new Vector2(1280f, 720f));

        RectTransform headerRoot = CreateRect("LoadingHeader", content, new Vector2(0f, 112f), new Vector2(540f, 140f));
        Image headerScrim = CreateSolidImage("HeaderScrim", headerRoot, new Color(0.03f, 0.025f, 0.02f, 0.52f));
        headerScrim.rectTransform.sizeDelta = new Vector2(540f, 140f);
        Image topOrnament = CreateImage("LoadingTitleTopOrnament", headerRoot, "UI_12", new Vector2(0f, 44f), new Vector2(360f, 34f));
        topOrnament.color = new Color(1f, 0.84f, 0.52f, 0.96f);

        TextMeshProUGUI loadingTitle = CreateText("LoadingTitleText", headerRoot, "LOADING", new Vector2(0f, 4f), new Vector2(460f, 52f), 42f, TextAlignmentOptions.Center);
        loadingTitle.fontStyle = FontStyles.Bold;
        loadingTitle.color = new Color(1f, 0.95f, 0.82f);

        Image bottomOrnament = CreateImage("LoadingTitleBottomOrnament", headerRoot, "UI_05", new Vector2(0f, -34f), new Vector2(340f, 24f));
        bottomOrnament.color = new Color(1f, 0.76f, 0.36f, 0.9f);

        RectTransform progressRoot = CreateRect("ProgressPanel", content, new Vector2(0f, -8f), new Vector2(916f, 125f));
        CreateImage("ProgressFrame", progressRoot, "UI_01", Vector2.zero, new Vector2(916f, 125f));
        CreateImage("ProgressTrackBackground", progressRoot, "UI_02", new Vector2(0f, 0f), new Vector2(805f, 36f));

        Image fill = CreateImage("ProgressFill", progressRoot, "UI_03", new Vector2(0f, 1f), new Vector2(780f, 19f));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;

        CreateImage("ProgressMilestones", progressRoot, "UI_04", new Vector2(0f, 3f), new Vector2(780f, 64f));
        RectTransform track = CreateRect("ProgressTrack", progressRoot, new Vector2(0f, 3f), new Vector2(780f, 1f));

        Image marker = CreateImage("ProgressMarker", progressRoot, "UI_14", new Vector2(-390f, 4f), new Vector2(62f, 78f));
        marker.color = new Color(1f, 0.92f, 0.62f, 1f);

        TextMeshProUGUI progressText = CreateText("ProgressPercentText", progressRoot, "0%", new Vector2(420f, 50f), new Vector2(90f, 28f), 20f, TextAlignmentOptions.Center);
        progressText.color = new Color(1f, 0.95f, 0.78f);

        Slider progressSlider = CreateHiddenSlider("ProgressSlider", progressRoot);

        RectTransform tipRoot = CreateRect("TipPanel", content, new Vector2(0f, -155f), new Vector2(535f, 178f));
        CreateImage("TipPanelFrame", tipRoot, "UI_07", Vector2.zero, new Vector2(535f, 178f));
        CreateImage("TipBadgeFrame", tipRoot, "UI_06", new Vector2(0f, 90f), new Vector2(172f, 46f));

        TextMeshProUGUI tipLabel = CreateText("TipBadgeText", tipRoot, "TIP", new Vector2(0f, 88f), new Vector2(90f, 24f), 17f, TextAlignmentOptions.Center);
        tipLabel.fontStyle = FontStyles.Bold;
        tipLabel.color = new Color(1f, 0.91f, 0.65f);

        CreateImage("TipIcon", tipRoot, "UI_10", new Vector2(-160f, -7f), new Vector2(96f, 100f));

        TextMeshProUGUI tipTitle = CreateText("TipTitleText", tipRoot, "Strike on the beat!", new Vector2(80f, 22f), new Vector2(280f, 32f), 20f, TextAlignmentOptions.Left);
        tipTitle.fontStyle = FontStyles.Bold;
        tipTitle.color = new Color(0.16f, 0.1f, 0.055f);

        TextMeshProUGUI tipBody = CreateText("TipBodyText", tipRoot, "Hitting notes on the beat deals more damage\nand builds more <color=#38CFE7>Focus</color>.", new Vector2(91f, -23f), new Vector2(306f, 56f), 15f, TextAlignmentOptions.Left);
        tipBody.color = new Color(0.13f, 0.09f, 0.055f);
        tipBody.richText = true;

        LoadingSceneController controller = GetOrCreateComponent<LoadingSceneController>("LoadingSceneController");
        controller.enabled = true;
        controller.ProgressBar = progressSlider;
        controller.ProgressText = progressText;
        controller.LoadingCanvasGroup = canvasGroup;
        controller.StatusText = null;
        controller.TipTitleText = tipTitle;
        controller.TipBodyText = tipBody;
        controller.ProgressFillImage = fill;
        controller.ProgressMarker = marker.rectTransform;
        controller.ProgressTrack = track;
        controller.MinimumLoadingTime = 1f;
        controller.ExitFadeDuration = 0.65f;
        controller.ProgressDisplaySpeed = 0.8f;
        controller.MapGenerationTimeout = 30f;

        Camera mainCamera = Object.FindFirstObjectByType<Camera>();
        if (mainCamera != null)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.black;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[RhythmLoadingSceneBuilder] LoadingScene UI rebuilt with header ornaments.");
    }

    private static void ConfigureSpriteImports()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { UiRoot });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    private static T GetOrCreateComponent<T>(string objectName) where T : Component
    {
        GameObject gameObject = GameObject.Find(objectName);
        if (gameObject == null)
        {
            gameObject = new GameObject(objectName);
        }

        return GetOrAdd<T>(gameObject);
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null) component = gameObject.AddComponent<T>();
        return component;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        gameObject.transform.SetParent(parent, false);

        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static Image CreateImage(string name, Transform parent, string spriteName, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform rect = CreateRect(name, parent, anchoredPosition, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = LoadSprite(spriteName);
        image.raycastTarget = false;
        image.preserveAspect = false;
        return image;
    }

    private static Image CreateSolidImage(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.zero);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent, anchoredPosition, size);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.enableWordWrapping = true;
        label.raycastTarget = false;

        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);

        return label;
    }

    private static Slider CreateHiddenSlider(string name, Transform parent)
    {
        RectTransform rect = CreateRect(name, parent, Vector2.zero, new Vector2(780f, 24f));
        Slider slider = rect.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.wholeNumbers = false;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        return slider;
    }

    private static Sprite LoadSprite(string spriteName)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiRoot}/{spriteName}.png");
        if (sprite == null)
        {
            Debug.LogError($"[RhythmLoadingSceneBuilder] Missing sprite: {spriteName}");
        }

        return sprite;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void ClearChildren(Transform transform)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }
}
