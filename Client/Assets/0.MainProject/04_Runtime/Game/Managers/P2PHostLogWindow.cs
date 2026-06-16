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
    [SerializeField] private bool _windowEnabled = true;
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
    private const int MaxStackLines = 6;
    private const int MaxMessageChars = 900;
    private const KeyCode ToggleWindowKey = KeyCode.F9;
    private const string ToggleWindowKeyName = "Ctrl+Alt+F9";

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
        P2PDebugViewConfig.PollRuntimeToggles();

        if (RuntimeHotkey.WasPressed(ToggleWindowKey, requireCtrl: true, requireAlt: true))
        {
            _windowEnabled = !_windowEnabled;
            RefreshVisibility();
        }
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

        RefreshVisibility();
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

    private void RefreshVisibility()
    {
        _visible = _windowEnabled && P2PDebugConfig.LogOverheadEnabled && _isRelayMode && _isHostLocal;
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

        if (!_isRelayMode || string.IsNullOrWhiteSpace(condition))
            return;

        if (!P2PDebugConfig.ShouldCaptureHostLog(condition, type))
            return;

        string message = NormalizeLogMessage(condition);
        if (string.IsNullOrWhiteSpace(message))
            return;

        string compactStack = ShouldShowStackTrace(type)
            ? CompactStackTrace(stackTrace)
            : "";

        AddEntry(type.ToString().ToUpperInvariant(), message, compactStack);
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
        {
            _windowEnabled = false;
            _visible = false;
        }

        bool followLatest = GUILayout.Toggle(_followLatest, "Follow", GUILayout.Width(74));
        if (followLatest != _followLatest)
        {
            _followLatest = followLatest;
            _scrollToBottomRequested = _followLatest;
        }

        GUILayout.EndHorizontal();

        GUILayout.Label($"{ToggleWindowKeyName} Window: {(_windowEnabled ? "ON" : "OFF")} | {P2PDebugViewConfig.ToggleSteamSectionKeyName} Steam: {(P2PDebugViewConfig.ShowSteamSections ? "ON" : "OFF")}");

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

    private static bool ShouldShowStackTrace(LogType type)
        => type == LogType.Error || type == LogType.Exception || type == LogType.Assert;

    private static string NormalizeLogMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "";

        string[] lines = message.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var sb = new StringBuilder(message.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i]?.TrimEnd() ?? "";
            if (line.Length == 0)
                continue;

            if (i > 0 && IsStackTraceLine(line))
                continue;

            if (sb.Length > 0)
                sb.Append(" | ");

            sb.Append(line.Trim());
            if (sb.Length >= MaxMessageChars)
                break;
        }

        if (sb.Length > MaxMessageChars)
            return sb.ToString(0, MaxMessageChars) + "...";

        return sb.ToString();
    }

    private static string CompactStackTrace(string stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
            return "";

        string[] lines = stackTrace.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var sb = new StringBuilder(512);
        int kept = 0;
        for (int i = 0; i < lines.Length && kept < MaxStackLines; i++)
        {
            string line = lines[i]?.Trim() ?? "";
            if (line.Length == 0 || IsUnityDebugStackLine(line))
                continue;

            if (sb.Length > 0)
                sb.Append('\n');

            sb.Append("    ").Append(line);
            kept++;
        }

        return sb.ToString();
    }

    private static bool IsStackTraceLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        string trimmed = line.TrimStart();
        return trimmed.StartsWith("UnityEngine.", StringComparison.Ordinal)
            || trimmed.StartsWith("System.", StringComparison.Ordinal)
            || trimmed.StartsWith("at ", StringComparison.Ordinal)
            || trimmed.Contains(" (at ");
    }

    private static bool IsUnityDebugStackLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return true;

        return line.StartsWith("UnityEngine.Debug:", StringComparison.Ordinal)
            || line.StartsWith("UnityEngine.Logger:", StringComparison.Ordinal)
            || line.StartsWith("P2PHostLogWindow:", StringComparison.Ordinal)
            || line.StartsWith("P2PDebugConfig:", StringComparison.Ordinal);
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

internal static class RuntimeHotkey
{
    internal static bool WasPressed(
        KeyCode key,
        bool requireCtrl = false,
        bool requireAlt = false,
        bool requireShift = false)
    {
        if (!Input.GetKeyDown(key))
            return false;

        return IsModifierSatisfied(requireCtrl, IsCtrlHeld())
            && IsModifierSatisfied(requireAlt, IsAltHeld())
            && IsModifierSatisfied(requireShift, IsShiftHeld());
    }

    private static bool IsModifierSatisfied(bool required, bool actual)
    {
        return !required || actual;
    }

    private static bool IsCtrlHeld()
    {
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    }

    private static bool IsAltHeld()
    {
        return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
    }

    private static bool IsShiftHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }
}

internal static class P2PDebugConfig
{
    private const bool TraceHostFlowDefault = true;
    private const bool TraceContentDefault = false;
    private const bool TraceInputDefault = true;
    private const bool TraceCombatDefault = false;
    private const bool TraceRealtimeTransportDefault = false;
    private const bool TraceRealtimeInputVerboseDefault = false;
    private static readonly bool CaptureOnlyP2PHostLogs = true;
    private const KeyCode ToggleLogOverheadKey = KeyCode.F7;
    private static int _lastToggleFrame = -1;

