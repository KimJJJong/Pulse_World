using UnityEngine;

public static class TimeSync
{
    /// <summary>serverMs ≈ localMs + OffsetMs</summary>
    public static double OffsetMs { get; private set; }

    /// <summary>최근 RTT 추정치(ms)</summary>
    public static double EstimatedRttMs { get; private set; }

    // ---- 안정화 파라미터 ----
    [Header("Warmup (Initial Snap)")]
    /// <summary>
    /// 초기 샘플 N번은 "스냅"으로 즉시 맞춘다.
    /// 서버 업타임과 클라 업타임 차이가 큰 환경에서 필수.
    /// </summary>
    public static int WarmupCount = 8;

    /// <summary>Warmup 이후 오프셋 스무딩(0~1). 0.1~0.3 추천</summary>
    public static float Smoothing = 0.2f;

    [Header("Clamp (After Warmup)")]
    /// <summary>
    /// Warmup 이후에만 적용되는 최대 점프 제한(ms).
    /// 지터/이상치로 offset이 갑자기 튀는 걸 방지.
    /// </summary>
    public static float MaxJumpMs = 200f;

    [Header("Outlier Rejection (Optional)")]
    /// <summary>
    /// Warmup 이후, diff가 이 값보다 너무 크면(예: 수초 단위) 샘플을 무시.
    /// (패킷 파싱 꼬임/서버 시계 섞임/일시적인 대형 이상치 방어)
    /// </summary>
    public static float RejectIfDiffAbsMs = 5000f;

    // 디버그
    public static string LastSetBy { get; private set; } = "none";
    public static long LastServerNowArg { get; private set; } = 0;
    public static long LastLocalRecv { get; private set; } = 0;

    static int _samples = 0;

    public static void Reset()
    {
        OffsetMs = 0;
        EstimatedRttMs = 0;
        _samples = 0;
        LastSetBy = "reset";
        LastServerNowArg = 0;
        LastLocalRecv = 0;
    }

    /// <summary>클라 로컬 단조시간(ms) - Thread Safe</summary>
    public static long LocalNowMs()
    {
        return (long)(System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
    }

    /// <summary>현재 서버 시간 추정(ms)</summary>
    public static long ServerNowMs()
        => (long)(LocalNowMs() + OffsetMs);

    /// <summary>serverTargetMs까지 남은 ms(음수면 지남)</summary>
    public static long MillisUntil(long serverTargetMs)
        => serverTargetMs - ServerNowMs();

    // -----------------------------
    // Welcome 등 RTT 없는 초기 동기화(대충 1회용)
    // -----------------------------
    public static void SetOffsetFromServerNow(long serverNowMs)
    {
        long localRecvMs = LocalNowMs();
        Mark("Welcome/NoRTT", serverNowMs, localRecvMs);

        // [TimeSync_Warning] RTT 보정 없음 — 원격 환경에서 OffsetMs가 RTT/2만큼 음수로 치우침.
        // 반드시 Ping/Pong 루프가 돌기 시작한 후에 overloaded 메서드로 재보정 필요.
        Debug.LogWarning($"[TimeSync_NoRTT] samples={_samples} serverArg={serverNowMs} localRecv={localRecvMs} rawOffset={serverNowMs - localRecvMs}ms — RTT unknown, will drift by ~RTT/2 until Ping runs");

        double targetOffset = serverNowMs - localRecvMs;






        // RTT 없는 값은 너무 신뢰하지 말고,
        // Warmup 이전에만 스냅, 이후엔 약하게 섞어줌.
        if (_samples < WarmupCount)
        {
            OffsetMs = targetOffset; // 초기 스냅
        }
        else
        {
            // warmup 이후엔 부드럽게 섞기
            OffsetMs = Mathf.Lerp((float)OffsetMs, (float)targetOffset, 0.05f);
        }

        _samples++;
    }

    // -----------------------------
    // Ping/Pong 기반 정밀 동기화
    // serverNowAtRecvMs = serverSendMs + RTT/2 (권장)
    // -----------------------------
    public static void SetOffsetFromServerNow(long serverNowAtRecvMs, long rttMs)
    {
        long localRecvMs = LocalNowMs();
        Mark("Ping/Pong", serverNowAtRecvMs, localRecvMs);

        double targetOffset = serverNowAtRecvMs - localRecvMs;

        // 1) Warmup: 큰 offset 차이는 정상 -> 즉시 스냅
        if (_samples < WarmupCount)
        {
            OffsetMs = targetOffset;
            EstimatedRttMs = rttMs;
            _samples++;
            return;
        }

        // 2) outlier reject (선택): 말도 안 되는 샘플 방어
        double diffRaw = targetOffset - OffsetMs;
        if (RejectIfDiffAbsMs > 0 && Mathf.Abs((float)diffRaw) > RejectIfDiffAbsMs)
        {
            // 샘플 무시 (RTT만 갱신)
            EstimatedRttMs = rttMs;
            _samples++;
            return;
        }

        // [Fix] Wi-Fi 지터 대응 필터링
        // 양방향 무선망(Wi-Fi)에서는 핑이 순간적으로 100~200ms 이상 튀는 지터(Jitter)가 흔합니다.
        // 핑이 튀었다는 것은 패킷이 공유기 등에서 오래 대기했다는 뜻이므로, 
        // 이 불량 샘플로 서버-클라 시계 보정(Offset)을 크게 해버리면 리듬 게임 판정이 요동칩니다.
        float currentSmoothing = Smoothing;
        if (rttMs > EstimatedRttMs + 30)
        {
            // 지터가 30ms 이상 튀면 오프셋 반영 가중치를 1/10 로 대폭 줄여서 시간을 안정적으로 유지합니다.
            currentSmoothing *= 0.1f;
            //Debug.Log($"[TimeSync_SpikeFilter] Jitter detected! rtt={rttMs}ms (Est={EstimatedRttMs:F0}ms). Smoothing reduced to {currentSmoothing:F3}");
        }
        else if (rttMs < EstimatedRttMs)
        {
            // 핑이 평균보다 빠르다면 중간에 물리적 대기시간(큐잉 딜레이)이 적었던 가장 신뢰성 높은 샘플이므로극 보정합니다.
            currentSmoothing *= 1.5f;
        }

        // 3) clamp: 튐 방지
        if (Mathf.Abs((float)diffRaw) > MaxJumpMs)
            targetOffset = OffsetMs + Mathf.Sign((float)diffRaw) * MaxJumpMs;

        // 4) smoothing: 미세 흔들림 완화
        OffsetMs = Mathf.Lerp((float)OffsetMs, (float)targetOffset, currentSmoothing);

        EstimatedRttMs = Mathf.Lerp((float)EstimatedRttMs, rttMs, 0.2f); // RTT 추정치는 따로 부드럽게 갱신
        _samples++;

        // [TimeSync_Pong] 정상적인 RTT 보정 적용. 이 로그가 주기적으로 떠야 함.
        //Debug.Log($"[TimeSync_Pong] sample#{_samples} rtt={rttMs}ms oneWay={rttMs / 2}ms offset={OffsetMs:F1}ms serverNow={ServerNowMs()}");

    }

    static void Mark(string by, long serverNowArg, long localRecv)
    {
        LastSetBy = by;
        LastServerNowArg = serverNowArg;
        LastLocalRecv = localRecv;
    }
}
