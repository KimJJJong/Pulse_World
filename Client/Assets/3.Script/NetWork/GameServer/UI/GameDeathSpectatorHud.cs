using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(GraphicRaycaster))]
public sealed class GameDeathSpectatorHud : MonoBehaviour
{
    private const int SortingOrder = 31950;
    private const int MaxTargetButtons = 8;
    private const int DecoyEntityIdBase = 700000;
    private const int DecoyEntityIdLimit = DecoyEntityIdBase + 100000;
    private const string ResourcePrefabPath = "UI/GameDeathSpectatorHud";
    // Runtime prefab fallback path used when no scene-placed HUD exists.
    private static readonly Color DimColor = new(0f, 0.01f, 0.018f, 0.58f);
    private static readonly Color PanelColor = new(0.025f, 0.075f, 0.095f, 0.88f);
    private static readonly Color PanelLine = new(0.28f, 0.96f, 1f, 0.34f);
    private static readonly Color TextMain = new(0.92f, 0.99f, 1f, 1f);
    private static readonly Color TextSoft = new(0.72f, 0.88f, 0.90f, 1f);
    private static readonly Color TextDisabled = new(0.42f, 0.58f, 0.61f, 1f);
    private static readonly Color Gold = new(0.95f, 0.69f, 0.29f, 1f);
    private static readonly Color Cyan = new(0.28f, 0.96f, 1f, 1f);
    private static readonly Color ButtonFill = new(0.055f, 0.18f, 0.20f, 0.92f);
    private static readonly Color SelectedButtonFill = new(0.15f, 0.26f, 0.18f, 0.96f);
    private static readonly Color DisabledButtonFill = new(0.035f, 0.06f, 0.07f, 0.76f);

    private static GameDeathSpectatorHud _instance;
    private static TMP_FontAsset _koreanFont;

    [Header("Placed UI")]
    [SerializeField] private bool _autoBuildMissingUi = true;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _root;
    [SerializeField] private RectTransform _spectatorPanel;
    [SerializeField] private RectTransform _wipePanel;
    [SerializeField] private RectTransform _targetListRoot;
    [SerializeField] private TextMeshProUGUI _spectatorTitle;
    [SerializeField] private TextMeshProUGUI _spectatorStatus;
    [SerializeField] private TextMeshProUGUI _wipeStatus;
    [SerializeField] private Button _previousButton;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _returnTownButton;
    [SerializeField] private Button _retryButton;

    private readonly List<Button> _targetButtons = new();
    private readonly List<TextMeshProUGUI> _targetLabels = new();
    private readonly List<int> _partyTargets = new();
    private readonly List<int> _aliveTargets = new();
    private int _observedActorId;
    private bool _subscribed;
    private ClientGameState _subscribedState;
    private Coroutine _refreshRoutine;
    private float _nextPassiveRefreshAt;
    private bool _buttonListenersWired;

    public static GameDeathSpectatorHud EnsureInScene()
    {
        if (_instance != null)
        {
            _instance.EnsureRuntimeReady();
            return _instance;
        }

        _instance = FindPlacedInstance();
        if (_instance != null)
        {
            _instance.gameObject.SetActive(true);
            _instance.EnsureRuntimeReady();
            return _instance;
        }

        GameObject prefab = Resources.Load<GameObject>(ResourcePrefabPath);
        GameObject go = prefab != null && prefab.GetComponent<GameDeathSpectatorHud>() != null
            ? Instantiate(prefab)
            : new GameObject(nameof(GameDeathSpectatorHud));

        go.name = nameof(GameDeathSpectatorHud);
        if (Application.isPlaying)
            DontDestroyOnLoad(go);

        _instance = go.GetComponent<GameDeathSpectatorHud>();
        if (_instance == null)
            _instance = go.AddComponent<GameDeathSpectatorHud>();
        _instance.EnsureRuntimeReady();
        return _instance;
    }