    internal const string ToggleLogOverheadKeyName = "Ctrl+Alt+F7";
    internal static bool DebugUiEnabled { get; private set; } = true;
    internal static bool LogOverheadEnabled { get; private set; } = false;
    internal static bool TraceHostFlow => LogOverheadEnabled && TraceHostFlowDefault;
    internal static bool TraceContent => LogOverheadEnabled && TraceContentDefault;
    internal static bool TraceInput => LogOverheadEnabled && TraceInputDefault;
    internal static bool TraceCombat => LogOverheadEnabled && TraceCombatDefault;
    internal static bool TraceRealtimeTransport => LogOverheadEnabled && TraceRealtimeTransportDefault;
    internal static bool TraceRealtimeInputVerbose => LogOverheadEnabled && TraceRealtimeInputVerboseDefault;

    internal static bool PollRuntimeToggle()
    {
        if (!DebugUiEnabled)
            return false;

        if (!RuntimeHotkey.WasPressed(ToggleLogOverheadKey, requireCtrl: true, requireAlt: true))
            return false;

        int frame = Time.frameCount;
        if (_lastToggleFrame == frame)
            return false;

        _lastToggleFrame = frame;
        SetLogOverheadEnabled(!LogOverheadEnabled);
        return true;
    }

    internal static void SetDebugUiEnabled(bool enabled)
    {
        if (DebugUiEnabled == enabled)
            return;

        DebugUiEnabled = enabled;
        if (!enabled)
            SetLogOverheadEnabled(false);
    }

    internal static void SetLogOverheadEnabled(bool enabled)
    {
        if (enabled && !DebugUiEnabled)
            enabled = false;

        if (LogOverheadEnabled == enabled)
            return;

        LogOverheadEnabled = enabled;

        if (enabled)
        {
            P2PHostLogWindow.Instance.SetCaptureEnabled(true);
            P2PHostLogWindow.Instance.PushSystemMessage($"Log overhead enabled ({ToggleLogOverheadKeyName})");
            if (P2PRelayClientBridge.HasInstance)
                P2PRelayClientBridge.Instance.RefreshHostLogWindow();
            return;
        }

        if (P2PHostLogWindow.HasInstance)
        {
            P2PHostLogWindow.Instance.SetCaptureEnabled(false);
            P2PHostLogWindow.Instance.HideAndClear();
        }
    }

