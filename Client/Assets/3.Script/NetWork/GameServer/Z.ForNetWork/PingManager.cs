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
    [SerializeField, ReadOnly] long lastJitterMs;
    [SerializeField, ReadOnly] long avgJitterMs;
    
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
    public long LastRttMs => lastRttMs;
    public long AvgRttMs => avgRttMs;
    public long LastJitterMs => lastJitterMs;
    public long AvgJitterMs => avgJitterMs;
    public float PacketLossPercent => packetLossPercent;

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
        lastJitterMs = 0;
        avgJitterMs = 0;
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

        var prevRtt = lastRttMs;
        lastJitterMs = prevRtt > 0 ? Math.Abs(rtt - prevRtt) : 0;
        if (avgJitterMs == 0) avgJitterMs = lastJitterMs;
        else avgJitterMs = (long)(EMA_ALPHA * lastJitterMs + (1 - EMA_ALPHA) * avgJitterMs);

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
        var richLines = new List<string>(32);
        var plainLines = new List<string>(32);
        var steam = AppBootstrap.Instance != null && AppBootstrap.Instance.Root != null
            ? AppBootstrap.Instance.Root.SteamPlatform
            : null;

        AddSyncSection(richLines, plainLines, "[Network Sync]", "<b><color=#ffffff>[Network Sync]</color></b>");
        AddSyncSection(richLines, plainLines, "[Transport]", "<b><color=#7fffd4>[Transport]</color></b>");

        if (relayBridge != null && relayBridge.IsRelayMode)
        {
            AddSyncLine(
                richLines,
                plainLines,
                $"Path: {DescribeTransportPath(relayBridge)}",
                $"<color=#7bdff2>Path:</color> {DescribeTransportPath(relayBridge)}");
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

        AddSyncSection(richLines, plainLines, "[Latency]", "<b><color=#98fb98>[Latency]</color></b>");
        AddSyncLine(
            richLines,
            plainLines,
            $"Validation RTT: {avgRttMs}ms avg (Last:{lastRttMs} Max:{maxRttMs} Loss:{packetLossPercent:F1}%)",
            $"<color=#00ff7f>Validation RTT:</color> {avgRttMs}ms avg (Last:{lastRttMs} Max:{maxRttMs} Loss:{packetLossPercent:F1}%)");
        AddSyncLine(
            richLines,
            plainLines,
            "Validation Source: App ping over TCP validation socket",
            "<color=#5fd7ff>Validation Source:</color> App ping over TCP validation socket");

        if (relayBridge != null && relayBridge.IsRelayMode)
        {
            if (relayBridge.IsSteamTransport && relayBridge.HasTransportPairStats)
            {
                AddSyncLine(
                    richLines,
                    plainLines,
                    $"Transport Pair RTT: {relayBridge.TransportPairPingMs}ms",
                    $"<color=#00fa9a>Transport Pair RTT:</color> {relayBridge.TransportPairPingMs}ms");
                AddSyncLine(
                    richLines,
                    plainLines,
                    $"Transport Queue: P.Rel {relayBridge.TransportPendingReliableBytes} | P.Unrel {relayBridge.TransportPendingUnreliableBytes} | UnackedRel {relayBridge.TransportSentUnackedReliableBytes}",
                    $"<color=#20b2aa>Transport Queue:</color> P.Rel {relayBridge.TransportPendingReliableBytes} | P.Unrel {relayBridge.TransportPendingUnreliableBytes} | UnackedRel {relayBridge.TransportSentUnackedReliableBytes}");
                AddSyncLine(
                    richLines,
                    plainLines,
                    $"Transport Quality: local {FormatRatioPercent(relayBridge.TransportConnectionQualityLocal)} / remote {FormatRatioPercent(relayBridge.TransportConnectionQualityRemote)}",
                    $"<color=#48cae4>Transport Quality:</color> local {FormatRatioPercent(relayBridge.TransportConnectionQualityLocal)} / remote {FormatRatioPercent(relayBridge.TransportConnectionQualityRemote)}");
            }

            AddSyncLine(
                richLines,
                plainLines,
                $"Host Path: {DescribeHostLatencyPath(relayBridge)}",
                $"<color=#f9c74f>Host Path:</color> {DescribeHostLatencyPath(relayBridge)}");

            string hostPingPlain = relayBridge.IsHostLocal
                ? "Host Echo RTT: Local Host"
                : $"Host Echo RTT: {relayBridge.HostAvgRttMs}ms avg (Last:{relayBridge.HostLastRttMs} Max:{relayBridge.HostMaxRttMs})";
            string hostPingRich = relayBridge.IsHostLocal
                ? "<color=#ffa500>Host Echo RTT:</color> Local Host"
                : $"<color=#ffa500>Host Echo RTT:</color> {relayBridge.HostAvgRttMs}ms avg (Last:{relayBridge.HostLastRttMs} Max:{relayBridge.HostMaxRttMs})";
            AddSyncLine(richLines, plainLines, hostPingPlain, hostPingRich);

            if (!relayBridge.IsHostLocal)
            {
                AddSyncLine(
                    richLines,
                    plainLines,
                    $"Host Probe: RawRTT {relayBridge.HostLastRawRttMs}ms | HostProc {relayBridge.HostLastProcMs}ms",
                    $"<color=#ffd166>Host Probe:</color> RawRTT {relayBridge.HostLastRawRttMs}ms | HostProc {relayBridge.HostLastProcMs}ms");
                AddSyncLine(
                    richLines,
                    plainLines,
                    $"Host Source: {DescribeHostLatencySource(relayBridge)}",
                    $"<color=#ff9f1c>Host Source:</color> {DescribeHostLatencySource(relayBridge)}");
            }

            if (relayBridge.GameplayPendingActionCount > 0
                || relayBridge.GameplayAvgStartRttMs > 0
                || relayBridge.GameplayAvgResultRttMs > 0)
            {
                AddSyncLine(
                    richLines,
                    plainLines,
                    $"Gameplay Start RTT: {FormatGameplayRtt(relayBridge.GameplayAvgStartRttMs, relayBridge.GameplayLastStartRttMs, relayBridge.GameplayMaxStartRttMs)}",
                    $"<color=#f4a261>Gameplay Start RTT:</color> {FormatGameplayRtt(relayBridge.GameplayAvgStartRttMs, relayBridge.GameplayLastStartRttMs, relayBridge.GameplayMaxStartRttMs)}");
                AddSyncLine(
                    richLines,
                    plainLines,
                    $"Gameplay Result RTT: {FormatGameplayRtt(relayBridge.GameplayAvgResultRttMs, relayBridge.GameplayLastResultRttMs, relayBridge.GameplayMaxResultRttMs)}",
                    $"<color=#e76f51>Gameplay Result RTT:</color> {FormatGameplayRtt(relayBridge.GameplayAvgResultRttMs, relayBridge.GameplayLastResultRttMs, relayBridge.GameplayMaxResultRttMs)}");
                AddSyncLine(
                    richLines,
                    plainLines,
                    $"Gameplay Telemetry: {relayBridge.GameplayTelemetryStatus} (pending {relayBridge.GameplayPendingActionCount})",
                    $"<color=#ffb4a2>Gameplay Telemetry:</color> {relayBridge.GameplayTelemetryStatus} (pending {relayBridge.GameplayPendingActionCount})");
            }

            AddSyncLine(
                richLines,
                plainLines,
                $"Host Status: {relayBridge.HostPingStatus}",
                $"<color=#ffd27f>Host Status:</color> {relayBridge.HostPingStatus}");
        }

        if (relayBridge != null && relayBridge.IsRelayMode)
        {
            AddSyncSection(richLines, plainLines, "[Host Selection]", "<b><color=#90ee90>[Host Selection]</color></b>");
            AddSyncLine(
                richLines,
                plainLines,
                $"Host: actor {relayBridge.HostActorId} / uid {FormatDebugValue(relayBridge.HostUid)} / epoch {relayBridge.HostEpoch}",
                $"<color=#ff9ff3>Host:</color> actor {relayBridge.HostActorId} / uid {FormatDebugValue(relayBridge.HostUid)} / epoch {relayBridge.HostEpoch}");
            AddSyncLine(
                richLines,
                plainLines,
                $"Selection: {relayBridge.HostSelectionModeSummary} / {relayBridge.HostSelectionMetricVersion} / epoch {relayBridge.HostSelectionEpoch}",
                $"<color=#90ee90>Selection:</color> {relayBridge.HostSelectionModeSummary} / {relayBridge.HostSelectionMetricVersion} / epoch {relayBridge.HostSelectionEpoch}");
            AddSyncLine(
                richLines,
                plainLines,
                $"Metric Note: {DescribeSelectionMetricNote(relayBridge.HostSelectionMetricVersion)}",
                $"<color=#c7f9cc>Metric Note:</color> {DescribeSelectionMetricNote(relayBridge.HostSelectionMetricVersion)}");
            AddSyncLine(
                richLines,
                plainLines,
                $"SelectionScore: {FormatSelectionScore(relayBridge.HostSelectionScore)}",
                $"<color=#ffdead>SelectionScore:</color> {FormatSelectionScore(relayBridge.HostSelectionScore)}");
            AddSyncLine(
                richLines,
                plainLines,
                $"CandidateOrder: {relayBridge.HostCandidateOrderSummary}",
                $"<color=#f0e68c>CandidateOrder:</color> {relayBridge.HostCandidateOrderSummary}");
        }

        AddSyncSection(richLines, plainLines, "[Steam Runtime]", "<b><color=#dda0dd>[Steam Runtime]</color></b>");
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
        else
        {
            AddSyncLine(
                richLines,
                plainLines,
                "Steam Runtime: unavailable",
                "<color=#adff2f>Steam Runtime:</color> unavailable");
        }

        AddSyncSection(richLines, plainLines, "[Clock Sync]", "<b><color=#ffff99>[Clock Sync]</color></b>");
        AddSyncLine(
            richLines,
            plainLines,
            $"Validation Probe: RawRTT {lastRawRTT}ms | ServerProc {lastServerProc}ms",
            $"<color=#00e5ee>Validation Probe:</color> RawRTT {lastRawRTT}ms | <color=#ff69b4>ServerProc</color> {lastServerProc}ms");
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

    private static void AddSyncSection(List<string> richLines, List<string> plainLines, string plainTitle, string richTitle)
    {
        if (plainLines.Count > 0)
        {
            plainLines.Add("");
            richLines.Add("");
        }

        plainLines.Add(plainTitle);
        richLines.Add(string.IsNullOrWhiteSpace(richTitle) ? plainTitle : richTitle);
    }

    private static string FormatDebugValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static string FormatSelectionScore(float value)
    {
        return value >= 0f ? value.ToString("F3") : "-";
    }

    private static string FormatRatioPercent(float value)
    {
        return value < 0f ? "-" : $"{value * 100f:F0}%";
    }

    private static string FormatGameplayRtt(long avg, long last, long max)
    {
        if (avg <= 0 && last <= 0 && max <= 0)
            return "-";

        return $"{avg}ms avg (Last:{last} Max:{max})";
    }

    private static string DescribeTransportPath(P2PRelayClientBridge relayBridge)
    {
        if (relayBridge == null || !relayBridge.IsRelayMode)
            return "Dedicated client <-> game server socket";

        if (relayBridge.IsSteamTransport)
            return "Steam relay socket (Valve relay network)";

        if (relayBridge.IsServerRelayTransport)
            return "Application server relay path";

        return "P2P path";
    }

    private static string DescribeHostLatencyPath(P2PRelayClientBridge relayBridge)
    {
        if (relayBridge == null || !relayBridge.IsRelayMode)
            return "Dedicated game server";

        if (relayBridge.IsHostLocal)
            return "Local host authority";

        if (relayBridge.IsSteamTransport)
            return "Guest <-> Host over Steam relay socket";

        if (relayBridge.IsServerRelayTransport)
            return "Guest <-> Host via application server relay";

        return "Guest <-> Host";
    }

    private static string DescribeHostLatencySource(P2PRelayClientBridge relayBridge)
    {
        if (relayBridge == null || !relayBridge.IsRelayMode)
            return "No host probe";

        if (relayBridge.IsSteamTransport)
            return "Host echo over Reliable Steam payload";

        if (relayBridge.IsServerRelayTransport)
            return "App ping over server relay payload";

        return "App ping";
    }

    private static string DescribeSelectionMetricNote(string metricVersion)
    {
        if (string.IsNullOrWhiteSpace(metricVersion))
            return "Unknown metric source";

        if (metricVersion.IndexOf("v2", StringComparison.OrdinalIgnoreCase) >= 0
            || metricVersion.IndexOf("pair", StringComparison.OrdinalIgnoreCase) >= 0
            || metricVersion.IndexOf("hybrid", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Uses measured Steam pair RTT when available, with proxy fallback for missing or stale pairs";
        }

        if (metricVersion.IndexOf("proxy", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Proxy estimate from each client's server RTT, not a measured Steam pair ping";

        return "Metric is not marked as proxy";
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
