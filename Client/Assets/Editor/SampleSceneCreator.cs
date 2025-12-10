// Assets/Editor/SampleSceneCreator.cs  (중요 부분만 발췌/갱신)
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.IO;

public static class SampleSceneCreator
{
    [MenuItem("Tools/NetClient/Generate Sample Scene & Prefabs")]
    public static void Generate()
    {
        var root = "Assets/NetClientSample";
        var pfDir = $"{root}/Prefabs";
        var scDir = $"{root}/Scenes";
        Directory.CreateDirectory(root); Directory.CreateDirectory(pfDir); Directory.CreateDirectory(scDir);

        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);

        // Canvas/EventSystem
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // TopBar
        var topBar = CreatePanel("TopBar", canvas.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(1600, 60));
        var btnRefresh = CreateButton("Refresh", topBar.transform, new Vector2(60, -30), new Vector2(120, 40), "Refresh");
        var inputTitle = CreateInputField("TitleInput", topBar.transform, new Vector2(350, -30), new Vector2(400, 40), "Room Title");
        var btnCreate = CreateButton("Create", topBar.transform, new Vector2(600, -30), new Vector2(120, 40), "Create");

        // LobbySection (좌측 리스트)
        var lobbySection = CreatePanel("LobbySection", canvas.transform, new Vector2(0, 1), new Vector2(0, 0), new Vector2(10, -70), new Vector2(520, 820));
        var (scrollView, content) = CreateScrollView("RoomList", lobbySection.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 800));

        // RoomSection (우측: 멤버/버튼/카운트다운) - 기본 비활성
        var roomSection = CreatePanel("RoomSection", canvas.transform, new Vector2(1, 1), new Vector2(1, 0), new Vector2(-10, -70), new Vector2(1060, 820));
        roomSection.SetActive(false);

        var btnReady = CreateButton("Ready", roomSection.transform, new Vector2(-900, -100), new Vector2(140, 50), "Ready");
        var btnUnready = CreateButton("Unready", roomSection.transform, new Vector2(-740, -100), new Vector2(140, 50), "Unready");
        var btnLeave = CreateButton("Leave", roomSection.transform, new Vector2(-580, -100), new Vector2(140, 50), "Leave");
        var countdownT = CreateText("CountdownText", roomSection.transform, new Vector2(-900, -180), new Vector2(280, 60), "", 28, TextAnchor.MiddleLeft);

        // 슬롯 2개 (PlayerSlotView)
        var slot1 = CreatePlayerSlot(roomSection, new Vector2(-900, -260), "Player 1");
        var slot2 = CreatePlayerSlot(roomSection, new Vector2(-700, -260), "Player 2");

        // RoomItem 프리팹
        var itemGo = CreateRoomItemPrefab(pfDir, out RoomItemView itemView);

        // NetClient + 바인딩
        var clientGo = new GameObject("NetClient", typeof(RoomUIView));
        var ui = clientGo.GetComponent<RoomUIView>();
        ui.BtnRefresh = btnRefresh.GetComponent<Button>();
        ui.BtnCreate = btnCreate.GetComponent<Button>();
        ui.InputTitle = inputTitle.GetComponent<InputField>();
        ui.RoomListRoot = content;
        ui.RoomItemPrefab = itemView;

        ui.LobbySection = lobbySection;
        ui.RoomSection = roomSection;
        ui.BtnReady = btnReady.GetComponent<Button>();
        ui.BtnUnready = btnUnready.GetComponent<Button>();
        ui.BtnLeave = btnLeave.GetComponent<Button>();
        ui.TxtCountdown = countdownT.GetComponent<Text>();
        ui.Slot1 = slot1.GetComponent<PlayerSlotView>();
        ui.Slot2 = slot2.GetComponent<PlayerSlotView>();

        new GameObject("MainThreadDispatcher").AddComponent<MainThreadDispatcher>();

        var scenePath = $"{scDir}/LobbyRoomSample.unity";
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("NetClient", $"샘플 씬/프리팹 생성 완료!\n\nScene:\n{scenePath}\n\nPrefab:\n{pfDir}/RoomItem.prefab", "OK");
    }

    // ===== UI 유틸들 =====

    static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = anchoredPos;
        var img = go.GetComponent<Image>(); img.color = new Color(0, 0, 0, 0.25f);
        return go;
    }

    static GameObject CreateButton(string name, Transform parent, Vector2 anchoredPos, Vector2 size, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = size; rt.anchoredPosition = anchoredPos;
        var img = go.GetComponent<Image>(); img.color = new Color(0.2f, 0.5f, 0.9f, 1f);
        CreateText("Text", go.transform, Vector2.zero, size, label, 20, TextAnchor.MiddleCenter);
        return go;
    }

    static GameObject CreateInputField(string name, Transform parent, Vector2 anchoredPos, Vector2 size, string placeholder)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = size; rt.anchoredPosition = anchoredPos;
        var img = go.GetComponent<Image>(); img.color = Color.white;

        var text = CreateText("Text", go.transform, Vector2.zero, new Vector2(size.x - 16, size.y - 10), "", 18, TextAnchor.MiddleLeft);
        var placeholderObj = CreateText("Placeholder", go.transform, Vector2.zero, new Vector2(size.x - 16, size.y - 10), placeholder, 18, TextAnchor.MiddleLeft);
        var tText = text.GetComponent<Text>(); var pText = placeholderObj.GetComponent<Text>();
        pText.color = new Color(0, 0, 0, 0.5f);

        var ifd = go.GetComponent<InputField>();
        ifd.textComponent = tText; ifd.placeholder = pText;
        return go;
    }

    static (GameObject, Transform) CreateScrollView(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        root.transform.SetParent(parent, false);
        var rt = root.GetComponent<RectTransform>(); rt.sizeDelta = size; rt.anchoredPosition = anchoredPos;
        root.GetComponent<Image>().color = new Color(1, 1, 1, 0.1f);

        var viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(root.transform, false);
        var vp = viewport.GetComponent<RectTransform>(); vp.anchorMin = new Vector2(0, 1); vp.anchorMax = new Vector2(0, 1); vp.pivot = new Vector2(0, 1);
        vp.sizeDelta = new Vector2(size.x - 20, size.y - 20); vp.anchoredPosition = new Vector2(10, -10);

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var ct = content.GetComponent<RectTransform>(); ct.anchorMin = new Vector2(0, 1); ct.anchorMax = new Vector2(0, 1); ct.pivot = new Vector2(0, 1);
        ct.sizeDelta = new Vector2(size.x - 40, 100);
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 6; vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        var csf = content.GetComponent<ContentSizeFitter>(); csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sr = root.GetComponent<ScrollRect>(); sr.viewport = vp; sr.content = ct; sr.horizontal = false; sr.vertical = true;
        return (root, ct);
    }

    static GameObject CreateText(string name, Transform parent, Vector2 anchoredPos, Vector2 size, string content, int fontSize, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = size; rt.anchoredPosition = anchoredPos;
        var t = go.GetComponent<Text>(); t.text = content; t.fontSize = fontSize; t.alignment = align; t.color = Color.black;

        // Unity 6: LegacyRuntime.ttf 사용
        Font font = null;
        try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
        t.font = font;

        return go;
    }

    static GameObject CreatePlayerSlot(GameObject parent, Vector2 anchoredPos, string label)
    {
        var go = new GameObject(label.Replace(" ", ""), typeof(RectTransform), typeof(Image), typeof(PlayerSlotView));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(180, 90); rt.anchoredPosition = anchoredPos;
        go.GetComponent<Image>().color = new Color(1, 1, 1, 0.85f);

        var nameT = CreateText("Name", go.transform, new Vector2(10, -25), new Vector2(140, 30), label, 18, TextAnchor.MiddleLeft);
        var dotGO = new GameObject("ReadyDot", typeof(RectTransform), typeof(Image));
        dotGO.transform.SetParent(go.transform, false);
        var drt = dotGO.GetComponent<RectTransform>(); drt.sizeDelta = new Vector2(16, 16); drt.anchoredPosition = new Vector2(150, -25);
        dotGO.GetComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f, 1f);

        var view = go.GetComponent<PlayerSlotView>();
        view.NameText = nameT.GetComponent<Text>();
        view.ReadyDot = dotGO.GetComponent<Image>();
        return go;
    }

    static GameObject CreateRoomItemPrefab(string pfDir, out RoomItemView itemView)
    {
        var go = new GameObject("RoomItem", typeof(RectTransform), typeof(Image), typeof(RoomItemView));
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(460, 80);
        go.GetComponent<Image>().color = new Color(1, 1, 1, 0.85f);

        var title = CreateText("Title", go.transform, new Vector2(10, -20), new Vector2(280, 30), "title", 20, TextAnchor.MiddleLeft);
        var status = CreateText("Status", go.transform, new Vector2(10, -50), new Vector2(160, 24), "status", 16, TextAnchor.MiddleLeft);
        var curmax = CreateText("CurMax", go.transform, new Vector2(200, -50), new Vector2(80, 24), "0/2", 16, TextAnchor.MiddleLeft);
        var idText = CreateText("Id", go.transform, new Vector2(10, -5), new Vector2(260, 18), "r_xxxx", 12, TextAnchor.UpperLeft);

        // 별도 Join 버튼(원하면 숨겨도 됨)
        var join = CreateButton("Join", go.transform, new Vector2(380, -40), new Vector2(120, 46), "Join");

        var view = go.GetComponent<RoomItemView>();
        view.TitleText = title.GetComponent<Text>();
        view.StatusText = status.GetComponent<Text>();
        view.CurMaxText = curmax.GetComponent<Text>();
        view.IdText = idText.GetComponent<Text>();
        view.JoinButton = join.GetComponent<Button>();

        var path = $"{pfDir}/RoomItem.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        GameObject.DestroyImmediate(go);

        itemView = prefab.GetComponent<RoomItemView>();
        return prefab;
    }
}
#endif
