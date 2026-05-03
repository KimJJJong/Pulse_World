using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class PingManager : MonoBehaviour
{
    public static PingManager Instance { get; private set; }

    [Header("Ping Settings")]
    [Tooltip("핑 간격(ms)")]
    [Min(100)]
    [SerializeField] int intervalMs = 2000;

    [Tooltip("Pong 타임아웃(ms)")]
    [Min(500)]
    [SerializeField] int timeoutMs = 6000;

    [Tooltip("연속 미수신 허용 횟수")]
    [SerializeField] int maxConsecMiss = 3;

    [Header("Events")]
    public UnityEvent onTimeout;
    public UnityEvent onRecovered;

    // --- 런타임 상태 (인스펙터 표시용, 스크립트에서 set만)
    [Header("Runtime (Read Only)")]
    [SerializeField, ReadOnly] bool running;
    [SerializeField, ReadOnly] int seq;
    [SerializeField, ReadOnly] int missCount;
    [SerializeField, ReadOnly] long lastPongAtMs;
    [SerializeField, ReadOnly] long lastRttMs;
    [SerializeField, ReadOnly] long avgRttMs;
    [SerializeField, ReadOnly] long maxRttMs;
    [SerializeField, ReadOnly] long minRttMs = long.MaxValue;
    [SerializeField, ReadOnly] float packetLossPercent;
    [SerializeField, ReadOnly] string status = "Idle";
    
    [Header("Detailed Debug")]
    [SerializeField, ReadOnly] long lastRawRTT;
    [SerializeField, ReadOnly] long lastServerProc;
    [SerializeField, ReadOnly] long lastLocalSend;
    [SerializeField, ReadOnly] long lastLocalRecv;

    // 내부
    CancellationTokenSource cts;
    long lastSentAtMs;
    long receivedCount;
    long sentCount;
    const float EMA_ALPHA = 0.2f; // RTT 지수이동평균

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool IsRunning => running;

    [ContextMenu("StartLoop")]
    public void StartLoop()
    {
        if (running) return;
        ResetRuntime();
        cts = new CancellationTokenSource();
        running = true;
        status = "Running";
        _ = LoopAsync(cts.Token);
    }

    [ContextMenu("StopLoop")]
    public void StopLoop()
    {
        if (!running) return;
        cts?.Cancel(); cts = null;
        running = false;
        status = "Stopped";
    }

    void ResetRuntime()
    {
        missCount = 0;
        lastPongAtMs = 0;
        lastRttMs = 0;
        avgRttMs = 0;
        maxRttMs = 0;
        minRttMs = long.MaxValue;
        packetLossPercent = 0;
        sentCount = 0;
        receivedCount = 0;
        seq = 0;
        status = "Idle";
    }

    async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var s = Interlocked.Increment(ref seq);
            var now = NowMs();               //  monotonic local time
            lastSentAtMs = now;
            sentCount++;

            NetworkManager.Instance.Send(new CS_Ping { seq = s, clientSendMs = now }.Write());

            // [Extreme Optimization] 초기 웜업 단계 (초기 8번)
            // 핑 응답(Pong)을 기다리지 않고 무자비하게 16ms(1프레임) 간격으로 핑을 쏴서
            // TimeSync의 WarmupCount를 순식간에 채운다.
            if (sentCount <= TimeSync.WarmupCount)
            {
                try { await Task.Delay(16, ct); }
                catch (TaskCanceledException) { break; }
                continue;
            }

            // timeout 대기 (웜업이 끝난 후 정규 핑)
            var deadline = now + timeoutMs;
            while (!ct.IsCancellationRequested && NowMs() < deadline &&
                   Interlocked.Read(ref lastPongAtMs) < now)
            {
                await Task.Yield();
            }
            if (ct.IsCancellationRequested) break;

            if (Interlocked.Read(ref lastPongAtMs) < now)
            {
                missCount++;
                RecomputeLoss();
                if (missCount >= maxConsecMiss)
                {
                    status = "Timeout";
                    onTimeout?.Invoke();
                    StopLoop();
                    break;
                }
                else
                {
                    status = $"Miss {missCount}/{maxConsecMiss}";
                }
            }
            else
            {
                missCount = 0;
                status = "OK";
            }

            try { await Task.Delay(intervalMs, ct); }
            catch (TaskCanceledException) { break; }
        }
    }

    public void OnPong(SC_Pong p)
    {
        var localRecvMs = NowMs(); //  수신 순간 local time (monotonic)
        Interlocked.Exchange(ref lastPongAtMs, localRecvMs);
        receivedCount++;
        RecomputeLoss();

        lastLocalSend = p.clientSendMs;
        lastLocalRecv = localRecvMs;

        // 서버 처리시간(선택): serverSend - serverRecv
        var proc = Math.Max(0, p.serverSendMs - p.serverRecvMs);
        lastServerProc = proc;

        var rawRtt = Math.Max(0, localRecvMs - p.clientSendMs);
        lastRawRTT = rawRtt;

        // RTT 추정: (recv - clientSend) - proc
        var rtt = Math.Max(0, rawRtt - proc);

        lastRttMs = rtt;
        if (avgRttMs == 0) avgRttMs = rtt;
        else avgRttMs = (long)(EMA_ALPHA * rtt + (1 - EMA_ALPHA) * avgRttMs);

        maxRttMs = Math.Max(maxRttMs, rtt);
        minRttMs = Math.Min(minRttMs, rtt);

        //  시간 동기화(오프셋) 갱신: serverNowAtRecv ≈ serverSend + RTT/2
        var oneWay = rtt / 2;
        TimeSync.SetOffsetFromServerNow(p.serverSendMs + oneWay, rtt);

        //Debug.Log($"[PONG] rtt={rtt} offset={TimeSync.OffsetMs:0} serverNow={TimeSync.ServerNowMs()}");


        if (!running)
        {
            running = true;
            status = "Recovered";
            onRecovered?.Invoke();
        }
    }

    void RecomputeLoss()
    {
        if (sentCount == 0) { packetLossPercent = 0; return; }
        var lost = sentCount - receivedCount;
        packetLossPercent = Mathf.Clamp01((float)lost / sentCount) * 100f;
    }

    //  반드시 monotonic(단조시간)로 통일! (Thread-Safe)
    static long NowMs() 
    {
        // [Fix] double 변환 없이 정수형 롱스케일로 연산하여 업타임 긴 기기에서의 정밀도 손실 예방
        return (System.Diagnostics.Stopwatch.GetTimestamp() * 1000L) / System.Diagnostics.Stopwatch.Frequency;
    }

    public void Configure(int? interval = null, int? timeout = null, int? maxMiss = null)
    {
        if (interval.HasValue) intervalMs = Math.Max(100, interval.Value);
        if (timeout.HasValue) timeoutMs = Math.Max(500, timeout.Value);
        if (maxMiss.HasValue) maxConsecMiss = Math.Max(1, maxMiss.Value);
    }

    private void BuildNetworkSyncText(P2PRelayClientBridge relayBridge, out string richText, out string plainText)
    {
        var richLines = new List<string>(16);
        var plainLines = new List<string>(16);
        var steam = AppBootstrap.Instance != null && AppBootstrap.Instance.Root != null
            ? AppBootstrap.Instance.Root.SteamPlatform
            : null;

        AddSyncLine(richLines, plainLines, "[Network Sync]");
        AddSyncLine(
            richLines,
            plainLines,
            $"RTT(Avg): {avgRttMs}ms (Last:{lastRttMs} Max:{maxRttMs} Loss:{packetLossPercent:F1}%)",
            $"<color=#00FF00>RTT(Avg):</color> {avgRttMs}ms (Last:{lastRttMs} Max:{maxRttMs} Loss:{packetLossPercent:F1}%)");

        if (relayBridge != null && relayBridge.IsRelayMode)
        {
            AddSyncLine(
                richLines,
                plainLines,
                $"Mode: {relayBridge.TransportName} / {relayBridge.NetworkStateSummary}",
                $"<color=#7fffd4>Mode:</color> {relayBridge.TransportName} / {relayBridge.NetworkStateSummary}");
            AddSyncLine(
                richLines,
                plainLines,
                $"Flow: {relayBridge.NetworkFlowSummary}",
                $"<color=#87cefa>Flow:</color> {relayBridge.NetworkFlowSummary}");
            AddSyncLine(
                richLines,
                plainLines,
                $"Authority: {relayBridge.HostAuthorityDebugState} / {(relayBridge.IsHostLocal ? "Host" : "Guest")}",
                $"<color=#ffd700>Authority:</color> {relayBridge.HostAuthorityDebugState} / {(relayBridge.IsHostLocal ? "Host" : "Guest")}");
            AddSyncLine(
                richLines,
                plainLines,
                $"Server: {relayBridge.ServerRoleSummary}",
                $"<color=#ffb347>Server:</color> {relayBridge.ServerRoleSummary}");
            AddSyncLine(
                richLines,
                plainLines,
                $"Host: actor {relayBridge.HostActorId} / uid {FormatDebugValue(relayBridge.HostUid)} / epoch {relayBridge.HostEpoch}",
                $"<color=#ff9ff3>Host:</color> actor {relayBridge.HostActorId} / uid {FormatDebugValue(relayBridge.HostUid)} / epoch {relayBridge.HostEpoch}");
            AddSyncLine(
                richLines,
                plainLines,
                $"Transport: {relayBridge.TransportDebugStatus}",
                $"<color=#c8a2c8>Transport:</color> {relayBridge.TransportDebugStatus}");
            AddSyncLine(
                richLines,
                plainLines,
                $"Steam Decision: {relayBridge.SteamTransportDecisionReason}",
                $"<color=#98fb98>Steam Decision:</color> {relayBridge.SteamTransportDecisionReason}");
            AddSyncLine(
                richLines,
                plainLines,
                $"Steam IDs: local {FormatDebugValue(relayBridge.LocalSteamId64)} / host {FormatDebugValue(relayBridge.HostSteamId64)}",
                $"<color=#66d9ef>Steam IDs:</color> local {FormatDebugValue(relayBridge.LocalSteamId64)} / host {FormatDebugValue(relayBridge.HostSteamId64)}");
            AddSyncLine(
                richLines,
                plainLines,
                $"RelayKey: {FormatDebugValue(relayBridge.RelayKey)}",
                $"<color=#b0c4de>RelayKey:</color> {FormatDebugValue(relayBridge.RelayKey)}");

            string hostPingPlain = relayBridge.IsHostLocal
                ? "Host RTT: Local Host"
                : $"Host RTT: {relayBridge.HostAvgRttMs}ms (Last:{relayBridge.HostLastRttMs} Max:{relayBridge.HostMaxRttMs})";
            string hostPingRich = relayBridge.IsHostLocal
                ? "<color=#ffa500>Host RTT:</color> Local Host"
                : $"<color=#ffa500>Host RTT:</color> {relayBridge.HostAvgRttMs}ms (Last:{relayBridge.HostLastRttMs} Max:{relayBridge.HostMaxRttMs})";
            AddSyncLine(richLines, plainLines, hostPingPlain, hostPingRich);
            AddSyncLine(
                richLines,
                plainLines,
                $"Host Status: {relayBridge.HostPingStatus}",
                $"<color=#ffd27f>Host Status:</color> {relayBridge.HostPingStatus}");
        }
        else
        {
            AddSyncLine(
                richLines,
                plainLines,
                "Mode: Dedicated / Client <-> GameServer",
                "<color=#7fffd4>Mode:</color> Dedicated / Client <-> GameServer");
            AddSyncLine(
                richLines,
                plainLines,
                "Server: Dedicated simulation",
                "<color=#ffb347>Server:</color> Dedicated simulation");
        }

        if (steam != null)
        {
            AddSyncLine(
                richLines,
                plainLines,
                $"Steam Runtime: enabled={steam.Enabled} init={steam.IsInitialized} joined={steam.HasJoinedLobby} owner={steam.IsLobbyOwner}",
                $"<color=#adff2f>Steam Runtime:</color> enabled={steam.Enabled} init={steam.IsInitialized} joined={steam.HasJoinedLobby} owner={steam.IsLobbyOwner}");
            AddSyncLine(
                richLines,
                plainLines,
                $"Steam Lobby: {FormatDebugValue(steam.CurrentLobbyId)}",
                $"<color=#dda0dd>Steam Lobby:</color> {FormatDebugValue(steam.CurrentLobbyId)}");
            if (!string.IsNullOrWhiteSpace(steam.LastError))
            {
                AddSyncLine(
                    richLines,
                    plainLines,
                    $"Steam Error: {steam.LastError}",
                    $"<color=#ff6b6b>Steam Error:</color> {steam.LastError}");
            }
        }

        AddSyncLine(
            richLines,
            plainLines,
            $"RawRTT: {lastRawRTT}ms | S.Proc: {lastServerProc}ms",
            $"<color=#00e5ee>RawRTT:</color> {lastRawRTT}ms | <color=#ff69b4>S.Proc:</color> {lastServerProc}ms");
        AddSyncLine(
            richLines,
            plainLines,
            $"Offset: {TimeSync.OffsetMs:F1}ms",
            $"<color=#FFFF00>Offset:</color> {TimeSync.OffsetMs:F1}ms");
        AddSyncLine(
            richLines,
            plainLines,
            $"L.Send: {lastLocalSend}",
            $"<color=#aaaaaa>L.Send:</color> {lastLocalSend}");
        AddSyncLine(
            richLines,
            plainLines,
            $"L.Recv: {lastLocalRecv}",
            $"<color=#aaaaaa>L.Recv:</color> {lastLocalRecv}");
        AddSyncLine(
            richLines,
            plainLines,
            $"Status: {(sentCount <= TimeSync.WarmupCount ? "Warming Up" : status)}",
            $"<color=#00FFFF>Status:</color> {(sentCount <= TimeSync.WarmupCount ? "Warming Up" : status)}");

        richText = string.Join("\n", richLines);
        plainText = string.Join("\n", plainLines);
    }

    private static void AddSyncLine(List<string> richLines, List<string> plainLines, string plainText, string richOverride = null)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return;

        plainLines.Add(plainText);
        richLines.Add(string.IsNullOrWhiteSpace(richOverride) ? plainText : richOverride);
    }

    private static string FormatDebugValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static Vector2 MeasureTextBlock(GUIStyle style, string text, float maxTextWidth)
    {
        if (style == null)
            return Vector2.zero;

        float desiredWidth = 0f;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = string.IsNullOrEmpty(rawLine) ? " " : rawLine;
            desiredWidth = Mathf.Max(desiredWidth, style.CalcSize(new GUIContent(line)).x);
        }

        float width = Mathf.Clamp(desiredWidth, 220f, maxTextWidth);
        float height = style.CalcHeight(new GUIContent(text), width);
        return new Vector2(width, height);
    }

    void OnGUI()
    {
        if (!running) return;

        var relayBridge = P2PRelayClientBridge.HasInstance ? P2PRelayClientBridge.Instance : null;
        BuildNetworkSyncText(relayBridge, out var richText, out var plainText);

        const float screenPadding = 10f;
        const float contentPadding = 12f;
        float maxWindowWidth = Mathf.Max(360f, Mathf.Min(760f, Screen.width * 0.48f));

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            alignment = TextAnchor.UpperLeft,
            richText = true,
            wordWrap = true
        };

        GUIStyle measureStyle = new GUIStyle(style)
        {
            richText = false,
            wordWrap = true
        };

        Vector2 measured = MeasureTextBlock(measureStyle, plainText, Mathf.Max(240f, maxWindowWidth - contentPadding * 2f));
        float width = Mathf.Clamp(measured.x + contentPadding * 2f, 340f, Screen.width - screenPadding * 2f);
        float textWidth = Mathf.Max(220f, width - contentPadding * 2f);
        float height = Mathf.Clamp(
            measureStyle.CalcHeight(new GUIContent(plainText), textWidth) + contentPadding * 2f,
            150f,
            Screen.height - screenPadding * 2f);

        Rect rect = new Rect(Screen.width - width - screenPadding, screenPadding, width, height);
        GUI.Box(rect, "");

        style.normal.textColor = Color.black;
        Rect shadowRect = new Rect(rect.x + contentPadding + 1f, rect.y + contentPadding + 1f, textWidth, height - contentPadding * 2f);
        GUI.Label(shadowRect, richText, style);

        style.normal.textColor = Color.white;
        Rect textRect = new Rect(rect.x + contentPadding, rect.y + contentPadding, textWidth, height - contentPadding * 2f);
        GUI.Label(textRect, richText, style);
    }
}

[AttributeUsage(AttributeTargets.Field)] public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var old = GUI.enabled;
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = old;
    }
}
#endif