    private static GameDeathSpectatorHud FindPlacedInstance()
    {
        var candidates = Resources.FindObjectsOfTypeAll<GameDeathSpectatorHud>();
        foreach (var candidate in candidates)
        {
            if (candidate == null)
                continue;

            var scene = candidate.gameObject.scene;
            if (scene.IsValid())
                return candidate;
        }

        return null;
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
        RequestRefresh();
    }

    private void OnDisable()
    {
        CancelPendingRefresh();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Unsubscribe();
    }

    private void Update()
    {
        if (!_subscribed)
            Subscribe();

        if (_group == null || _group.alpha <= 0.01f)
        {
            if (Time.unscaledTime >= _nextPassiveRefreshAt)
            {
                _nextPassiveRefreshAt = Time.unscaledTime + 0.25f;
                RefreshState();
            }

            return;
        }

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

        BindPlacedReferences();

        if (_root == null)
        {
            if (!_autoBuildMissingUi)
                return;

            BuildUi();
        }

        BindPlacedReferences();
        WireButtonListeners();
    }

    [ContextMenu("Rebuild Placed UI")]
    public void RebuildPlacedUiForEditor()
    {
        _canvas = GetComponent<Canvas>();
        if (_canvas == null)
            _canvas = gameObject.AddComponent<Canvas>();
        ConfigureCanvas(_canvas);

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        _group = GetComponent<CanvasGroup>();
        if (_group == null)
            _group = gameObject.AddComponent<CanvasGroup>();

        ClearChildren(transform);
        _root = null;
        _spectatorPanel = null;
        _wipePanel = null;
        _targetListRoot = null;
        _spectatorTitle = null;
        _spectatorStatus = null;
        _wipeStatus = null;
        _previousButton = null;
        _nextButton = null;
        _returnTownButton = null;
        _retryButton = null;
        _targetButtons.Clear();
        _targetLabels.Clear();
        _buttonListenersWired = false;

        BuildUi();
        BindPlacedReferences();
        WireButtonListeners();
        ShowEditPreview();
        ConfigureCanvasRect(GetComponent<RectTransform>());
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
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

        _spectatorPanel = CreatePanel("SpectatorPanel", _root, new Vector2(820f, 320f), new Vector2(0.5f, 0f), new Vector2(0f, 70f));
        _spectatorTitle = CreateText("Title", _spectatorPanel, "전투 불능", 32f, FontStyles.Bold, TextAlignmentOptions.Center, TextMain);
        Anchor(_spectatorTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(600f, 44f), new Vector2(0.5f, 0.5f));

        _spectatorStatus = CreateText("Status", _spectatorPanel, "", 20f, FontStyles.Normal, TextAlignmentOptions.Center, TextSoft);
        Anchor(_spectatorStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(660f, 36f), new Vector2(0.5f, 0.5f));

        _previousButton = BuildButton(_spectatorPanel, "Button_Previous", "이전", Gold, new Vector2(-290f, 38f), new Vector2(136f, 46f), null, new Vector2(0.5f, 0f));
        _nextButton = BuildButton(_spectatorPanel, "Button_Next", "다음", Gold, new Vector2(290f, 38f), new Vector2(136f, 46f), null, new Vector2(0.5f, 0f));

        _targetListRoot = CreateRect("TargetList", _spectatorPanel);
        Anchor(_targetListRoot, new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(680f, 96f), new Vector2(0.5f, 0.5f));
        BuildTargetButtons();

        _wipePanel = CreatePanel("PartyWipePanel", _root, new Vector2(700f, 330f), new Vector2(0.5f, 0.5f), Vector2.zero);
        var wipeTitle = CreateText("Title", _wipePanel, "파티 전멸", 44f, FontStyles.Bold, TextAlignmentOptions.Center, TextMain);
        Anchor(wipeTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(520f, 64f), new Vector2(0.5f, 0.5f));

        _wipeStatus = CreateText("Status", _wipePanel, "모든 플레이어가 쓰러졌습니다.", 22f, FontStyles.Normal, TextAlignmentOptions.Center, TextSoft);
        Anchor(_wipeStatus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(560f, 44f), new Vector2(0.5f, 0.5f));

        _retryButton = BuildButton(_wipePanel, "Button_Retry", "다시 시도", Cyan, new Vector2(-170f, -98f), new Vector2(250f, 64f), null);
        _returnTownButton = BuildButton(_wipePanel, "Button_ReturnTown", "마을로", Gold, new Vector2(170f, -98f), new Vector2(250f, 64f), null);

        if (Application.isPlaying)
            Hide();
        else
            ShowEditPreview();
    }

