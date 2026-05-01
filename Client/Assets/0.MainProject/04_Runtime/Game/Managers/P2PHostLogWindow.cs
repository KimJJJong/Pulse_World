using System;
using System.Collections.Generic;
using System.Text;
using NetClient.Room.UI;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class P2PHostLogWindow : MonoBehaviour
{
    public static P2PHostLogWindow Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject(nameof(P2PHostLogWindow));
                _instance = go.AddComponent<P2PHostLogWindow>();
                DontDestroyOnLoad(go);
            }

            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;

    private static P2PHostLogWindow _instance;

    [SerializeField] private Rect _windowRect = new Rect(18f, 18f, 760f, 420f);
    [SerializeField] private bool _visible;
    [SerializeField] private bool _followLatest = true;
    [SerializeField] private int _maxEntries = 240;

    private readonly object _lock = new object();
    private readonly List<LogEntry> _entries = new List<LogEntry>();

    private string _relayKey = "";
    private bool _isRelayMode;
    private bool _isHostLocal;
    private int _hostActorId;
    private string _statusLine = "P2P relay idle";
    private string _stateSignature = "";
    private bool _dirty = true;
    private string _cachedText = "";
    private Vector2 _scroll;
    private bool _scrollToBottomRequested = true;
    private GUIStyle _logStyle;
    private bool _captureSubscribed;

    private sealed class LogEntry
    {
        public string Time = "";
        public string Type = "";
        public string Message = "";
        public string Stack = "";
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        SetCaptureEnabled(P2PDebugConfig.LogOverheadEnabled);
    }

    private void OnDestroy()
    {
        SetCaptureEnabled(false);

        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        P2PDebugConfig.PollRuntimeToggle();

        if (_visible && Input.GetKeyDown(KeyCode.F9))
            _visible = false;
    }

    internal void SetCaptureEnabled(bool enabled)
    {
        if (enabled)
        {
            if (_captureSubscribed)
                return;

            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            _captureSubscribed = true;
            return;
        }

        if (!_captureSubscribed)
            return;

        Application.logMessageReceivedThreaded -= OnLogMessageReceived;
        _captureSubscribed = false;
    }

    public void SetRelayContext(string relayKey, bool isRelayMode, bool isHostLocal, int hostActorId)
    {
        relayKey ??= "";

        _relayKey = relayKey;
        _isRelayMode = isRelayMode;
        _isHostLocal = isHostLocal;
        _hostActorId = hostActorId;

        string signature = $"{_relayKey}|{_isRelayMode}|{_isHostLocal}|{_hostActorId}";
        if (signature != _stateSignature)
        {
            _stateSignature = signature;
            _statusLine = _isRelayMode
                ? (_isHostLocal
                    ? $"Relay host active. HostActor={_hostActorId}"
                    : $"Relay active. Waiting for host assignment. HostActor={_hostActorId}")
                : "Relay disabled";

            PushSystemMessage(_statusLine);
        }

        _visible = P2PDebugConfig.LogOverheadEnabled && _isRelayMode && _isHostLocal;
        _dirty = true;
    }

    public void HideAndClear()
    {
        _visible = false;
        _relayKey = "";
        _isRelayMode = false;
        _isHostLocal = false;
        _hostActorId = 0;
        _statusLine = "P2P relay idle";
        _stateSignature = "";
        Clear();
        _scroll = Vector2.zero;
    }

    public void PushSystemMessage(string message)
    {
        AddEntry("SYS", message, "");
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            _cachedText = "";
            _dirty = true;
        }

        _scroll = Vector2.zero;
        _scrollToBottomRequested = true;
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (!P2PDebugConfig.LogOverheadEnabled)
            return;

        if (!_isRelayMode || !_isHostLocal || string.IsNullOrWhiteSpace(condition))
            return;

        if (!P2PDebugConfig.ShouldCaptureHostLog(condition, type))
            return;

        AddEntry(type.ToString().ToUpperInvariant(), condition, stackTrace ?? "");
    }

    private void AddEntry(string type, string message, string stackTrace)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        lock (_lock)
        {
            _entries.Add(new LogEntry
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Type = type,
                Message = message,
                Stack = stackTrace ?? ""
            });

            if (_entries.Count > _maxEntries)
                _entries.RemoveAt(0);

            _dirty = true;
            if (_followLatest)
                _scrollToBottomRequested = true;
        }
    }

    private void OnGUI()
    {
        if (!_visible)
            return;

        EnsureStyles();
        _windowRect = GUILayout.Window(991243, _windowRect, DrawWindow, BuildTitle());
    }

    private string BuildTitle()
        => _isHostLocal ? "P2P Host Log" : "P2P Relay Log";

    private void DrawWindow(int id)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(_statusLine, GUILayout.ExpandWidth(true));

        if (GUILayout.Button("Copy", GUILayout.Width(60)))
            GUIUtility.systemCopyBuffer = BuildTextSnapshot();

        if (GUILayout.Button("Clear", GUILayout.Width(60)))
            Clear();

        if (GUILayout.Button("Hide", GUILayout.Width(60)))
            _visible = false;

        bool followLatest = GUILayout.Toggle(_followLatest, "Follow", GUILayout.Width(74));
        if (followLatest != _followLatest)
        {
            _followLatest = followLatest;
            _scrollToBottomRequested = _followLatest;
        }

        GUILayout.EndHorizontal();

        GUILayout.Label($"RelayKey: {_relayKey} | HostActorId: {_hostActorId}");

        string snapshot = BuildTextSnapshot();
        float contentWidth = Mathf.Max(240f, _windowRect.width - 44f);
        float contentHeight = Mathf.Max(120f, _logStyle.CalcHeight(new GUIContent(snapshot), contentWidth));

        if (_followLatest && _scrollToBottomRequested)
            _scroll.y = float.MaxValue;

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        Rect logRect = GUILayoutUtility.GetRect(contentWidth, contentHeight, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(false));
        GUI.TextArea(logRect, snapshot, _logStyle);
        GUILayout.EndScrollView();

        if (_followLatest && _scrollToBottomRequested)
            _scrollToBottomRequested = false;

        GUI.DragWindow(new Rect(0, 0, 10000f, 24f));
    }

    private string BuildTextSnapshot()
    {
        lock (_lock)
        {
            if (!_dirty)
                return _cachedText;

            var sb = new StringBuilder(4096);
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                sb.Append('[').Append(entry.Time).Append("] [").Append(entry.Type).Append("] ").Append(entry.Message);

                if (!string.IsNullOrWhiteSpace(entry.Stack))
                {
                    sb.Append('\n');
                    sb.Append(entry.Stack);
                }

                if (i + 1 < _entries.Count)
                    sb.Append('\n');
            }

            _cachedText = sb.ToString();
            _dirty = false;
            return _cachedText;
        }
    }

    private void EnsureStyles()
    {
        if (_logStyle != null)
            return;

        _logStyle = new GUIStyle(GUI.skin.textArea)
        {
            wordWrap = true,
            richText = false,
            fontSize = 12
        };
    }
}

