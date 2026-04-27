using System;
using System.Collections.Generic;
using System.Text;
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
        Application.logMessageReceivedThreaded += OnLogMessageReceived;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;

        Application.logMessageReceivedThreaded -= OnLogMessageReceived;
    }

    private void Update()
    {
        if (_visible && Input.GetKeyDown(KeyCode.F9))
            _visible = false;
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

        _visible = _isRelayMode && _isHostLocal;
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
        if (!_isRelayMode || string.IsNullOrWhiteSpace(condition))
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
    internal static readonly bool TraceHostFlow = false;
    internal static readonly bool TraceContent = false;
    internal static readonly bool TraceInput = false;
    internal static readonly bool TraceCombat = false;
    internal static readonly bool CaptureOnlyP2PHostLogs = true;

    internal static bool ShouldCaptureHostLog(string condition, LogType type)
    {
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