    private void BuildTargetButtons()
    {
        for (int i = 0; i < MaxTargetButtons; i++)
        {
            float width = 154f;
            float x = (i % 4 - 1.5f) * (width + 12f);
            float y = i < 4 ? 25f : -25f;
            var button = BuildButton(_targetListRoot, $"Target_{i}", "-", Cyan, new Vector2(x, y), new Vector2(width, 40f), null);
            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            label.fontSize = 15f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = 15f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            _targetButtons.Add(button);
            _targetLabels.Add(label);
        }
    }

    private void BindPlacedReferences()
    {
        if (_root == null)
            _root = transform.Find("Root") as RectTransform;
        if (_root == null)
            return;

        if (_spectatorPanel == null)
            _spectatorPanel = _root.Find("SpectatorPanel") as RectTransform;
        if (_wipePanel == null)
            _wipePanel = _root.Find("PartyWipePanel") as RectTransform;

        if (_spectatorPanel != null)
        {
            if (_spectatorTitle == null)
                _spectatorTitle = FindChild<TextMeshProUGUI>(_spectatorPanel, "Title");
            if (_spectatorStatus == null)
                _spectatorStatus = FindChild<TextMeshProUGUI>(_spectatorPanel, "Status");
            if (_previousButton == null)
                _previousButton = FindChild<Button>(_spectatorPanel, "Button_Previous");
            if (_nextButton == null)
                _nextButton = FindChild<Button>(_spectatorPanel, "Button_Next");
            if (_targetListRoot == null)
                _targetListRoot = _spectatorPanel.Find("TargetList") as RectTransform;
        }

        if (_wipePanel != null)
        {
            if (_wipeStatus == null)
                _wipeStatus = FindChild<TextMeshProUGUI>(_wipePanel, "Status");
            if (_retryButton == null)
                _retryButton = FindChild<Button>(_wipePanel, "Button_Retry");
            if (_returnTownButton == null)
                _returnTownButton = FindChild<Button>(_wipePanel, "Button_ReturnTown");
        }

        BindTargetButtons();
        ApplyPreferredFont(_spectatorTitle);
        ApplyPreferredFont(_spectatorStatus);
        ApplyPreferredFont(_wipeStatus);
    }

    private void BindTargetButtons()
    {
        if (_targetListRoot == null)
            return;

        _targetButtons.Clear();
        _targetLabels.Clear();
        for (int i = 0; i < MaxTargetButtons; i++)
        {
            var button = FindChild<Button>(_targetListRoot, $"Target_{i}");
            if (button == null)
                continue;

            _targetButtons.Add(button);
            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                ApplyPreferredFont(label);
            _targetLabels.Add(label);
        }
    }

    private void WireButtonListeners()
    {
        if (_buttonListenersWired && Application.isPlaying)
            return;

        if (_previousButton != null)
        {
            _previousButton.onClick.RemoveAllListeners();
            _previousButton.onClick.AddListener(HandlePreviousClicked);
        }

        if (_nextButton != null)
        {
            _nextButton.onClick.RemoveAllListeners();
            _nextButton.onClick.AddListener(HandleNextClicked);
        }

        if (_retryButton != null)
        {
            _retryButton.onClick.RemoveAllListeners();
            _retryButton.onClick.AddListener(HandleRetryClicked);
        }

        if (_returnTownButton != null)
        {
            _returnTownButton.onClick.RemoveAllListeners();
            _returnTownButton.onClick.AddListener(HandleReturnTownClicked);
        }

        for (int i = 0; i < _targetButtons.Count; i++)
        {
            int slot = i;
            Button button = _targetButtons[i];
            if (button == null)
                continue;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectTargetSlot(slot));
        }