    internal static bool ShouldCaptureHostLog(string condition, LogType type)
    {
        if (!DebugUiEnabled || !LogOverheadEnabled)
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

internal static class P2PDebugViewConfig
{
    private const KeyCode ToggleNetworkOverlayKey = KeyCode.F10;
    private const KeyCode ToggleSteamSectionKey = KeyCode.F11;
    private static int _lastOverlayToggleFrame = -1;
    private static int _lastSteamToggleFrame = -1;

    internal const string ToggleNetworkOverlayKeyName = "Ctrl+Alt+F10";
    internal const string ToggleSteamSectionKeyName = "Ctrl+Alt+F11";
    internal static bool DebugUiEnabled { get; private set; } = true;
    internal static bool ShowNetworkSyncOverlay { get; private set; } = true;
    internal static bool ShowSteamSections { get; private set; } = true;

    internal static void PollRuntimeToggles()
    {
        if (!DebugUiEnabled)
            return;

        if (RuntimeHotkey.WasPressed(ToggleNetworkOverlayKey, requireCtrl: true, requireAlt: true))
            ToggleNetworkOverlayOnce();

        if (RuntimeHotkey.WasPressed(ToggleSteamSectionKey, requireCtrl: true, requireAlt: true))
            ToggleSteamSectionOnce();
    }

    internal static bool HandleRuntimeToggleEvent(Event evt)
    {
        if (!DebugUiEnabled)
            return false;

        if (evt == null || evt.type != EventType.KeyDown)
            return false;

        var handled = false;
        if (evt.keyCode == ToggleNetworkOverlayKey && IsEventModifierSatisfied(evt, requireCtrl: true, requireAlt: true))
        {
            ToggleNetworkOverlayOnce();
            evt.Use();
            handled = true;
        }

        if (evt.keyCode == ToggleSteamSectionKey && IsEventModifierSatisfied(evt, requireCtrl: true, requireAlt: true))
        {
            ToggleSteamSectionOnce();
            evt.Use();
            handled = true;
        }

        return handled;
    }

    internal static void SetDebugUiEnabled(bool enabled)
    {
        if (DebugUiEnabled == enabled)
            return;

        DebugUiEnabled = enabled;
        ShowNetworkSyncOverlay = enabled;
        ShowSteamSections = enabled;
    }

    private static bool IsEventModifierSatisfied(
        Event evt,
        bool requireCtrl = false,
        bool requireAlt = false,
        bool requireShift = false)
    {
        return (!requireCtrl || evt.control || evt.command)
            && (!requireAlt || evt.alt)
            && (!requireShift || evt.shift);
    }

    private static void ToggleNetworkOverlayOnce()
    {
        if (!DebugUiEnabled)
            return;

        int frame = Time.frameCount;
        if (_lastOverlayToggleFrame == frame)
            return;

        _lastOverlayToggleFrame = frame;
        ShowNetworkSyncOverlay = !ShowNetworkSyncOverlay;
    }

    private static void ToggleSteamSectionOnce()
    {
        if (!DebugUiEnabled)
            return;

        int frame = Time.frameCount;
        if (_lastSteamToggleFrame == frame)
            return;

        _lastSteamToggleFrame = frame;
        ShowSteamSections = !ShowSteamSections;
    }
}

internal static class P2PTransportDiagnostics
{
    private const int MaxEventCount = 12;
    private const int MaxMessageLength = 160;
    private const int MaxActionHistory = 6;
    private const long HostSeenTimeoutMs = 1500;

    private sealed class DiagnosticEvent
    {
        public string Time = "";
        public string Category = "";
        public string Message = "";
    }

    private sealed class LocalActionTrace
    {
        public string Key = "";
        public string ActionName = "";
        public int ActionKind;
        public int ActorId;
        public int SlotIndex;
        public int TargetX;
        public int TargetY;
        public long ClientSendTimeMs;
        public long InputAtMs;
        public long LastUpdatedAtMs;
        public string SendStatus = "Pending";
        public string HostStatus = "Pending";
        public string JudgeStatus = "Pending";
        public string ResultStatus = "Pending";
        public bool Completed;
    }

    private static readonly object _gate = new object();
    private static readonly Queue<DiagnosticEvent> _events = new Queue<DiagnosticEvent>();
    private static readonly Dictionary<string, LocalActionTrace> _actionsByKey = new Dictionary<string, LocalActionTrace>(StringComparer.Ordinal);
    private static readonly List<LocalActionTrace> _actionHistory = new List<LocalActionTrace>(MaxActionHistory);

    private static string _context = "-";
    private static int _inputBlockedCount;
    private static int _inputAttemptCount;
    private static int _steamSendCount;
    private static int _steamSendFailCount;
    private static int _relaySendCount;
    private static int _hostReceiveSteamCount;
    private static int _hostReceiveRelayCount;
    private static int _actorFallbackRecoveredCount;
    private static int _actorResolveDropCount;
    private static int _hostQueueCount;
    private static int _hostJudgeAcceptedCount;
    private static int _hostJudgeRejectedCount;
    private static int _steamAttemptStartCount;
    private static int _steamAttemptConnectedCount;
    private static int _steamAttemptDisconnectedCount;
    private static int _fallbackCount;

    internal static void Reset(string context)
    {
        lock (_gate)
        {
            _context = string.IsNullOrWhiteSpace(context) ? "-" : context;
            _events.Clear();
            _actionsByKey.Clear();
            _actionHistory.Clear();
            _inputBlockedCount = 0;
            _inputAttemptCount = 0;
            _steamSendCount = 0;
            _steamSendFailCount = 0;
            _relaySendCount = 0;
            _hostReceiveSteamCount = 0;
            _hostReceiveRelayCount = 0;
            _actorFallbackRecoveredCount = 0;
            _actorResolveDropCount = 0;
            _hostQueueCount = 0;
            _hostJudgeAcceptedCount = 0;
            _hostJudgeRejectedCount = 0;
            _steamAttemptStartCount = 0;
            _steamAttemptConnectedCount = 0;
            _steamAttemptDisconnectedCount = 0;
            _fallbackCount = 0;
            AddEventUnsafe("RESET", $"context={_context}");
        }
    }

    internal static void RecordInputBlocked(string actionTag, string reason)
    {
        lock (_gate)
        {
            _inputBlockedCount++;
            AddEventUnsafe("BLOCK", $"{Safe(actionTag)} {Trim(reason)}");
        }
    }

    internal static void RecordInputAttempt(
        string action,
        int actionKind,
        int actorId,
        int slotIndex,
        int targetX,
        int targetY,
        long clientSendTimeMs,
        string detail)
    {
        lock (_gate)
        {
            _inputAttemptCount++;
            var trace = new LocalActionTrace
            {
                Key = BuildActionKey(actorId, actionKind, slotIndex, targetX, targetY, clientSendTimeMs),
                ActionName = Safe(action),
                ActionKind = actionKind,
                ActorId = actorId,
                SlotIndex = slotIndex,
                TargetX = targetX,
                TargetY = targetY,
                ClientSendTimeMs = clientSendTimeMs,
                InputAtMs = P2PRelayDiagnosticsPackets.NowMs(),
                LastUpdatedAtMs = P2PRelayDiagnosticsPackets.NowMs()
            };

            _actionsByKey[trace.Key] = trace;
            _actionHistory.Add(trace);
            while (_actionHistory.Count > MaxActionHistory)
            {
                var expired = _actionHistory[0];
                _actionHistory.RemoveAt(0);
                _actionsByKey.Remove(expired.Key);
            }

            AddEventUnsafe("INPUT", $"actor={actorId} action={Safe(action)} target=({targetX},{targetY}) slot={slotIndex} sendMs={clientSendTimeMs}");
        }
    }

    internal static void RecordOutgoing(string route, string protocol, bool success, string detail, byte[] payloadBytes = null)
    {
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(route) && route.IndexOf("relay", StringComparison.OrdinalIgnoreCase) >= 0)
                _relaySendCount++;
            else if (success)
                _steamSendCount++;
            else
                _steamSendFailCount++;

            if (TryExtractActionRequest(payloadBytes, out var req))
            {
                if (TryGetActionTrace(req.ActorId, req.ActionKind, req.SlotIndex, req.TargetX, req.TargetY, req.ClientSendTimeMs, out var trace))
                {
                    trace.SendStatus = $"{Safe(route)} {(success ? "OK" : "FAIL")} {Trim(detail)}";
                    trace.LastUpdatedAtMs = P2PRelayDiagnosticsPackets.NowMs();
                }

                AddEventUnsafe("SEND", $"{Safe(route)} action={(ActionKind)req.ActionKind} {(success ? "OK" : "FAIL")} {Trim(detail)}");
                return;
            }

            if (string.Equals(protocol, "CS_ActionRequest(27)", StringComparison.Ordinal))
                AddEventUnsafe("SEND", $"{Safe(route)} {Safe(protocol)} {(success ? "OK" : "FAIL")} {Trim(detail)}");
        }
    }

