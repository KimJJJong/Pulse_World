using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GameDeathSpectatorHud : MonoBehaviour
{
    private const int SortingOrder = 31950;
    private const int MaxTargetButtons = 8;

    private static readonly Color DimColor = new(0f, 0.01f, 0.018f, 0.58f);
    private static readonly Color PanelColor = new(0.025f, 0.075f, 0.095f, 0.88f);
    private static readonly Color PanelLine = new(0.28f, 0.96f, 1f, 0.34f);
    private static readonly Color TextMain = new(0.92f, 0.99f, 1f, 1f);
    private static readonly Color TextSoft = new(0.72f, 0.88f, 0.90f, 1f);
    private static readonly Color Gold = new(0.95f, 0.69f, 0.29f, 1f);
    private static readonly Color Cyan = new(0.28f, 0.96f, 1f, 1f);
    private static readonly Color ButtonFill = new(0.055f, 0.18f, 0.20f, 0.92f);

    private static GameDeathSpectatorHud _instance;

    private Canvas _canvas;
    private CanvasGroup _group;
    private RectTransform _root;
    private RectTransform _spectatorPanel;
    private RectTransform _wipePanel;
    private RectTransform _targetListRoot;
    private TextMeshProUGUI _spectatorTitle;
    private TextMeshProUGUI _spectatorStatus;
    private TextMeshProUGUI _wipeStatus;
    private Button _previousButton;
    private Button _nextButton;
    private Button _returnTownButton;
    private Button _retryButton;

    private readonly List<Button> _targetButtons = new();
    private readonly List<TextMeshProUGUI> _targetLabels = new();
    private readonly List<int> _aliveTargets = new();
    private int _observedActorId;
    private bool _subscribed;
    private ClientGameState _subscribedState;

    public static GameDeathSpectatorHud EnsureInScene()
    {
        if (_instance != null)
        {
            _instance.EnsureRuntimeReady();
            return _instance;
        }

        var go = new GameObject(nameof(GameDeathSpectatorHud));
        if (Application.isPlaying)
            DontDestroyOnLoad(go);

        _instance = go.AddComponent<GameDeathSpectatorHud>();
        _instance.EnsureRuntimeReady();
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        EnsureRuntimeReady();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Subscribe();
        RefreshState();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Unsubscribe();
    }

    private void Update()
    {
        if (!_subscribed)
            Subscribe();

        if (_group == null || _group.alpha <= 0.01f)
            return;

        if (_spectatorPanel != null && _spectatorPanel.gameObject.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Q))
                SelectRelativeTarget(-1);
            if (Input.GetKeyDown(KeyCode.E))
                SelectRelativeTarget(1);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Unsubscribe();
        if (_instance == this)
            _instance = null;
    }

    private void EnsureRuntimeReady()
    {
        EnsureEventSystem();

        if (_canvas == null)
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
                _canvas = gameObject.AddComponent<Canvas>();
        }

        ConfigureCanvas(_canvas);

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        if (_group == null)
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null)
                _group = gameObject.AddComponent<CanvasGroup>();
        }

        if (_root == null)
            BuildUi();
    }

    private void Subscribe()
    {
        var gs = ClientGameState.Instance;
        if (gs == null)
            return;

        if (_subscribed && _subscribedState == gs)
            return;

        if (_subscribed && _subscribedState != gs)
            Unsubscribe();

        gs.MyEntityChanged += HandleMyEntityChanged;
        gs.EntityChanged += HandleEntityChanged;
        gs.EntityRemoved += HandleEntityRemoved;
        gs.PartyStateChanged += HandlePartyStateChanged;
        gs.EntitiesCleared += HandleEntitiesCleared;
        _subscribedState = gs;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;

        var gs = _subscribedState != null ? _subscribedState : ClientGameState.Instance;
        if (gs != null)
        {
            gs.MyEntityChanged -= HandleMyEntityChanged;
            gs.EntityChanged -= HandleEntityChanged;
            gs.EntityRemoved -= HandleEntityRemoved;
            gs.PartyStateChanged -= HandlePartyStateChanged;
            gs.EntitiesCleared -= HandleEntitiesCleared;
        }

        _subscribedState = null;
        _subscribed = false;
    }

    private void BuildUi()
    {
        _root = CreateRect("Root", transform);
        Stretch(_root);

        var dim = CreateImage("Dim", _root, DimColor);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        _spectatorPanel = CreatePanel("SpectatorPanel", _root, new Vector2(760f, 250f), new Vector2(0.5f, 0f), new Vector2(0f, 80f));
        _spectatorTitle = CreateText("Title", _spectatorPanel, "전투 불능", 32f, FontStyles.Bold, TextAlignmentOptions.Center, TextMain);
        Anchor(_spectatorTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(600f, 44f), new Vector2(0.5f, 0.5f));

        _spectatorStatus = CreateText("Status", _spectatorPanel, "", 20f, FontStyles.Normal, TextAlignmentOptions.Center, TextSoft);
        Anchor(_spectatorStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(660f, 36f), new Vector2(0.5f, 0.5f));

        _previousButton = BuildButton(_spectatorPanel, "Button_Previous", "이전", Gold, new Vector2(-290f, -48f), new Vector2(136f, 48f), () => SelectRelativeTarget(-1));
        _nextButton = BuildButton(_spectatorPanel, "Button_Next", "다음", Gold, new Vector2(290f, -48f), new Vector2(136f, 48f), () => SelectRelativeTarget(1));

        _targetListRoot = CreateRect("TargetList", _spectatorPanel);
        Anchor(_targetListRoot, new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(560f, 70f), new Vector2(0.5f, 0f));
        BuildTargetButtons();

        _wipePanel = CreatePanel("PartyWipePanel", _root, new Vector2(700f, 330f), new Vector2(0.5f, 0.5f), Vector2.zero);
        var wipeTitle = CreateText("Title", _wipePanel, "파티 전멸", 44f, FontStyles.Bold, TextAlignmentOptions.Center, TextMain);
        Anchor(wipeTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(520f, 64f), new Vector2(0.5f, 0.5f));

        _wipeStatus = CreateText("Status", _wipePanel, "모든 플레이어가 쓰러졌습니다.", 22f, FontStyles.Normal, TextAlignmentOptions.Center, TextSoft);
        Anchor(_wipeStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(560f, 44f), new Vector2(0.5f, 0.5f));

        _retryButton = BuildButton(_wipePanel, "Button_Retry", "다시 시도", Cyan, new Vector2(-170f, -98f), new Vector2(250f, 64f), HandleRetryClicked);
        _returnTownButton = BuildButton(_wipePanel, "Button_ReturnTown", "마을로", Gold, new Vector2(170f, -98f), new Vector2(250f, 64f), HandleReturnTownClicked);

        Hide();
    }

    private void BuildTargetButtons()
    {
        for (int i = 0; i < MaxTargetButtons; i++)
        {
            int slot = i;
            float width = 128f;
            float x = (i % 4 - 1.5f) * (width + 12f);
            float y = i < 4 ? 42f : 6f;
            var button = BuildButton(_targetListRoot, $"Target_{i}", "-", Cyan, new Vector2(x, y), new Vector2(width, 30f), () => SelectTargetSlot(slot));
            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            _targetButtons.Add(button);
            _targetLabels.Add(label);
        }
    }

    private void RefreshState()
    {
        Subscribe();

        if (!IsGameScene())
        {
            Hide();
            return;
        }

        var gs = ClientGameState.Instance;
        if (gs == null || !gs.TryGetMyEntity(out var me) || me.EntityType != (int)EntityType.Player)
        {
            Hide();
            return;
        }

        if (me.Hp > 0)
        {
            _observedActorId = 0;
            BindCameraToActor(gs.MyActorId);
            Hide();
            return;
        }

        CollectAliveTargets(gs);
        if (_aliveTargets.Count == 0)
        {
            ShowPartyWipe();
            return;
        }

        if (!_aliveTargets.Contains(_observedActorId))
            _observedActorId = _aliveTargets[0];

        ShowSpectator();
        BindCameraToActor(_observedActorId);
    }

    private void CollectAliveTargets(ClientGameState gs)
    {
        _aliveTargets.Clear();
        var seen = new HashSet<int>();

        if (gs.PlayerActorIds != null)
        {
            for (int i = 0; i < gs.PlayerActorIds.Length; i++)
                AddAliveTarget(gs, gs.PlayerActorIds[i], seen, _aliveTargets);
        }

        foreach (var roster in gs.EnumeratePlayerRoster())
            AddAliveTarget(gs, roster.ActorId, seen, _aliveTargets);

        foreach (var entity in gs.EnumerateEntities())
        {
            if (entity.EntityType == (int)EntityType.Player)
                AddAliveTarget(gs, entity.EntityId, seen, _aliveTargets);
        }

        _aliveTargets.Sort();
    }

    private static void AddAliveTarget(ClientGameState gs, int actorId, HashSet<int> seen, List<int> targets)
    {
        if (actorId <= 0 || actorId == gs.MyActorId || !seen.Add(actorId))
            return;

        if (gs.TryGetEntity(actorId, out var info)
            && info.EntityType == (int)EntityType.Player
            && info.Hp > 0)
        {
            targets.Add(actorId);
        }
    }

    private void ShowSpectator()
    {
        SetVisible(true);
        _spectatorPanel.gameObject.SetActive(true);
        _wipePanel.gameObject.SetActive(false);

        var gs = ClientGameState.Instance;
        string label = ResolvePlayerName(gs, _observedActorId);
        _spectatorStatus.text = $"관전 중: {label}";
        _spectatorTitle.text = "전투 불능 - 관전";

        for (int i = 0; i < _targetButtons.Count; i++)
        {
            bool visible = i < _aliveTargets.Count;
            _targetButtons[i].gameObject.SetActive(visible);
            if (!visible)
                continue;

            int actorId = _aliveTargets[i];
            _targetLabels[i].text = ResolvePlayerName(gs, actorId);
            _targetLabels[i].color = actorId == _observedActorId ? Gold : TextMain;
            _targetButtons[i].interactable = actorId != _observedActorId;
        }

        bool canCycle = _aliveTargets.Count > 1;
        _previousButton.interactable = canCycle;
        _nextButton.interactable = canCycle;
    }

    private void ShowPartyWipe()
    {
        SetVisible(true);
        _spectatorPanel.gameObject.SetActive(false);
        _wipePanel.gameObject.SetActive(true);
        _observedActorId = 0;

        bool canRetry = ClientFlow.Instance != null && ClientFlow.Instance.CanRetryCurrentGame;
        _retryButton.interactable = canRetry;
        _wipeStatus.text = canRetry
            ? "모든 플레이어가 쓰러졌습니다. 다시 도전하거나 마을로 돌아가세요."
            : "모든 플레이어가 쓰러졌습니다. 현재 세션은 마을로 돌아갈 수 있습니다.";
    }

    private void Hide()
    {
        SetVisible(false);
        if (_spectatorPanel != null)
            _spectatorPanel.gameObject.SetActive(false);
        if (_wipePanel != null)
            _wipePanel.gameObject.SetActive(false);
    }

    private void SetVisible(bool visible)
    {
        if (_group == null)
            return;

        gameObject.SetActive(true);
        _group.alpha = visible ? 1f : 0f;
        _group.blocksRaycasts = visible;
        _group.interactable = visible;
        transform.SetAsLastSibling();
    }

    private void SelectRelativeTarget(int delta)
    {
        if (_aliveTargets.Count == 0)
            return;

        int index = Mathf.Max(0, _aliveTargets.IndexOf(_observedActorId));
        index = (index + delta + _aliveTargets.Count) % _aliveTargets.Count;
        _observedActorId = _aliveTargets[index];
        RefreshState();
    }

    private void SelectTargetSlot(int slot)
    {
        if ((uint)slot >= (uint)_aliveTargets.Count)
            return;

        _observedActorId = _aliveTargets[slot];
        RefreshState();
    }

    private static bool BindCameraToActor(int actorId)
    {
        if (actorId <= 0 || BoardView.Instance == null)
            return false;

        if (!BoardView.Instance.TryGetEntityView(actorId, out var visual) || visual == null)
            return false;

        CameraBinder.Instance?.Bind(visual.transform);
        return true;
    }

    private static string ResolvePlayerName(ClientGameState gs, int actorId)
    {
        if (gs == null || actorId <= 0)
            return "-";

        if (gs.TryGetPlayerUid(actorId, out var uid) && !string.IsNullOrWhiteSpace(uid))
            return TrimLabel(uid);

        if (actorId == gs.MyActorId
            && SessionContext.Instance != null
            && !string.IsNullOrWhiteSpace(SessionContext.Instance.Uid))
        {
            return TrimLabel(SessionContext.Instance.Uid);
        }

        return $"Player {actorId}";
    }

    private static string TrimLabel(string label)
    {
        var clean = label.Trim();
        return clean.Length <= 13 ? clean : $"{clean.Substring(0, 11)}..";
    }

    private static bool IsGameScene()
    {
        var sceneName = SceneManager.GetActiveScene().name;
        return sceneName.StartsWith("Game", System.StringComparison.OrdinalIgnoreCase);
    }

    private void HandleRetryClicked()
    {
        _wipeStatus.text = "다시 시도 중...";
        _retryButton.interactable = false;
        _returnTownButton.interactable = false;
        ClientFlow.Instance?.RetryCurrentGame();
    }

    private void HandleReturnTownClicked()
    {
        _wipeStatus.text = "마을로 돌아가는 중...";
        _retryButton.interactable = false;
        _returnTownButton.interactable = false;
        ClientFlow.Instance?.ReturnToTown();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsGameScene())
            Hide();
        else
            RefreshState();
    }

    private void HandleMyEntityChanged(ClientEntityInfo _) => RefreshState();

    private void HandleEntityChanged(ClientEntityInfo info)
    {
        if (info.EntityType == (int)EntityType.Player)
            RefreshState();
    }

    private void HandleEntityRemoved(int _) => RefreshState();
    private void HandlePartyStateChanged() => RefreshState();
    private void HandleEntitiesCleared() => Hide();

    private static RectTransform CreatePanel(string name, Transform parent, Vector2 size, Vector2 anchor, Vector2 anchoredPosition)
    {
        var rect = CreateRect(name, parent);
        Anchor(rect, anchor, anchoredPosition, size, anchor);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = PanelColor;
        image.raycastTarget = true;
        var outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = PanelLine;
        outline.effectDistance = new Vector2(2f, -2f);
        return rect;
    }

    private static Button BuildButton(RectTransform parent, string name, string label, Color labelColor, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var image = CreateImage(name, parent, ButtonFill);
        Anchor(image.rectTransform, new Vector2(0.5f, 0.5f), anchoredPosition, size, new Vector2(0.5f, 0.5f));
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        var text = CreateText("Label", image.rectTransform, label, 20f, FontStyles.Normal, TextAlignmentOptions.Center, labelColor);
        Stretch(text.rectTransform);
        return button;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void ConfigureCanvas(Canvas canvas)
    {
        if (canvas == null)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private static void EnsureEventSystem()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
            return;
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }
}