internal static class P2PDebugConfig
{
    private const bool TraceHostFlowDefault = false;
    private const bool TraceContentDefault = false;
    private const bool TraceInputDefault = false;
    private const bool TraceCombatDefault = false;
    private const bool CaptureOnlyP2PHostLogs = true;
    private const KeyCode ToggleLogOverheadKey = KeyCode.F10;
    private static int _lastToggleFrame = -1;

    internal static bool LogOverheadEnabled { get; private set; } = false;
    internal static bool TraceHostFlow => LogOverheadEnabled && TraceHostFlowDefault;
    internal static bool TraceContent => LogOverheadEnabled && TraceContentDefault;
    internal static bool TraceInput => LogOverheadEnabled && TraceInputDefault;
    internal static bool TraceCombat => LogOverheadEnabled && TraceCombatDefault;

    internal static void PollRuntimeToggle()
    {
        if (!Input.GetKeyDown(ToggleLogOverheadKey))
            return;

        int frame = Time.frameCount;
        if (_lastToggleFrame == frame)
            return;

        _lastToggleFrame = frame;
        SetLogOverheadEnabled(!LogOverheadEnabled);
    }

    internal static void SetLogOverheadEnabled(bool enabled)
    {
        if (LogOverheadEnabled == enabled)
            return;

        LogOverheadEnabled = enabled;

        if (P2PHostLogWindow.HasInstance)
        {
            P2PHostLogWindow.Instance.SetCaptureEnabled(enabled);
            if (!enabled)
                P2PHostLogWindow.Instance.HideAndClear();
        }
    }

    internal static bool ShouldCaptureHostLog(string condition, LogType type)
    {
        if (!LogOverheadEnabled)
            return false;

        if (type == LogType.Error || type == LogType.Exception)
            return true;

        if (!CaptureOnlyP2PHostLogs)
            return true;

        if (string.IsNullOrWhiteSpace(condition))
            return false;

        return StartsWithAny(
            condition,
            "[P2P",
            "[P2PHostController]",
            "[P2PContentDirector]",
            "[Input_",
            "[DamageRecv]",
            "[WarningRecv]",
            "[SC_",
            "[InitMap",
            "[RhythmInput]");
    }

