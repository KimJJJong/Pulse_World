using System.Collections.Generic;
using NetClient.Room.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class TownSceneUiBuilder
{
    private const string OverlayCanvasName = "Canvas_TownExpeditionOverlay";
    private const string ApiClientProviderPrefabPath = "Assets/0.MainProject/Resources/ApiClientProvider.prefab";
    private const string RoomUiRootPrefabPath = "Assets/0.MainProject/Resources/RoomUIRoot.prefab";
    private const int ExpeditionSortingOrder = 7000;
    private const int InventorySortingOrder = 8000;

    private static readonly string[] GameMapIds =
    {
        "Game_Forest_Tutorial",
        "Game_Forest_01",
        "Game_01",
        "Game"
    };

    private static readonly string[] GameTitles =
    {
        "Forest Tutorial",
        "Whispering Forest",
        "Game 01",
        "Game"
    };

    [MenuItem("RhythmRPG/Editors/UI/Ensure Town Scene UI Objects")]
    public static void EnsureTownSceneUiObjects()
    {
        EnsureEventSystem();
        var apiProvider = EnsureApiClientProvider();
        EnsureRoomUiRoot(apiProvider);
        EnsureTownExpeditionOverlay();
        InventoryUIBuilder.CreateTownInventoryMenu();
        ConfigureTownInventory();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[TownSceneUiBuilder] Town scene UI objects are visible in the Hierarchy and positioned away from the minimap.");
    }

    private static void EnsureTownExpeditionOverlay()
    {
        var canvasGo = GameObject.Find(OverlayCanvasName);
        if (canvasGo == null)
            canvasGo = new GameObject(OverlayCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
            canvasGo.layer = uiLayer;

        var canvas = GetOrAdd<Canvas>(canvasGo);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = ExpeditionSortingOrder;

        var scaler = GetOrAdd<CanvasScaler>(canvasGo);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GetOrAdd<GraphicRaycaster>(canvasGo);
        var panel = GetOrAdd<TownExpeditionPanel>(canvasGo);

        ClearChildren(canvasGo.transform);

        var font = LoadKoreanFont();
        var root = CreateImagePanel("TownExpeditionInfo", canvasGo.transform, new Color32(226, 202, 148, 235), false);
        SetRect(root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(304f, 254f), new Vector2(-56f, -376f));

        var header = CreateImagePanel("Header", root, new Color32(19, 68, 75, 245), false);
        SetStretchTop(header, 34f);
        var titleText = CreateText("Title", header, "Town Party", 22, TextAlignmentOptions.MidlineLeft, new Color32(246, 231, 185, 255), font);
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(280f, 30f), new Vector2(14f, -2f));

        var statusText = CreateText("Status", root, "Town 정보를 불러오는 중...", 16, TextAlignmentOptions.MidlineLeft, new Color32(43, 50, 49, 255), font);
        SetRect(statusText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(276f, 24f), new Vector2(14f, -44f));

        var detailText = CreateText("Detail", root, "", 14, TextAlignmentOptions.TopLeft, new Color32(43, 50, 49, 255), font);
        SetRect(detailText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(276f, 52f), new Vector2(14f, -72f));

        var readySummaryText = CreateText("ReadySummary", root, "참가자 준비: 대기방 연결 전", 14, TextAlignmentOptions.TopLeft, new Color32(43, 50, 49, 255), font);
        SetRect(readySummaryText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(276f, 40f), new Vector2(14f, -130f));
        readySummaryText.gameObject.SetActive(false);

        var hostStartButton = CreateButton("HostStartGameButton", root, "게임 시작", new Vector2(132f, 30f), new Vector2(14f, 48f), new Vector2(0f, 0f), font);
        var hostCancelButton = CreateButton("HostCancelGameButton", root, "대기 취소", new Vector2(132f, 30f), new Vector2(158f, 48f), new Vector2(0f, 0f), font);
        hostStartButton.gameObject.SetActive(false);
        hostCancelButton.gameObject.SetActive(false);

        var inventoryButton = CreateButton("InventoryButton", root, "인벤토리", new Vector2(88f, 30f), new Vector2(14f, 12f), new Vector2(0f, 0f), font);
        var gameSelectButton = CreateButton("GameSelectButton", root, "게임 선택", new Vector2(88f, 30f), new Vector2(108f, 12f), new Vector2(0f, 0f), font);
        var readyButton = CreateButton("ReadyWindowButton", root, "준비창 열기", new Vector2(92f, 30f), new Vector2(202f, 12f), new Vector2(0f, 0f), font);
        readyButton.gameObject.SetActive(false);

        var gameWindow = CreateImagePanel("TownGameSelectWindow", canvasGo.transform, new Color32(226, 202, 148, 245), false);
        SetRect(gameWindow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360f, 282f), new Vector2(-240f, 0f));

        var windowHeader = CreateImagePanel("Header", gameWindow, new Color32(19, 68, 75, 250), false);
        SetStretchTop(windowHeader, 42f);
        var gameWindowTitle = CreateText("Title", windowHeader, "Game 선택", 24, TextAlignmentOptions.MidlineLeft, new Color32(246, 231, 185, 255), font);
        SetRect(gameWindowTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(290f, 38f), new Vector2(16f, -2f));

        var closeButton = CreateButton("CloseButton", windowHeader, "X", new Vector2(32f, 30f), new Vector2(-10f, -6f), new Vector2(1f, 1f), font);
        var descText = CreateText("Desc", gameWindow, "Host가 만들 Game 대기방을 선택합니다.", 15, TextAlignmentOptions.MidlineLeft, new Color32(43, 50, 49, 255), font);
        SetRect(descText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(320f, 28f), new Vector2(20f, -56f));

        var optionButtons = new List<Button>(GameMapIds.Length);
        for (int i = 0; i < GameMapIds.Length; i++)
        {
            var option = CreateButton($"GameOption_{GameMapIds[i]}", gameWindow, GameTitles[i], new Vector2(320f, 34f), new Vector2(20f, -94f - (i * 42f)), new Vector2(0f, 1f), font);
            optionButtons.Add(option);
        }

        gameWindow.gameObject.SetActive(false);
        BindPanel(panel, canvas, root, titleText, statusText, detailText, readySummaryText, inventoryButton, gameSelectButton, readyButton, hostStartButton, hostCancelButton, gameWindow, closeButton, optionButtons);
        EditorUtility.SetDirty(canvasGo);
    }

    private static void BindPanel(
        TownExpeditionPanel panel,
        Canvas canvas,
        RectTransform root,
        TMP_Text titleText,
        TMP_Text statusText,
        TMP_Text detailText,
        TMP_Text readySummaryText,
        Button inventoryButton,
        Button gameSelectButton,
        Button readyButton,
        Button hostStartButton,
        Button hostCancelButton,
        RectTransform gameWindow,
        Button closeButton,
        List<Button> optionButtons)
    {
        var serialized = new SerializedObject(panel);
        serialized.FindProperty("_canvas").objectReferenceValue = canvas;
        serialized.FindProperty("_root").objectReferenceValue = root;
        serialized.FindProperty("_titleText").objectReferenceValue = titleText;
        serialized.FindProperty("_statusText").objectReferenceValue = statusText;
        serialized.FindProperty("_detailText").objectReferenceValue = detailText;
        serialized.FindProperty("_readySummaryText").objectReferenceValue = readySummaryText;
        serialized.FindProperty("_inventoryButton").objectReferenceValue = inventoryButton;
        serialized.FindProperty("_gameSelectButton").objectReferenceValue = gameSelectButton;
        serialized.FindProperty("_readyWindowButton").objectReferenceValue = readyButton;
        serialized.FindProperty("_hostStartGameButton").objectReferenceValue = hostStartButton;
        serialized.FindProperty("_hostCancelGameButton").objectReferenceValue = hostCancelButton;
        serialized.FindProperty("_gameSelectWindow").objectReferenceValue = gameWindow;
        serialized.FindProperty("_gameSelectCloseButton").objectReferenceValue = closeButton;

        var options = serialized.FindProperty("_gameOptionButtons");
        options.arraySize = optionButtons.Count;
        for (int i = 0; i < optionButtons.Count; i++)
            options.GetArrayElementAtIndex(i).objectReferenceValue = optionButtons[i];

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureTownInventory()
    {
        var root = GameObject.Find("TownInventory_UI");
        if (root == null)
            return;

        var canvas = GetOrAdd<Canvas>(root);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = InventorySortingOrder;

        var scaler = GetOrAdd<CanvasScaler>(root);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        GetOrAdd<GraphicRaycaster>(root);

        var panel = root.transform.Find("Panel") as RectTransform;
        if (panel != null)
        {
            panel.anchorMin = new Vector2(0.04f, 0.08f);
            panel.anchorMax = new Vector2(0.62f, 0.92f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panel.gameObject.SetActive(false);
        }

        EditorUtility.SetDirty(root);
    }

    private static ApiClientProvider EnsureApiClientProvider()
    {
        var provider = Object.FindFirstObjectByType<ApiClientProvider>(FindObjectsInactive.Include);
        if (provider != null)
        {
            provider.gameObject.SetActive(true);
            EditorUtility.SetDirty(provider.gameObject);
            return provider;
        }

        GameObject go = null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ApiClientProviderPrefabPath);
        if (prefab != null)
            go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

        if (go == null)
            go = new GameObject("ApiClientProvider", typeof(ApiClientProvider));

        go.name = "ApiClientProvider";
        go.SetActive(true);
        EditorUtility.SetDirty(go);
        return go.GetComponent<ApiClientProvider>() ?? go.AddComponent<ApiClientProvider>();
    }

    private static void EnsureRoomUiRoot(ApiClientProvider apiProvider)
    {
        var controller = Object.FindFirstObjectByType<RoomUiController>(FindObjectsInactive.Include);
        GameObject root = controller != null ? controller.gameObject : null;

        if (root == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoomUiRootPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[TownSceneUiBuilder] Missing Room UI prefab: {RoomUiRootPrefabPath}");
                return;
            }

            root = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (root == null)
            {
                Debug.LogError("[TownSceneUiBuilder] Failed to instantiate RoomUIRoot prefab.");
                return;
            }
        }

        root.name = "RoomUIRoot";
        controller = root.GetComponentInChildren<RoomUiController>(true);
        if (controller != null && apiProvider != null)
        {
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("apiProvider").objectReferenceValue = apiProvider;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        root.SetActive(false);
        EditorUtility.SetDirty(root);
    }

    private static void EnsureEventSystem()
    {
        var eventSystem = Object.FindObjectOfType<EventSystem>(true);
        GameObject eventSystemGo;
        if (eventSystem == null)
        {
            eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
        else
        {
            eventSystemGo = eventSystem.gameObject;
            eventSystemGo.SetActive(true);
        }

        GetOrAdd<EventSystem>(eventSystemGo).enabled = true;
        GetOrAdd<InputSystemUIInputModule>(eventSystemGo).enabled = true;
        var standalone = eventSystemGo.GetComponent<StandaloneInputModule>();
        if (standalone != null)
            standalone.enabled = false;

        EditorUtility.SetDirty(eventSystemGo);
    }

    private static RectTransform CreateImagePanel(string name, Transform parent, Color color, bool raycastTarget)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return go.GetComponent<RectTransform>();
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 size, Vector2 anchoredPosition, Vector2 anchor, TMP_FontAsset font)
    {
        var rect = CreateImagePanel(name, parent, new Color32(19, 68, 75, 250), true);
        SetRect(rect, anchor, anchor, anchor, size, anchoredPosition);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();

        var text = CreateText("Label", rect, label, 15, TextAlignmentOptions.Center, new Color32(246, 231, 185, 255), font);
        SetRect(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, Vector2.zero);
        ConfigureButtonColors(button);
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, int size, TextAlignmentOptions alignment, Color color, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.color = color;
        if (font != null)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
        }

        return text;
    }

    private static void ConfigureButtonColors(Button button)
    {
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.78f, 1f);
        colors.pressedColor = new Color(0.82f, 0.75f, 0.58f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        button.colors = colors;
    }

    private static void SetStretchTop(RectTransform rect, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(0f, -height);
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 anchoredPosition)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    private static TMP_FontAsset LoadKoreanFont()
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/NanumGothic SDF");
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("NanumGothic SDF");
        return font;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }
}
