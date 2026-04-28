using System;
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

    void OnGUI()
    {
        if (!running) return;

        var relayBridge = P2PRelayClientBridge.HasInstance ? P2PRelayClientBridge.Instance : null;
        bool showHostPing = relayBridge != null && relayBridge.IsRelayMode;

        // 화면 우측 상단 배치
        int width = 340;
        int height = showHostPing ? 220 : 180;
        Rect rect = new Rect(Screen.width - width - 10, 10, width, height);

        // 반투명 배경 박스
        GUI.Box(rect, "");

        // 글꼴 색상 및 정렬 설정
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.alignment = TextAnchor.UpperLeft;
        
        // 텍스트 내용 조립
        string hostPingLine = "";
        if (showHostPing)
        {
            hostPingLine = relayBridge.IsHostLocal
                ? "<color=#ffa500>Host RTT:</color> Local Host\n"
                : $"<color=#ffa500>Host RTT:</color> {relayBridge.HostAvgRttMs}ms (Last:{relayBridge.HostLastRttMs} Max:{relayBridge.HostMaxRttMs})\n" +
                  $"<color=#ffd27f>Host Status:</color> {relayBridge.HostPingStatus}\n";
        }

        string text = $"[Network Sync]\n" +
                      $"<color=#00FF00>RTT(Avg):</color> {avgRttMs}ms (Max:{maxRttMs})\n" +
                      hostPingLine +
                      $"<color=#00e5ee>RawRTT:</color> {lastRawRTT}ms | <color=#ff69b4>S.Proc:</color> {lastServerProc}ms\n" +
                      $"<color=#FFFF00>Offset:</color> {TimeSync.OffsetMs:F1}ms\n" +
                      $"<color=#aaaaaa>L.Send:</color> {lastLocalSend}\n" +
                      $"<color=#aaaaaa>L.Recv:</color> {lastLocalRecv}\n" +
                      $"<color=#00FFFF>Status:</color> {(sentCount <= 10 ? "Warming Up" : status)}";

        // 약간의 그림자 효과를 위해 검은색 텍스트 먼저 찍기
        style.normal.textColor = Color.black;
        Rect shadowRect = new Rect(rect.x + 6, rect.y + 6, rect.width, rect.height);
        GUI.Label(shadowRect, text, style);

        // 그 위에 하얀색 텍스트 (RichText 허용)
        style.richText = true;
        style.normal.textColor = Color.white;
        Rect textRect = new Rect(rect.x + 5, rect.y + 5, rect.width, rect.height);
        GUI.Label(textRect, text, style);
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