    internal static void RecordIncoming(string route, string protocol, int actorId, string detail)
    {
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(route) && route.IndexOf("relay", StringComparison.OrdinalIgnoreCase) >= 0)
                _hostReceiveRelayCount++;
            else
                _hostReceiveSteamCount++;

            if (!string.IsNullOrWhiteSpace(route)
                && route.IndexOf("GuestToHost", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                AddEventUnsafe("RECV", $"{Safe(route)} actor={actorId} {Safe(protocol)}");
            }
        }
    }

    internal static void RecordActorFallbackRecovered(string steamId64, int actorId, string protocol)
    {
        lock (_gate)
        {
            _actorFallbackRecoveredCount++;
            AddEventUnsafe("RESOLVE", $"payload actor={actorId} steam={Safe(steamId64)} {Safe(protocol)}");
        }
    }

    internal static void RecordActorResolveDrop(string steamId64, string protocol, string detail)
    {
        lock (_gate)
        {
            _actorResolveDropCount++;
            AddEventUnsafe("DROP", $"actor unresolved steam={Safe(steamId64)} {Safe(protocol)} {Trim(detail)}");
        }
    }

    internal static void RecordHostQueue(string source, int actorId, string action, string detail)
    {
        lock (_gate)
        {
            _hostQueueCount++;
            AddEventUnsafe("QUEUE", $"{Safe(source)} actor={actorId} action={Safe(action)} {Trim(detail)}");
        }
    }

    internal static void RecordHostJudge(string outcome, int actorId, string action, string detail)
    {
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(outcome) && outcome.IndexOf("accept", StringComparison.OrdinalIgnoreCase) >= 0)
                _hostJudgeAcceptedCount++;
            else
                _hostJudgeRejectedCount++;

            AddEventUnsafe("JUDGE", $"{Safe(outcome)} actor={actorId} action={Safe(action)} {Trim(detail)}");
        }
    }

    internal static void RecordFallback(string reason, string protocol, string detail)
    {
        lock (_gate)
        {
            _fallbackCount++;
            AddEventUnsafe("FALLBACK", $"{Safe(reason)} {Safe(protocol)} {Trim(detail)}");
        }
    }

    internal static void RecordSteamAttempt(string stage, int attempt, string detail)
    {
        lock (_gate)
        {
            if (string.Equals(stage, "Start", StringComparison.OrdinalIgnoreCase))
                _steamAttemptStartCount++;
            else if (string.Equals(stage, "Connected", StringComparison.OrdinalIgnoreCase))
                _steamAttemptConnectedCount++;
            else if (string.Equals(stage, "Disconnected", StringComparison.OrdinalIgnoreCase))
                _steamAttemptDisconnectedCount++;

            AddEventUnsafe("STEAM", $"{Safe(stage)}#{Math.Max(0, attempt)} {Trim(detail)}");
        }
    }

    internal static void RecordActionTrace(P2PActionTracePacket packet)
    {
        if (packet == null)
            return;

        lock (_gate)
        {
            if (!TryGetActionTrace(
                    packet.ActorId,
                    packet.ActionKind,
                    packet.SlotIndex,
                    packet.TargetX,
                    packet.TargetY,
                    packet.ClientSendTimeMs,
                    out var trace))
            {
                return;
            }

            long deltaMs = Math.Max(0, P2PRelayDiagnosticsPackets.NowMs() - trace.InputAtMs);
            trace.LastUpdatedAtMs = P2PRelayDiagnosticsPackets.NowMs();

            switch ((P2PActionTraceStage)packet.StageCode)
            {
                case P2PActionTraceStage.HostSeen:
                    trace.HostStatus = $"Seen +{deltaMs}ms";
                    AddEventUnsafe("HOST", $"{trace.ActionName} actor={trace.ActorId} +{deltaMs}ms");
                    break;

                case P2PActionTraceStage.Judge:
                    trace.JudgeStatus = DescribeJudgeReason((P2PActionTraceReason)packet.ReasonCode, packet.ExecuteBeat, packet.DetailValue);
                    AddEventUnsafe("JUDGE", $"{trace.ActionName} {trace.JudgeStatus}");
                    break;

                case P2PActionTraceStage.MoveResult:
                    trace.ResultStatus = DescribeMoveResultReason((P2PActionTraceReason)packet.ReasonCode, packet.ResultX, packet.ResultY);
                    trace.Completed = true;
                    AddEventUnsafe("RESULT", $"{trace.ActionName} {trace.ResultStatus}");
                    break;
            }
        }
    }

    internal static void RecordBeatResult(int actorId, int actionKind, bool accepted, int fromX, int fromY, int toX, int toY)
    {
        lock (_gate)
        {
            if (!TryFindLatestActionTrace(actorId, actionKind, out var trace))
                return;

            trace.LastUpdatedAtMs = P2PRelayDiagnosticsPackets.NowMs();
            trace.ResultStatus = actionKind == (int)ActionKind.Move
                ? (accepted ? $"Beat Ack -> ({toX},{toY})" : $"Beat Reject -> ({fromX},{fromY})")
                : (accepted ? "Beat Ack" : "Beat Reject");
            trace.Completed = true;
        }
    }

    internal static bool HasPendingHostSeenTimeout(out long ageMs, out string summary)
    {
        lock (_gate)
        {
            ageMs = 0;
            summary = "";

            for (int i = _actionHistory.Count - 1; i >= 0; i--)
            {
                var trace = _actionHistory[i];
                if (trace == null || trace.Completed || !string.Equals(trace.HostStatus, "Pending", StringComparison.Ordinal))
                    continue;

                ageMs = Math.Max(0, P2PRelayDiagnosticsPackets.NowMs() - trace.InputAtMs);
                if (ageMs < HostSeenTimeoutMs)
                    return false;

                summary = $"{trace.ActionName} actor={trace.ActorId} target=({trace.TargetX},{trace.TargetY}) age={ageMs}ms";
                return true;
            }

            return false;
        }
    }

    internal static void MarkPendingHostSeenTimeout()
    {
        lock (_gate)
        {
            for (int i = _actionHistory.Count - 1; i >= 0; i--)
            {
                var trace = _actionHistory[i];
                if (trace == null || trace.Completed || !string.Equals(trace.HostStatus, "Pending", StringComparison.Ordinal))
                    continue;

                long ageMs = Math.Max(0, P2PRelayDiagnosticsPackets.NowMs() - trace.InputAtMs);
                trace.HostStatus = $"Timeout {ageMs}ms";
                trace.LastUpdatedAtMs = P2PRelayDiagnosticsPackets.NowMs();
                AddEventUnsafe("HOST", $"{trace.ActionName} timeout {ageMs}ms");
                return;
            }
        }
    }

    internal static List<string> BuildReportLines(int maxEvents = 0)
    {
        lock (_gate)
        {
            var lines = new List<string>(8)
            {
                $"Context: {_context}"
            };

            var bridge = P2PRelayClientBridge.HasInstance ? P2PRelayClientBridge.Instance : null;
            if (bridge != null && bridge.IsP2PMode)
            {
                lines.Add(
                    $"Direct: phase={Safe(bridge.SteamConnectionPhase)} route={Safe(bridge.SteamRouteHint)} ping={Safe(bridge.HostPingStatus)} fallback={Safe(bridge.FallbackReason)}");
            }

            if (TryGetLatestActionTrace(out var latest))
            {
                long ageMs = Math.Max(0, P2PRelayDiagnosticsPackets.NowMs() - latest.InputAtMs);
                string hostStatus = latest.HostStatus;
                if (string.Equals(hostStatus, "Pending", StringComparison.Ordinal))
                    hostStatus = ageMs >= HostSeenTimeoutMs ? $"Pending {ageMs}ms (uplink suspect)" : $"Pending {ageMs}ms";

                lines.Add(
                    $"LastAction: {latest.ActionName} actor={latest.ActorId} target=({latest.TargetX},{latest.TargetY}) slot={latest.SlotIndex} sendMs={latest.ClientSendTimeMs}");
                lines.Add($"LastSend: {latest.SendStatus}");
                lines.Add($"HostSeen: {hostStatus}");
                lines.Add($"Judge: {latest.JudgeStatus}");
                lines.Add($"Result: {latest.ResultStatus}");
            }
            else
            {
                lines.Add("LastAction: -");
            }

            lines.Add(
                $"Counters: input {_inputAttemptCount} | block {_inputBlockedCount} | steamSend {_steamSendCount} | steamFail {_steamSendFailCount} | relaySend {_relaySendCount} | directStart {_steamAttemptStartCount} | directConn {_steamAttemptConnectedCount} | directDisc {_steamAttemptDisconnectedCount} | fallback {_fallbackCount}");

            int eventCount = Math.Min(Math.Max(0, maxEvents), _events.Count);
            if (eventCount > 0)
            {
                var recent = new List<DiagnosticEvent>(_events);
                int start = Math.Max(0, recent.Count - eventCount);
                for (int i = start; i < recent.Count; i++)
                {
                    var entry = recent[i];
                    lines.Add($"{entry.Time} [{entry.Category}] {entry.Message}");
                }
            }

            return lines;
        }
    }

    private static bool TryExtractActionRequest(byte[] payloadBytes, out CS_ActionRequest req)
    {
        req = null;
        if (payloadBytes == null || payloadBytes.Length < 4)
            return false;

        if (P2PRelayDiagnosticsPackets.PeekProtocol(payloadBytes) != (ushort)PacketID.CS_ActionRequest)
            return false;

        try
        {
            req = new CS_ActionRequest();
            req.Read(new ArraySegment<byte>(payloadBytes));
            return true;
        }
        catch
        {
            req = null;
            return false;
        }
    }

    private static bool TryGetLatestActionTrace(out LocalActionTrace trace)
    {
        trace = null;
        if (_actionHistory.Count <= 0)
            return false;

        trace = _actionHistory[_actionHistory.Count - 1];
        return trace != null;
    }

    private static bool TryGetActionTrace(int actorId, int actionKind, int slotIndex, int targetX, int targetY, long clientSendTimeMs, out LocalActionTrace trace)
    {
        return _actionsByKey.TryGetValue(
            BuildActionKey(actorId, actionKind, slotIndex, targetX, targetY, clientSendTimeMs),
            out trace);
    }

    private static bool TryFindLatestActionTrace(int actorId, int actionKind, out LocalActionTrace trace)
    {
        trace = null;
        for (int i = _actionHistory.Count - 1; i >= 0; i--)
        {
            var candidate = _actionHistory[i];
            if (candidate == null || candidate.ActorId != actorId)
                continue;

            bool isSkillLike = actionKind == (int)ActionKind.Attack || actionKind == (int)ActionKind.Skill;
            bool candidateSkillLike = candidate.ActionKind == (int)ActionKind.Attack || candidate.ActionKind == (int)ActionKind.Skill;
            if (isSkillLike == candidateSkillLike)
            {
                trace = candidate;
                return true;
            }

            if (candidate.ActionKind == actionKind)
            {
                trace = candidate;
                return true;
            }
        }

        return false;
    }

    private static string BuildActionKey(int actorId, int actionKind, int slotIndex, int targetX, int targetY, long clientSendTimeMs)
    {
        return $"{actorId}:{actionKind}:{slotIndex}:{targetX}:{targetY}:{clientSendTimeMs}";
    }

    private static string DescribeJudgeReason(P2PActionTraceReason reason, long executeBeat, int detailValue)
    {
        switch (reason)
        {
            case P2PActionTraceReason.AcceptMove:
                return $"Accept Move beat={executeBeat}";
            case P2PActionTraceReason.AcceptMoveLate:
                return $"Accept MoveLate beat={executeBeat}";
            case P2PActionTraceReason.AcceptSkill:
                return $"Accept Skill beat={executeBeat}";
            case P2PActionTraceReason.AcceptSkillLate:
                return $"Accept SkillLate beat={executeBeat}";
            case P2PActionTraceReason.AcceptSkillCatchUp:
                return $"Accept SkillCatchUp beat={executeBeat}";
            case P2PActionTraceReason.RejectHostOrDepsMissing:
                return "Reject HostOrDepsMissing";
            case P2PActionTraceReason.RejectActorNotFound:
                return "Reject ActorNotFound";
            case P2PActionTraceReason.RejectActorDead:
                return "Reject ActorDead";
            case P2PActionTraceReason.RejectCurrentBeatInvalid:
                return "Reject CurrentBeatInvalid";
            case P2PActionTraceReason.RejectJudgeWindow:
                return $"Reject JudgeWindow diff={detailValue}ms beat={executeBeat}";
            case P2PActionTraceReason.RejectDuplicateBeat:
                return $"Reject DuplicateBeat beat={executeBeat}";
            default:
                return "Pending";
        }
    }

    private static string DescribeMoveResultReason(P2PActionTraceReason reason, int resultX, int resultY)
    {
        switch (reason)
        {
            case P2PActionTraceReason.MoveApplied:
                return $"Move Applied -> ({resultX},{resultY})";
            case P2PActionTraceReason.MoveSameTile:
                return "Move Reject SameTile";
            case P2PActionTraceReason.MoveBlockedTile:
                return "Move Reject BlockedTile";
            case P2PActionTraceReason.MoveOccupied:
                return "Move Reject Occupied";
            default:
                return "Move Reject";
        }
    }

    private static void AddEventUnsafe(string category, string message)
    {
        _events.Enqueue(new DiagnosticEvent
        {
            Time = DateTime.Now.ToString("HH:mm:ss.fff"),
            Category = Safe(category),
            Message = Trim(message)
        });

        while (_events.Count > MaxEventCount)
            _events.Dequeue();
    }

    private static string Trim(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "-";

        string trimmed = value.Trim();
        return trimmed.Length > MaxMessageLength
            ? trimmed.Substring(0, MaxMessageLength) + "..."
            : trimmed;
    }

    private static string Safe(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }
}

