using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

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

    [Header("Runtime (Read Only)")]
    [SerializeField, ReadOnly] bool   running;
    [SerializeField, ReadOnly] int    seq;
    [SerializeField, ReadOnly] int    missCount;
    [SerializeField, ReadOnly] long   lastPongAtMs;
    [SerializeField, ReadOnly] long   lastRttMs;
    [SerializeField, ReadOnly] long   avgRttMs;
    [SerializeField, ReadOnly] long   maxRttMs;
    [SerializeField, ReadOnly] long   minRttMs = long.MaxValue;
    [SerializeField, ReadOnly] float  packetLossPercent;
    [SerializeField, ReadOnly] string status = "Idle";

    // 내부
    CancellationTokenSource cts;
    long  lastSentAtMs;
    long  receivedCount;
    long  sentCount;
    const float EMA_ALPHA = 0.2f;

    // [RTT Fix] Busy-wait(Task.Yield 루프) 제거 -> TCS 기반 즉시 Pong 수신 신호로 교체
    TaskCompletionSource<bool> _pongTcs;

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
        cts     = new CancellationTokenSource();
        running = true;
        status  = "Running";
        _ = LoopAsync(cts.Token);
    }

    [ContextMenu("StopLoop")]
    public void StopLoop()
    {
        if (!running) return;
        cts?.Cancel(); cts = null;
        running = false;
        status  = "Stopped";
    }

    void ResetRuntime()
    {
        missCount         = 0;
        lastPongAtMs      = 0;
        lastRttMs         = 0;
        avgRttMs          = 0;
        maxRttMs          = 0;
        minRttMs          = long.MaxValue;
        packetLossPercent = 0;
        sentCount         = 0;
        receivedCount     = 0;
        seq               = 0;
        status            = "Idle";
    }

    // -------------------------------------------------------
    // [RTT Fix] Task.Yield busy-wait 제거 -> TCS + Task.WhenAny
    // -------------------------------------------------------
    async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // 매 핑마다 새 TCS — 이전 Pong 신호가 다음 핑에 영향 안 주도록
            _pongTcs = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var s   = Interlocked.Increment(ref seq);
            var now = NowMs();
            lastSentAtMs = now;
            sentCount++;

            NetworkManager.Instance.Send(new CS_Ping { seq = s, clientSendMs = now }.Write());

            // 웜업: 16ms 간격으로 WarmupCount 빠르게 채우기
            if (sentCount <= TimeSync.WarmupCount)
            {
                try { await Task.Delay(16, ct); }
                catch (TaskCanceledException) { break; }
                continue;
            }

            // Pong 대기 — CPU 점유 없이 OS 이벤트로만 깨어남
            bool pongReceived;
            try
            {
                var completed = await Task.WhenAny(_pongTcs.Task, Task.Delay(timeoutMs, ct));
                pongReceived  = completed == _pongTcs.Task;
            }
            catch (TaskCanceledException) { break; }

            if (ct.IsCancellationRequested) break;

            if (pongReceived)
            {
                missCount = 0;
                status    = "OK";
                RecomputeLoss();
            }
            else
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
                status = $"Miss {missCount}/{maxConsecMiss}";
            }

            try { await Task.Delay(intervalMs, ct); }
            catch (TaskCanceledException) { break; }
        }
    }

    // -------------------------------------------------------
    // Pong 수신 (소켓 수신 스레드에서 호출)
    // -------------------------------------------------------
    public void OnPong(SC_Pong p)
    {
        var localRecvMs = NowMs();
        Interlocked.Exchange(ref lastPongAtMs, localRecvMs);
        receivedCount++;
        RecomputeLoss();

        // TCS 신호 -> LoopAsync busy-wait 제거의 핵심
        _pongTcs?.TrySetResult(true);

        var proc = Math.Max(0, p.serverSendMs - p.serverRecvMs);
        var rtt  = Math.Max(0, (localRecvMs - p.clientSendMs) - proc);

        lastRttMs = rtt;
        avgRttMs  = avgRttMs == 0 ? rtt : (long)(EMA_ALPHA * rtt + (1 - EMA_ALPHA) * avgRttMs);
        maxRttMs  = Math.Max(maxRttMs, rtt);
        minRttMs  = Math.Min(minRttMs, rtt);

        // 시간 동기화: serverNowAtRecv ≈ serverSend + RTT/2
        TimeSync.SetOffsetFromServerNow(p.serverSendMs + rtt / 2, rtt);

        if (!running)
        {
            running = true;
            status  = "Recovered";
            onRecovered?.Invoke();
        }
    }

    // -------------------------------------------------------
    void RecomputeLoss()
    {
        if (sentCount == 0) { packetLossPercent = 0; return; }
        packetLossPercent =
            Mathf.Clamp01((float)(sentCount - receivedCount) / sentCount) * 100f;
    }

    static long NowMs()
        => (long)(System.Diagnostics.Stopwatch.GetTimestamp()
                  * 1000.0 / System.Diagnostics.Stopwatch.Frequency);

    public void Configure(int? interval = null, int? timeout = null, int? maxMiss = null)
    {
        if (interval.HasValue) intervalMs    = Math.Max(100, interval.Value);
        if (timeout.HasValue)  timeoutMs     = Math.Max(500, timeout.Value);
        if (maxMiss.HasValue)  maxConsecMiss = Math.Max(1,   maxMiss.Value);
    }

    // -------------------------------------------------------
    // [RTT Fix] 확장 HUD - RTT min/max/avg, Offset Jitter, Loss%, SampleCount
    // -------------------------------------------------------
    void OnGUI()
    {
        if (!running) return;

        int  width = 360, height = 130;
        Rect rect  = new Rect(Screen.width - width - 10, 10, width, height);
        GUI.Box(rect, "");

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 17,
            alignment = TextAnchor.UpperLeft,
            richText  = true
        };

        bool   warming   = sentCount <= TimeSync.WarmupCount;
        float  loss      = packetLossPercent;
        string lossColor = loss < 1f ? "#00FF00" : loss < 5f ? "#FFFF00" : "#FF4444";
        long   minDisp   = minRttMs == long.MaxValue ? 0 : minRttMs;

        string text =
            "<color=#FFFFFF>[Network Sync]</color>\n" +
            $"<color=#00FF00>RTT:</color> avg={avgRttMs}ms  min={minDisp}  max={maxRttMs}\n" +
            $"<color=#FFFF00>Offset:</color> {TimeSync.OffsetMs:F1}ms  " +
            $"<color=#AAAAFF>Jitter:</color> {TimeSync.OffsetJitterMs:F2}ms\n" +
            $"<color=#00FFFF>Samples:</color> {TimeSync.SampleCount}  " +
            $"<color={lossColor}>Loss:</color> {loss:F1}%  " +
            $"<color=#CCCCCC>Status:</color> {(warming ? "Warming Up" : status)}";

        // 그림자
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 6, rect.y + 6, rect.width, rect.height), text, style);

        // 본문
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(rect.x + 5, rect.y + 5, rect.width, rect.height), text, style);
    }
}

[AttributeUsage(AttributeTargets.Field)]
public class ReadOnlyAttribute : PropertyAttribute { }

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
