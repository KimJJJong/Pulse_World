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

    /// <summary>클라 로컬 단조시간(ms)</summary>
    public static long LocalNowMs()
        => (long)(Time.realtimeSinceStartupAsDouble * 1000.0);

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

        // 3) clamp: 튐 방지
        if (Mathf.Abs((float)diffRaw) > MaxJumpMs)
            targetOffset = OffsetMs + Mathf.Sign((float)diffRaw) * MaxJumpMs;

        // 4) smoothing: 미세 흔들림 완화
        OffsetMs = Mathf.Lerp((float)OffsetMs, (float)targetOffset, Smoothing);

        EstimatedRttMs = rttMs;
        _samples++;
    }

    static void Mark(string by, long serverNowArg, long localRecv)
    {
        LastSetBy = by;
        LastServerNowArg = serverNowArg;
        LastLocalRecv = localRecv;
    }
}