[DefaultExecutionOrder(-995)]
public sealed class SteamP2PDebugHud : MonoBehaviour
{
    public static SteamP2PDebugHud Ensure(bool visibleByDefault)
    {
        if (!P2PDebugConfig.DebugUiEnabled)
        {
            SetVisible(false);
            return _instance;
        }

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

    public static void SetDebugUiEnabled(bool enabled)
    {
        if (!enabled)
            SetVisible(false);
    }

    public static void SetVisible(bool visible)
    {
        if (_instance != null)
            _instance._visible = visible && P2PDebugConfig.DebugUiEnabled;
    }

    private static SteamP2PDebugHud _instance;

    [SerializeField] private Rect _windowRect = new Rect(18f, 18f, 620f, 470f);
    [SerializeField] private bool _visible = true;

    private Vector2 _scroll;
    private GUIStyle _textStyle;
    private const KeyCode ToggleWindowKey = KeyCode.F8;
    private const string ToggleWindowKeyName = "Ctrl+Alt+F8";

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
        if (!P2PDebugConfig.DebugUiEnabled)
        {
            _visible = false;
            return;
        }

        P2PDebugConfig.PollRuntimeToggle();
        P2PDebugViewConfig.PollRuntimeToggles();

        if (RuntimeHotkey.WasPressed(ToggleWindowKey, requireCtrl: true, requireAlt: true))
            _visible = !_visible;
    }