    private static bool StartsWithAny(string text, params string[] prefixes)
    {
        for (int i = 0; i < prefixes.Length; i++)
        {
            if (text.StartsWith(prefixes[i], StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}

[DefaultExecutionOrder(-995)]
public sealed class SteamP2PDebugHud : MonoBehaviour
{
    public static SteamP2PDebugHud Ensure(bool visibleByDefault)
    {
        if (_instance == null)
        {
            var go = new GameObject(nameof(SteamP2PDebugHud));
            _instance = go.AddComponent<SteamP2PDebugHud>();
            _instance._visible = visibleByDefault;
            DontDestroyOnLoad(go);
        }
        else
        {
            _instance._visible = _instance._visible || visibleByDefault;
        }

        return _instance;
    }

    private static SteamP2PDebugHud _instance;

    [SerializeField] private Rect _windowRect = new Rect(18f, 18f, 620f, 470f);
    [SerializeField] private bool _visible = true;
    [SerializeField] private KeyCode _toggleKey = KeyCode.F8;

    private Vector2 _scroll;
    private GUIStyle _textStyle;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        P2PDebugConfig.PollRuntimeToggle();

        if (Input.GetKeyDown(_toggleKey))
            _visible = !_visible;
    }

    private void OnGUI()
    {
        if (!_visible)
            return;

        EnsureStyles();
        _windowRect = GUILayout.Window(991244, _windowRect, DrawWindow, "Steam / P2P Debug");
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("F8 Toggle", GUILayout.Width(80f));
        GUILayout.Label("Solo smoke / Steam / P2P status", GUILayout.ExpandWidth(true));

        if (GUILayout.Button("Copy", GUILayout.Width(60f)))
            GUIUtility.systemCopyBuffer = BuildSnapshot();

        if (GUILayout.Button("Hide", GUILayout.Width(60f)))
            _visible = false;

        GUILayout.EndHorizontal();

        string snapshot = BuildSnapshot();
        float contentWidth = Mathf.Max(220f, _windowRect.width - 44f);
        float contentHeight = Mathf.Max(160f, _textStyle.CalcHeight(new GUIContent(snapshot), contentWidth));

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        Rect rect = GUILayoutUtility.GetRect(contentWidth, contentHeight, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(false));
        GUI.TextArea(rect, snapshot, _textStyle);
        GUILayout.EndScrollView();

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private string BuildSnapshot()
    {
        var sb = new StringBuilder(2048);

        var root = AppBootstrap.Instance != null ? AppBootstrap.Instance.Root : null;
        var config = root?.Config;
        var steam = root?.SteamPlatform;
        var auth = root?.AuthApi;
        var room = RoomUiController.ActiveInstance;
        var bridge = P2PRelayClientBridge.HasInstance ? P2PRelayClientBridge.Instance : null;
        var manifest = SessionContext.Instance?.LastMatchManifest;

        AppendHeader(sb, "Steam");
        AppendField(sb, "Enabled", steam != null && steam.Enabled ? "YES" : "NO");
        AppendField(sb, "Initialized", steam != null && steam.IsInitialized ? "YES" : "NO");
        AppendField(sb, "AppId", config?.SteamAppId ?? "-");
        AppendField(sb, "SteamId64", steam?.SteamId64 ?? "-");
        AppendField(sb, "Name", steam?.DisplayName ?? "-");
        AppendField(sb, "Lobby", steam != null && steam.HasJoinedLobby ? steam.CurrentLobbyId : "Not Joined");
        AppendField(sb, "LobbyOwner", steam != null && steam.IsLobbyOwner ? "YES" : "NO");
        AppendField(sb, "LastError", string.IsNullOrWhiteSpace(steam?.LastError) ? "-" : steam.LastError);

        AppendHeader(sb, "Auth");
        AppendField(sb, "Mode", auth?.LastPreferredLoginMode ?? "Unknown");
        AppendField(sb, "Detail", auth?.LastPreferredLoginDetail ?? "-");

        AppendHeader(sb, "Waiting Room");
        if (room != null && room.IsUiOpen)
        {
            AppendField(sb, "RoomId", string.IsNullOrWhiteSpace(room.CurrentRoomId) ? "-" : room.CurrentRoomId);
            AppendField(sb, "Members", room.MemberCount.ToString());
            AppendField(sb, "SteamLobbyId", string.IsNullOrWhiteSpace(room.CurrentSteamLobbyId) ? "-" : room.CurrentSteamLobbyId);
            AppendField(sb, "LobbyStatus", room.SteamLobbyStatus);
            AppendField(sb, "Probe", room.LastWaitingProbeRttMs >= 0 ? $"{room.LastWaitingProbeRttMs} ms ({room.LastWaitingProbeStatus})" : room.LastWaitingProbeStatus);
            AppendField(sb, "PreferredHost", string.IsNullOrWhiteSpace(room.PreferredHostUid) ? "-" : $"{room.PreferredHostUid} / epoch {room.PreferredHostEpoch}");
            AppendField(sb, "RoomWarn", string.IsNullOrWhiteSpace(room.LastWarningText) ? "-" : room.LastWarningText);
        }
        else
        {
            AppendField(sb, "State", "Room UI closed");
        }

        AppendHeader(sb, "Match / Transport");
        AppendField(sb, "LogOverhead", P2PDebugConfig.LogOverheadEnabled ? "ON (F10)" : "OFF (F10)");
        AppendField(sb, "Manifest", manifest != null ? $"{manifest.NetworkMode} / {manifest.MatchId}" : "No active manifest");
        if (manifest != null)
        {
            AppendField(sb, "ManifestHost", $"{manifest.HostUid} / {manifest.HostSteamId64}");
            AppendField(sb, "ManifestEpoch", manifest.HostEpoch.ToString());
        }

        if (bridge != null && bridge.IsP2PMode)
        {
            AppendField(sb, "Transport", bridge.TransportName);
            AppendField(sb, "Role", bridge.IsHostLocal ? "Host" : "Guest");
            AppendField(sb, "State", bridge.TransportDebugStatus);
            AppendField(sb, "RelayKey", string.IsNullOrWhiteSpace(bridge.RelayKey) ? "-" : bridge.RelayKey);
            AppendField(sb, "Host", $"{bridge.HostUid} / {bridge.HostSteamId64} / actor {bridge.HostActorId}");
            AppendField(sb, "HostEpoch", bridge.HostEpoch.ToString());
            AppendField(sb, "Peers", bridge.SteamConnectedPeerCount.ToString());
            AppendField(sb, "ToHost", bridge.IsSteamTransport ? (bridge.IsSteamTransportConnectedToHost ? "Connected" : "Not Connected") : "-");
            AppendField(sb, "Ping", bridge.IsHostLocal ? "Local Host" : FormatPingSummary(bridge));
            AppendField(sb, "TransportError", string.IsNullOrWhiteSpace(bridge.TransportLastError) ? "-" : bridge.TransportLastError);
        }
        else
        {
            AppendField(sb, "State", "No active P2P match");
        }

        AppendHeader(sb, "Solo Check");
        AppendField(sb, "Summary", BuildSoloHint(steam, room, bridge));

        return sb.ToString();
    }

    private static string FormatPingSummary(P2PRelayClientBridge bridge)
    {
        if (bridge == null)
            return "-";

        if (!bridge.HasRemoteHostPing)
            return bridge.HostPingStatus;

        return $"{bridge.HostPingStatus} / last {bridge.HostLastRttMs} ms / avg {bridge.HostAvgRttMs} ms";
    }

    private static string BuildSoloHint(ISteamPlatformService steam, RoomUiController room, P2PRelayClientBridge bridge)
    {
        if (steam == null || !steam.Enabled)
            return "Steam toggle is off. P2P path will not start.";

        if (!steam.IsInitialized)
            return $"Steam init is not complete. {steam.LastError}";

        if (room != null && room.IsUiOpen && room.MemberCount <= 1)
            return "Steam is ready. With one client, you can validate init/login/lobby/probe only. Guest-host direct connect and host reselection still need a second Steam account.";

        if (bridge != null && bridge.IsServerRelayTransport)
            return "This match started on ServerRelay, not Steam P2P. Usually that means the remote ApiServer did not send the new matchManifest/networkMode yet, or the deployment is still on the older build.";

        if (bridge != null && bridge.IsSteamTransport && !bridge.IsHostLocal && !bridge.IsSteamTransportConnectedToHost)
            return "Steam transport is selected, but this guest is not connected to the host yet.";

        if (bridge != null && bridge.IsSteamTransport && bridge.IsHostLocal && bridge.SteamConnectedPeerCount == 0)
            return "Steam host socket is ready. No remote guest is attached yet.";

        return "Steam transport looks ready for the current step.";
    }

    private static void AppendHeader(StringBuilder sb, string title)
    {
        if (sb.Length > 0)
            sb.AppendLine();

        sb.AppendLine($"[{title}]");
    }

    private static void AppendField(StringBuilder sb, string key, string value)
    {
        sb.Append(key)
            .Append(": ")
            .AppendLine(string.IsNullOrWhiteSpace(value) ? "-" : value);
    }

    private void EnsureStyles()
    {
        if (_textStyle != null)
            return;

        _textStyle = new GUIStyle(GUI.skin.textArea)
        {
            wordWrap = true,
            richText = false,
            fontSize = 12
        };
    }
}
