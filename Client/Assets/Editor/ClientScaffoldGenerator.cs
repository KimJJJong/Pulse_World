#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ClientScaffoldGenerator
{
    [MenuItem("RhythmRPG/Generate/Client Scaffold (Login+Town Tickets+TCP+Bind)")]
    public static void Generate()
    {
        EnsureFolders();
        EnsureAppConfigAsset();

        GenerateBootstrapScene();
        GenerateLoginScene();
        GenerateTownScene();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Done", "Scaffold generated: Login UX + Town Tickets UI + TCP connector.", "OK");
    }

    static void EnsureFolders()
    {
        void Mk(string p) { if (!AssetDatabase.IsValidFolder(p)) Directory.CreateDirectory(p); }
        Mk("Assets/ReFactory");
        Mk("Assets/ReFactory/Scenes");
        Mk("Assets/ReFactory/Resources");
    }

    static void EnsureAppConfigAsset()
    {
        var path = "Assets/ReFactory/Resources/AppConfig.asset";
        var asset = AssetDatabase.LoadAssetAtPath<AppConfig>(path);
        if (asset != null) return;

        asset = ScriptableObject.CreateInstance<AppConfig>();
        AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
    }

    static void GenerateBootstrapScene()
    {
        var scenePath = "Assets/ReFactory/Scenes/Bootstrap.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var go = new GameObject("AppBootstrap");
        var bootstrap = go.AddComponent<AppBootstrap>();
        var cfg = Resources.Load<AppConfig>("AppConfig");

        var so = new SerializedObject(bootstrap);
        so.FindProperty("config").objectReferenceValue = cfg;
        so.ApplyModifiedPropertiesWithoutUndo();

        // TcpConnector도 부팅 씬에 생성 (DontDestroy)
        new GameObject("TcpConnector").AddComponent<TcpConnector>();

        EditorSceneManager.SaveScene(scene, scenePath);
    }

    // -------------------- LOGIN --------------------

    static void GenerateLoginScene()
    {
        var scenePath = "Assets/ReFactory/Scenes/Login.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var canvasGo = CreateCanvas();
        EnsureEventSystem();

        var panel = CreatePanel(canvasGo.transform, "Panel", 760, 640);

        var title = CreateText(panel.transform, "Title", "Login", 52);
        SetRect(title.rectTransform, 0, 230, 680, 90);

        var didLabel = CreateText(panel.transform, "DeviceIdLabel", "DeviceId", 28);
        didLabel.alignment = TextAlignmentOptions.Left;
        SetRect(didLabel.rectTransform, -260, 130, 220, 50);

        var didValueBg = CreateBox(panel.transform, "DeviceIdBox", 460, 62);
        SetRect(didValueBg.GetComponent<RectTransform>(), 60, 130, 460, 62);

        var didValue = CreateText(didValueBg.transform, "DeviceIdText", "----", 26);
        didValue.alignment = TextAlignmentOptions.Left;
        didValue.color = Color.black;
        SetRect(didValue.rectTransform, 0, 0, 420, 50);

        var copyBtn = CreateButton(panel.transform, "CopyButton", "Copy", 26);
        SetRect(copyBtn.GetComponent<RectTransform>(), -160, 60, 220, 70);

        var resetBtn = CreateButton(panel.transform, "ResetButton", "Reset", 26);
        SetRect(resetBtn.GetComponent<RectTransform>(), 160, 60, 220, 70);

        var loginBtn = CreateButton(panel.transform, "LoginButton", "Guest Login", 34);
        SetRect(loginBtn.GetComponent<RectTransform>(), 0, -40, 660, 96);

        var err = CreateText(panel.transform, "ErrorText", "", 24);
        err.color = new Color(1f, 0.35f, 0.35f, 1f);
        SetRect(err.rectTransform, 0, -140, 660, 90);
        err.gameObject.SetActive(false);

        var busy = CreateText(panel.transform, "Busy", "Loading...", 24);
        SetRect(busy.rectTransform, 0, -210, 660, 60);
        busy.gameObject.SetActive(false);

        var popup = CreateConfirmPopup(canvasGo.transform);

        var rootGo = new GameObject("LoginScreenRoot");
        rootGo.transform.SetParent(canvasGo.transform, false);

        var view = rootGo.AddComponent<LoginView>();
        var screen = rootGo.AddComponent<LoginScreen>();

        view.DeviceIdText = didValue;
        view.CopyDeviceIdButton = copyBtn.GetComponent<Button>();
        view.ResetDeviceIdButton = resetBtn.GetComponent<Button>();
        view.LoginButton = loginBtn.GetComponent<Button>();
        view.ErrorText = err;
        view.Busy = busy.gameObject;

        {
            var so = new SerializedObject(screen);
            so.FindProperty("view").objectReferenceValue = view;
            so.FindProperty("confirm").objectReferenceValue = popup;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.SaveScene(scene, scenePath);
    }

    // -------------------- TOWN --------------------

    static void GenerateTownScene()
    {
        var scenePath = "Assets/ReFactory/Scenes/Town.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var canvasGo = CreateCanvas();
        EnsureEventSystem();

        // Header
        var header = CreateText(canvasGo.transform, "Header", "Town / Session Tickets", 44);
        SetRect(header.rectTransform, 0, 430, 1200, 90);

        // Left: Town ticket card
        var townCard = CreatePanel(canvasGo.transform, "TownCard", 860, 520);
        SetRect(townCard.GetComponent<RectTransform>(), -470, 40, 860, 520);

        var townTitle = CreateText(townCard.transform, "TownTitle", "Town Ticket", 36);
        SetRect(townTitle.rectTransform, 0, 200, 760, 70);

        var townRegion = CreateInputRow(townCard.transform, "TownRegion", "PreferredRegion", "kr", y: 120);
        var townIssueBtn = CreateButton(townCard.transform, "TownIssueButton", "Issue Town Ticket", 28);
        SetRect(townIssueBtn.GetComponent<RectTransform>(), 0, 40, 760, 84);

        var townTicketId = CreateText(townCard.transform, "TownTicketId", "TicketId: -", 24);
        townTicketId.alignment = TextAlignmentOptions.Left;
        SetRect(townTicketId.rectTransform, 0, -40, 760, 40);

        var townExpire = CreateText(townCard.transform, "TownExpire", "ExpireAtMs: -", 24);
        townExpire.alignment = TextAlignmentOptions.Left;
        SetRect(townExpire.rectTransform, 0, -90, 760, 40);

        var townEndpoint = CreateText(townCard.transform, "TownEndpoint", "Endpoint: -", 24);
        townEndpoint.alignment = TextAlignmentOptions.Left;
        SetRect(townEndpoint.rectTransform, 0, -140, 760, 40);

        var townConnectBtn = CreateButton(townCard.transform, "TownConnectButton", "Connect (TCP)", 28);
        SetRect(townConnectBtn.GetComponent<RectTransform>(), 0, -220, 760, 84);

        // Right: Game ticket card
        var gameCard = CreatePanel(canvasGo.transform, "GameCard", 860, 520);
        SetRect(gameCard.GetComponent<RectTransform>(), 470, 40, 860, 520);

        var gameTitle = CreateText(gameCard.transform, "GameTitle", "Game Ticket", 36);
        SetRect(gameTitle.rectTransform, 0, 200, 760, 70);

        var gameRegion = CreateInputRow(gameCard.transform, "GameRegion", "PreferredRegion", "kr", y: 140);
        var gameRoom = CreateInputRow(gameCard.transform, "GameRoomId", "RoomId", "room-1", y: 80);
        var gameMap = CreateInputRow(gameCard.transform, "GameMap", "Map", "map_01", y: 20);
        var gameMax = CreateInputRow(gameCard.transform, "GameMaxPlayers", "MaxPlayers", "2", y: -40);

        var gameIssueBtn = CreateButton(gameCard.transform, "GameIssueButton", "Issue Game Ticket", 28);
        SetRect(gameIssueBtn.GetComponent<RectTransform>(), 0, -120, 760, 84);

        var gameTransitionId = CreateText(gameCard.transform, "GameTransitionId", "TransitionId: -", 22);
        gameTransitionId.alignment = TextAlignmentOptions.Left;
        SetRect(gameTransitionId.rectTransform, 0, -200, 760, 34);

        var gameTicketId = CreateText(gameCard.transform, "GameTicketId", "TicketId: -", 22);
        gameTicketId.alignment = TextAlignmentOptions.Left;
        SetRect(gameTicketId.rectTransform, 0, -240, 760, 34);

        var gameExpire = CreateText(gameCard.transform, "GameExpire", "ExpireAtMs: -", 22);
        gameExpire.alignment = TextAlignmentOptions.Left;
        SetRect(gameExpire.rectTransform, 0, -280, 760, 34);

        var gameServerId = CreateText(gameCard.transform, "GameServerId", "ServerId: -", 22);
        gameServerId.alignment = TextAlignmentOptions.Left;
        SetRect(gameServerId.rectTransform, 0, -320, 760, 34);

        var gameEndpoint = CreateText(gameCard.transform, "GameEndpoint", "Endpoint: -", 22);
        gameEndpoint.alignment = TextAlignmentOptions.Left;
        SetRect(gameEndpoint.rectTransform, 0, -360, 760, 34);

        var gameKey = CreateText(gameCard.transform, "GameKey", "Key: -", 22);
        gameKey.alignment = TextAlignmentOptions.Left;
        SetRect(gameKey.rectTransform, 0, -400, 760, 34);

        var gameConnectBtn = CreateButton(gameCard.transform, "GameConnectButton", "Connect (TCP)", 28);
        SetRect(gameConnectBtn.GetComponent<RectTransform>(), 0, -460, 760, 84);

        // Footer: status + busy
        var status = CreateText(canvasGo.transform, "Status", "", 26);
        status.color = new Color(1f, 1f, 1f, 1f);
        status.alignment = TextAlignmentOptions.Center;
        SetRect(status.rectTransform, 0, -420, 1600, 70);
        status.gameObject.SetActive(false);

        var busy = CreateText(canvasGo.transform, "Busy", "Loading...", 24);
        SetRect(busy.rectTransform, 0, -470, 1000, 60);
        busy.gameObject.SetActive(false);

        // Screen root
        var rootGo = new GameObject("TownScreenRoot");
        rootGo.transform.SetParent(canvasGo.transform, false);

        var view = rootGo.AddComponent<TownView>();
        var screen = rootGo.AddComponent<TownScreen>();

        // bind view fields
        view.TownPreferredRegion = townRegion;
        view.TownIssueButton = townIssueBtn.GetComponent<Button>();
        view.TownTicketIdText = townTicketId;
        view.TownExpireText = townExpire;
        view.TownEndpointText = townEndpoint;
        view.TownConnectButton = townConnectBtn.GetComponent<Button>();

        view.GamePreferredRegion = gameRegion;
        view.GameRoomId = gameRoom;
        view.GameMap = gameMap;
        view.GameMaxPlayers = gameMax;
        view.GameIssueButton = gameIssueBtn.GetComponent<Button>();
        view.GameTransitionIdText = gameTransitionId;
        view.GameTicketIdText = gameTicketId;
        view.GameExpireText = gameExpire;
        view.GameServerIdText = gameServerId;
        view.GameEndpointText = gameEndpoint;
        view.GameKeyText = gameKey;
        view.GameConnectButton = gameConnectBtn.GetComponent<Button>();

        view.StatusText = status;
        view.Busy = busy.gameObject;

        // inject serialized ref
        {
            var so = new SerializedObject(screen);
            so.FindProperty("view").objectReferenceValue = view;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.SaveScene(scene, scenePath);
    }

    // -------------------- UI helpers --------------------

    static GameObject CreateCanvas()
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        return canvasGo;
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    static GameObject CreatePanel(Transform parent, string name, float w, float h)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.60f);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;

        return go;
    }

    static GameObject CreateBox(Transform parent, string name, float w, float h)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.95f);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;

        return go;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string text, int size)
    {
        var go = new GameObject(name, typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;

        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(600, 80);
        rt.anchoredPosition = Vector2.zero;

        return t;
    }

    static GameObject CreateButton(Transform parent, string name, string label, int fontSize)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.2f, 0.55f, 1.0f, 1f);

        var text = CreateText(go.transform, "Label", label, fontSize);
        text.color = Color.white;
        SetRect(text.rectTransform, 0, 0, 520, 60);

        return go;
    }

    static TMP_InputField CreateInput(Transform parent, string name, string placeholder, string initial)
    {
        var root = new GameObject(name, typeof(Image), typeof(TMP_InputField));
        root.transform.SetParent(parent, false);

        var bg = root.GetComponent<Image>();
        bg.color = new Color(1, 1, 1, 0.95f);

        var ph = CreateText(root.transform, "Placeholder", placeholder, 22);
        ph.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        ph.alignment = TextAlignmentOptions.Left;
        SetRect(ph.rectTransform, 0, 0, 520, 44);

        var txt = CreateText(root.transform, "Text", initial, 22);
        txt.color = Color.black;
        txt.alignment = TextAlignmentOptions.Left;
        SetRect(txt.rectTransform, 0, 0, 520, 44);

        var input = root.GetComponent<TMP_InputField>();
        input.textComponent = txt;
        input.placeholder = ph;
        input.text = initial;

        return input;
    }

    static TMP_InputField CreateInputRow(Transform parent, string name, string label, string initial, float y)
    {
        var row = new GameObject(name);
        row.transform.SetParent(parent, false);

        var labelText = CreateText(row.transform, "Label", label, 22);
        labelText.alignment = TextAlignmentOptions.Left;
        SetRect(labelText.rectTransform, -250, 0, 260, 44);

        var input = CreateInput(row.transform, "Input", label, initial);
        var rt = input.GetComponent<RectTransform>();
        SetRect(rt, 160, 0, 520, 58);

        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = new Vector2(760, 70);
        rowRt.anchoredPosition = new Vector2(0, y);

        return input;
    }

    static void SetRect(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    // ----- ConfirmPopup (Login reset confirm) -----

    static ConfirmPopup CreateConfirmPopup(Transform parent)
    {
        var dim = new GameObject("ConfirmPopup", typeof(Image));
        dim.transform.SetParent(parent, false);
        dim.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);

        var dimRt = dim.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;

        var card = CreatePanel(dim.transform, "Card", 720, 420);
        card.GetComponent<Image>().color = new Color(0, 0, 0, 0.80f);

        var title = CreateText(card.transform, "Title", "Title", 40);
        SetRect(title.rectTransform, 0, 120, 640, 70);

        var msg = CreateText(card.transform, "Message", "Message", 26);
        msg.alignment = TextAlignmentOptions.Center;
        SetRect(msg.rectTransform, 0, 20, 640, 150);

        var okBtn = CreateButton(card.transform, "OkButton", "OK", 28);
        SetRect(okBtn.GetComponent<RectTransform>(), -140, -130, 260, 80);

        var cancelBtn = CreateButton(card.transform, "CancelButton", "Cancel", 28);
        SetRect(cancelBtn.GetComponent<RectTransform>(), 140, -130, 260, 80);

        var popup = dim.AddComponent<ConfirmPopup>();
        popup.Title = title;
        popup.Message = msg;
        popup.OkButton = okBtn.GetComponent<Button>();
        popup.CancelButton = cancelBtn.GetComponent<Button>();

        dim.SetActive(false);
        return popup;
    }
}
#endif