        _buttonListenersWired = true;
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
        if (gs == null || !IsLocalPlayerKnown(gs))
        {
            Hide();
            return;
        }

        CollectPartyTargets(gs);

        if (IsAlivePlayer(gs, gs.MyActorId))
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

    private void RequestRefresh()
    {
        if (!isActiveAndEnabled)
            return;

        _nextPassiveRefreshAt = Time.unscaledTime + 0.25f;
        RefreshState();
        if (_refreshRoutine == null)
            _refreshRoutine = StartCoroutine(CoRefreshAfterStateSettles());
    }

    private IEnumerator CoRefreshAfterStateSettles()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        _refreshRoutine = null;
        RefreshState();
    }

    private void CancelPendingRefresh()
    {
        if (_refreshRoutine == null)
            return;

        StopCoroutine(_refreshRoutine);
        _refreshRoutine = null;
    }

    private void CollectPartyTargets(ClientGameState gs)
    {
        _partyTargets.Clear();
        var seen = new HashSet<int>();

        AddPartyTarget(gs.MyActorId, seen, _partyTargets);

        if (gs.PlayerActorIds != null)
        {
            for (int i = 0; i < gs.PlayerActorIds.Length; i++)
                AddPartyTarget(gs.PlayerActorIds[i], seen, _partyTargets);
        }

        foreach (var roster in gs.EnumeratePlayerRoster())
            AddPartyTarget(roster.ActorId, seen, _partyTargets);

        foreach (var entity in gs.EnumerateEntities())
        {
            if (entity.EntityType == (int)EntityType.Player)
                AddPartyTarget(entity.EntityId, seen, _partyTargets);
        }

        _partyTargets.Sort();
    }

    private void CollectAliveTargets(ClientGameState gs)
    {
        _aliveTargets.Clear();

        for (int i = 0; i < _partyTargets.Count; i++)
        {
            int actorId = _partyTargets[i];
            if (actorId != gs.MyActorId && IsAlivePlayer(gs, actorId))
                _aliveTargets.Add(actorId);
        }
    }

    private static void AddPartyTarget(int actorId, HashSet<int> seen, List<int> targets)
    {
        if (actorId <= 0 || IsDecoyPlayerEntity(actorId) || !seen.Add(actorId))
            return;

        targets.Add(actorId);
    }

    private void ShowSpectator()
    {
        if (_spectatorPanel == null || _wipePanel == null)
            return;

        SetVisible(true);
        _spectatorPanel.gameObject.SetActive(true);
        _wipePanel.gameObject.SetActive(false);

        var gs = ClientGameState.Instance;
        string observedLabel = ResolvePlayerName(gs, _observedActorId);
        int totalCount = Mathf.Max(_partyTargets.Count, _aliveTargets.Count + 1);
        if (_spectatorStatus != null)
            _spectatorStatus.text = $"관전 중: {observedLabel}   생존 {_aliveTargets.Count}/{totalCount}";
        if (_spectatorTitle != null)
            _spectatorTitle.text = "전투 불능 - 관전";

        for (int i = 0; i < _targetButtons.Count; i++)
        {
            bool visible = i < _partyTargets.Count;
            _targetButtons[i].gameObject.SetActive(visible);
            if (!visible)
                continue;

            int actorId = _partyTargets[i];
            bool alive = IsAlivePlayer(gs, actorId);
            bool selected = actorId == _observedActorId;
            bool isSelf = actorId == gs.MyActorId;
            string state = alive ? "생존" : "전투불능";
            var targetLabel = i < _targetLabels.Count ? _targetLabels[i] : null;
            if (targetLabel != null)
            {
                targetLabel.text = isSelf
                    ? $"{ResolvePlayerName(gs, actorId)}  나"
                    : $"{ResolvePlayerName(gs, actorId)}  {state}";
                targetLabel.color = selected ? Gold : (alive ? TextMain : TextDisabled);
            }

            _targetButtons[i].interactable = alive && !selected && !isSelf;

            if (_targetButtons[i].targetGraphic is Image image)
                image.color = selected ? SelectedButtonFill : (alive ? ButtonFill : DisabledButtonFill);
        }

        bool canCycle = _aliveTargets.Count > 1;
        if (_previousButton != null)
            _previousButton.interactable = canCycle;
        if (_nextButton != null)
            _nextButton.interactable = canCycle;
    }

    private void ShowPartyWipe()
    {
        if (_spectatorPanel == null || _wipePanel == null)
            return;

        SetVisible(true);
        _spectatorPanel.gameObject.SetActive(false);
        _wipePanel.gameObject.SetActive(true);
        _observedActorId = 0;

        bool canRetry = ClientFlow.Instance != null && ClientFlow.Instance.CanRetryCurrentGame;
        if (_retryButton != null)
            _retryButton.interactable = canRetry;
        if (_returnTownButton != null)
            _returnTownButton.interactable = ClientFlow.Instance != null;
        if (_wipeStatus != null)
        {
            _wipeStatus.text = canRetry
                ? "모든 플레이어가 쓰러졌습니다. 다시 도전하거나 마을로 돌아가세요."
                : "모든 플레이어가 쓰러졌습니다. 현재 세션은 마을로 돌아갈 수 있습니다.";
        }
    }

    private void Hide()
    {
        SetVisible(false);
        if (_spectatorPanel != null)
            _spectatorPanel.gameObject.SetActive(false);
        if (_wipePanel != null)
            _wipePanel.gameObject.SetActive(false);
    }

    private void ShowEditPreview()
    {
        if (_group != null)
        {
            _group.alpha = 1f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        if (_spectatorPanel != null)
            _spectatorPanel.gameObject.SetActive(false);
        if (_wipePanel != null)
            _wipePanel.gameObject.SetActive(true);
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

    private void HandlePreviousClicked() => SelectRelativeTarget(-1);

    private void HandleNextClicked() => SelectRelativeTarget(1);

    private void SelectTargetSlot(int slot)
    {
        if ((uint)slot >= (uint)_partyTargets.Count)
            return;

        var gs = ClientGameState.Instance;
        int actorId = _partyTargets[slot];
        if (gs == null || actorId == gs.MyActorId || !IsAlivePlayer(gs, actorId))
            return;

        _observedActorId = actorId;
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

        if (gs.TryGetPlayerDisplayName(actorId, out var displayName) && !string.IsNullOrWhiteSpace(displayName))
            return TrimLabel(displayName);

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

    private static bool IsLocalPlayerKnown(ClientGameState gs)
        => gs != null && (gs.MyActorId > 0 || gs.TryGetMyEntity(out _));

    private static bool IsAlivePlayer(ClientGameState gs, int actorId)
    {
        return gs != null
               && actorId > 0
               && gs.TryGetEntity(actorId, out var info)
               && info.EntityType == (int)EntityType.Player
               && info.Hp > 0;
    }

    private static bool IsDecoyPlayerEntity(int entityId)
        => entityId >= DecoyEntityIdBase && entityId < DecoyEntityIdLimit;

    private static bool IsGameScene()
    {
        var sceneName = SceneManager.GetActiveScene().name;
        if (sceneName.StartsWith("Game", System.StringComparison.OrdinalIgnoreCase))
            return true;

        var contexts = Resources.FindObjectsOfTypeAll<GameSceneContext>();
        foreach (var context in contexts)
        {
            if (context != null && context.gameObject.scene.IsValid())
                return true;
        }

        return false;
    }

    private void HandleRetryClicked()
    {
        if (ClientFlow.Instance == null)
        {
            if (_wipeStatus != null)
                _wipeStatus.text = "재시도할 수 없습니다.";
            return;
        }

        if (_wipeStatus != null)
            _wipeStatus.text = "다시 시도 중...";
        if (_retryButton != null)
            _retryButton.interactable = false;
        if (_returnTownButton != null)
            _returnTownButton.interactable = false;
        ClientFlow.Instance.RetryCurrentGame();
    }

    private void HandleReturnTownClicked()
    {
        if (ClientFlow.Instance == null)
        {
            if (_wipeStatus != null)
                _wipeStatus.text = "마을로 돌아갈 수 없습니다.";
            return;
        }

        if (_wipeStatus != null)
            _wipeStatus.text = "마을로 돌아가는 중...";
        if (_retryButton != null)
            _retryButton.interactable = false;
        if (_returnTownButton != null)
            _returnTownButton.interactable = false;
        ClientFlow.Instance.ReturnToTown();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsGameScene())
            Hide();
        else
            RequestRefresh();
    }

    private void HandleMyEntityChanged(ClientEntityInfo _) => RequestRefresh();

    private void HandleEntityChanged(ClientEntityInfo info)
    {
        if (info.EntityType == (int)EntityType.Player)
            RequestRefresh();
    }

    private void HandleEntityRemoved(int _) => RequestRefresh();
    private void HandlePartyStateChanged() => RequestRefresh();
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

    private static Button BuildButton(
        RectTransform parent,
        string name,
        string label,
        Color labelColor,
        Vector2 anchoredPosition,
        Vector2 size,
        UnityEngine.Events.UnityAction action,
        Vector2? anchorOverride = null)
    {
        var image = CreateImage(name, parent, ButtonFill);
        Anchor(image.rectTransform, anchorOverride ?? new Vector2(0.5f, 0.5f), anchoredPosition, size, new Vector2(0.5f, 0.5f));
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (action != null)
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
        ApplyPreferredFont(text);
        return text;
    }

    private static void ApplyPreferredFont(TMP_Text text)
    {
        if (text == null)
            return;

        var font = LoadKoreanFont();
        if (font == null)
            return;

        text.font = font;
        text.fontSharedMaterial = font.material;
    }

    private static TMP_FontAsset LoadKoreanFont()
    {
        if (_koreanFont != null)
            return _koreanFont;

        _koreanFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Gowun Batang");
        if (_koreanFont == null)
            _koreanFont = Resources.Load<TMP_FontAsset>("Gowun Batang");
        if (_koreanFont == null)
            _koreanFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/NanumGothic SDF");
        if (_koreanFont == null)
            _koreanFont = Resources.Load<TMP_FontAsset>("NanumGothic SDF");

        return _koreanFont;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static T FindChild<T>(Transform parent, string childName) where T : Component
    {
        if (parent == null)
            return null;

        var child = parent.Find(childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
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

        ConfigureCanvasRect(canvas.GetComponent<RectTransform>());
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private static void ConfigureCanvasRect(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void EnsureEventSystem()
    {
        var eventSystem = FindFirstObjectByType<EventSystem>();
        GameObject eventSystemGo;
        if (eventSystem == null)
        {
            eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem = eventSystemGo.GetComponent<EventSystem>();
        }
        else
        {
            eventSystemGo = eventSystem.gameObject;
            eventSystemGo.SetActive(true);
        }

        eventSystem.enabled = true;

        var inputSystemModule = eventSystemGo.GetComponent<InputSystemUIInputModule>();
        if (inputSystemModule == null)
            inputSystemModule = eventSystemGo.AddComponent<InputSystemUIInputModule>();
        inputSystemModule.enabled = true;

        var standalone = eventSystemGo.GetComponent<StandaloneInputModule>();
        if (standalone != null)
            standalone.enabled = false;

        if (Application.isPlaying)
            DontDestroyOnLoad(eventSystemGo);
    }
}