    private void OnGUI()
    {
        if (!P2PDebugConfig.DebugUiEnabled || !_visible)
            return;

        EnsureStyles();
        _windowRect = GUILayout.Window(991244, _windowRect, DrawWindow, "Steam / P2P Debug");
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{ToggleWindowKeyName} Window", GUILayout.Width(150f));
        GUILayout.Label($"{P2PDebugViewConfig.ToggleSteamSectionKeyName} Steam", GUILayout.Width(160f));
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

        AppendHeader(sb, "Debug Toggles");
        AppendField(sb, "Window", $"ON ({ToggleWindowKeyName})");
        AppendField(sb, "SteamSection", P2PDebugViewConfig.ShowSteamSections
            ? $"ON ({P2PDebugViewConfig.ToggleSteamSectionKeyName})"
            : $"OFF ({P2PDebugViewConfig.ToggleSteamSectionKeyName})");

        if (P2PDebugViewConfig.ShowSteamSections)
        {
            AppendHeader(sb, "Steam");
            AppendField(sb, "Enabled", steam != null && steam.Enabled ? "YES" : "NO");
            AppendField(sb, "Initialized", steam != null && steam.IsInitialized ? "YES" : "NO");
            AppendField(sb, "AppId", config?.SteamAppId ?? "-");
            AppendField(sb, "SteamId64", steam?.SteamId64 ?? "-");
            AppendField(sb, "Name", steam?.DisplayName ?? "-");
            AppendField(sb, "Lobby", steam != null && steam.HasJoinedLobby ? steam.CurrentLobbyId : "Not Joined");
            AppendField(sb, "LobbyOwner", steam != null && steam.IsLobbyOwner ? "YES" : "NO");
            AppendField(sb, "LastError", string.IsNullOrWhiteSpace(steam?.LastError) ? "-" : steam.LastError);
        }

        AppendHeader(sb, "Auth");
        AppendField(sb, "Mode", auth?.LastPreferredLoginMode ?? "Unknown");
        AppendField(sb, "Detail", auth?.LastPreferredLoginDetail ?? "-");

        AppendHeader(sb, "Waiting Room");
        if (room != null && room.IsUiOpen)
        {
            AppendField(sb, "RoomId", string.IsNullOrWhiteSpace(room.CurrentRoomId) ? "-" : room.CurrentRoomId);
            AppendField(sb, "Members", room.MemberCount.ToString());
            if (P2PDebugViewConfig.ShowSteamSections)
            {
                AppendField(sb, "SteamLobbyId", string.IsNullOrWhiteSpace(room.CurrentSteamLobbyId) ? "-" : room.CurrentSteamLobbyId);
                AppendField(sb, "LobbyStatus", room.SteamLobbyStatus);
            }
            AppendField(sb, "Probe", room.LastWaitingProbeRttMs >= 0 ? $"{room.LastWaitingProbeRttMs} ms ({room.LastWaitingProbeStatus})" : room.LastWaitingProbeStatus);
            AppendField(sb, "PreferredHost", string.IsNullOrWhiteSpace(room.PreferredHostUid) ? "-" : $"{room.PreferredHostUid} / epoch {room.PreferredHostEpoch}");
            AppendField(sb, "Selection", string.IsNullOrWhiteSpace(room.HostSelectionMode) ? "-" : $"{room.HostSelectionMode} / {room.HostSelectionMetricVersion}");
            AppendField(sb, "SelectionScore", room.HostSelectionScore >= 0f ? room.HostSelectionScore.ToString("F3") : "-");
            AppendField(sb, "CandidateOrder", room.HostCandidateOrderSummary);
            AppendField(sb, "RoomWarn", string.IsNullOrWhiteSpace(room.LastWarningText) ? "-" : room.LastWarningText);

            if (RoomNetworkDebugFormatter.HasWaitingRoomDetails(room))
            {
                AppendHeader(sb, "Waiting Room Detail");
                AppendLines(sb, RoomNetworkDebugFormatter.BuildDetailedReportLines(room, maxCandidates: 4, maxPairs: 6));
            }
        }
        else
        {
            AppendField(sb, "State", "Room UI closed");
        }

        if (room != null && !room.IsUiOpen && RoomNetworkDebugFormatter.HasWaitingRoomDetails(room))
        {
            AppendHeader(sb, "Waiting Room Detail");
            AppendLines(sb, RoomNetworkDebugFormatter.BuildDetailedReportLines(room, maxCandidates: 4, maxPairs: 6));
        }

        AppendHeader(sb, "Match / Transport");
        AppendField(sb, "LogOverhead", P2PDebugConfig.LogOverheadEnabled
            ? $"ON ({P2PDebugConfig.ToggleLogOverheadKeyName})"
            : $"OFF ({P2PDebugConfig.ToggleLogOverheadKeyName})");
        AppendField(sb, "Manifest", manifest != null ? $"{manifest.NetworkMode} / {manifest.MatchId}" : "No active manifest");
        if (manifest != null)
        {
            AppendField(sb, "ManifestHost", P2PDebugViewConfig.ShowSteamSections
                ? $"{manifest.HostUid} / {manifest.HostSteamId64}"
                : manifest.HostUid);
            AppendField(sb, "ManifestEpoch", manifest.HostEpoch.ToString());
            AppendField(sb, "HostSelection", $"{bridge?.HostSelectionModeSummary ?? manifest.HostSelectionMode} / {bridge?.HostSelectionMetricVersion ?? manifest.HostSelectionMetricVersion}");
            AppendField(sb, "SelectionEpoch", (bridge?.HostSelectionEpoch ?? manifest.HostSelectionEpoch).ToString());
            AppendField(sb, "SelectionScore", (bridge?.HostSelectionScore ?? manifest.HostSelectionScore).ToString("F3"));
            AppendField(sb, "CandidateOrder", bridge != null ? bridge.HostCandidateOrderSummary : (manifest.HostCandidateOrder != null && manifest.HostCandidateOrder.Count > 0 ? string.Join(" > ", manifest.HostCandidateOrder) : "-"));
        }

        if (bridge != null && bridge.IsP2PMode)
        {
            AppendField(sb, "Transport", bridge.TransportName);
            AppendField(sb, "Role", bridge.IsHostLocal ? "Host" : "Guest");
            AppendField(sb, "State", bridge.TransportDebugStatus);
            AppendField(sb, "NetworkState", bridge.NetworkStateSummary);
            AppendField(sb, "NetworkFlow", bridge.NetworkFlowSummary);
            AppendField(sb, "HostAuthority", bridge.HostAuthorityDebugState);
            AppendField(sb, "ServerRole", bridge.ServerRoleSummary);
            AppendField(sb, "RelayKey", string.IsNullOrWhiteSpace(bridge.RelayKey) ? "-" : bridge.RelayKey);
            AppendField(sb, "Host", P2PDebugViewConfig.ShowSteamSections
                ? $"{bridge.HostUid} / {bridge.HostSteamId64} / actor {bridge.HostActorId}"
                : $"{bridge.HostUid} / actor {bridge.HostActorId}");
            AppendField(sb, "HostEpoch", bridge.HostEpoch.ToString());
            AppendField(sb, "Peers", bridge.SteamConnectedPeerCount.ToString());
            AppendField(sb, "ToHost", bridge.IsSteamTransport ? (bridge.IsSteamTransportConnectedToHost ? "Connected" : "Not Connected") : "-");
            AppendField(sb, "RouteHint", bridge.IsSteamTransport ? bridge.SteamRouteHint : bridge.TransportPairRouteHint);
            AppendField(sb, "Retry", bridge.IsSteamTransport
                ? $"attempts {bridge.SteamConnectAttemptCount} / retries {bridge.SteamRetryCount} / nextBackoff {bridge.SteamRetryBackoffMs} ms"
                : "-");
            AppendField(sb, "RetryTimeline", bridge.IsSteamTransport
                ? $"start {FormatTimestampMs(bridge.SteamInitialConnectAttemptAtMs)} / last {FormatTimestampMs(bridge.SteamLastConnectAttemptAtMs)} / connected {FormatTimestampMs(bridge.SteamConnectedAtMs)}"
                : "-");
            AppendField(sb, "Fallback", bridge.IsSteamTransport
                ? $"{bridge.FallbackReason} / at {FormatTimestampMs(bridge.FallbackActivatedAtMs)} / recovery {FormatTimestampMs(bridge.RecoveryObservedAtMs)}"
                : "-");
            AppendField(sb, "Ping", bridge.IsHostLocal ? "Local Host" : FormatPingSummary(bridge));
            AppendField(sb, "TransportError", string.IsNullOrWhiteSpace(bridge.TransportLastError) ? "-" : bridge.TransportLastError);

            if (P2PDebugViewConfig.ShowSteamSections)
            {
                AppendField(sb, "SteamDecision", bridge.SteamTransportDecisionReason);
                AppendField(sb, "LocalSteamId", string.IsNullOrWhiteSpace(bridge.LocalSteamId64) ? "-" : bridge.LocalSteamId64);
                AppendField(sb, "SteamPhase", bridge.IsSteamTransport ? bridge.SteamConnectionPhase : "-");
                AppendField(sb, "Detail", bridge.IsSteamTransport ? bridge.SteamDetailedStatusSnippet : bridge.TransportPairDetail);
            }
        }
        else
        {
            AppendField(sb, "State", "No active P2P match");
        }

        AppendHeader(sb, "Input / Direct Trace");
        AppendLines(sb, P2PTransportDiagnostics.BuildReportLines(0));

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
            return $"This match started on ServerRelay, not Steam P2P. Decision={bridge.SteamTransportDecisionReason}";

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

    private static void AppendLines(StringBuilder sb, IEnumerable<string> lines)
    {
        if (lines == null)
            return;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            sb.AppendLine(line);
        }
    }

    private static string FormatTimestampMs(long timestampMs)
    {
        return timestampMs > 0 ? timestampMs.ToString() : "-";
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
