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
            var now = NowMs();               // ✅ monotonic local time
            lastSentAtMs = now;
            sentCount++;

            NetWorkManager.Instance.Send(new CS_Ping { seq = s, clientSendMs = now }.Write());

            // timeout 대기
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
        var localRecvMs = NowMs(); // ✅ 수신 순간 local time (monotonic)
        Interlocked.Exchange(ref lastPongAtMs, localRecvMs);
        receivedCount++;
        RecomputeLoss();

        // 서버 처리시간(선택): serverSend - serverRecv
        var proc = Math.Max(0, p.serverSendMs - p.serverRecvMs);

        // RTT 추정: (recv - clientSend) - proc
        var rtt = Math.Max(0, (localRecvMs - p.clientSendMs) - proc);

        lastRttMs = rtt;
        if (avgRttMs == 0) avgRttMs = rtt;
        else avgRttMs = (long)(EMA_ALPHA * rtt + (1 - EMA_ALPHA) * avgRttMs);

        maxRttMs = Math.Max(maxRttMs, rtt);
        minRttMs = Math.Min(minRttMs, rtt);

        // ✅ 시간 동기화(오프셋) 갱신: serverNowAtRecv ≈ serverSend + RTT/2
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

    // ✅ 반드시 monotonic(단조시간)로 통일!
    static long NowMs() => (long)(Time.realtimeSinceStartupAsDouble * 1000.0);

    public void Configure(int? interval = null, int? timeout = null, int? maxMiss = null)
    {
        if (interval.HasValue) intervalMs = Math.Max(100, interval.Value);
        if (timeout.HasValue) timeoutMs = Math.Max(500, timeout.Value);
        if (maxMiss.HasValue) maxConsecMiss = Math.Max(1, maxMiss.Value);
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
