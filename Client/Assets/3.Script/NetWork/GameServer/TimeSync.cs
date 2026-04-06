using UnityEngine;

public static class TimeSync
{
    /// <summary>serverMs ≈ localMs + OffsetMs</summary>
    public static double OffsetMs { get; private set; }

    /// <summary>최근 RTT 추정치(ms)</summary>
    public static double EstimatedRttMs { get; private set; }

    /// <summary>초기 샘플 N번은 "스냅"으로 즉시 맞춘다.</summary>
    public static int WarmupCount = 8;

    /// <summary>Warmup 이후 최대 점프 제한(ms). 지터/이상치 방지.</summary>
    public static float MaxJumpMs = 200f;

    /// <summary>이 값보다 큰 diff는 이상치로 무시.</summary>
    public static float RejectIfDiffAbsMs = 5000f;

    // 디버그 / 모니터링
    public static string LastSetBy { get; private set; } = "none";
    public static long   LastServerNowArg { get; private set; } = 0;
    public static long   LastLocalRecv    { get; private set; } = 0;
    public static int    SampleCount      => _samples;

    /// <summary>
    /// [RTT Fix] offset 변화량 EMA (jitter 지표).
    /// PingManager.OnGUI 등에서 OffsetJitterMs 를 표시하면
    /// 동기화 품질을 실시간으로 확인할 수 있습니다.
    /// </summary>
    public static double OffsetJitterMs { get; private set; } = 0;

    static int    _samples    = 0;
    static double _prevOffset = 0;

    public static void Reset()
    {
        OffsetMs        = 0;
        EstimatedRttMs  = 0;
        OffsetJitterMs  = 0;
        _prevOffset     = 0;
        _samples        = 0;
        LastSetBy       = "reset";
        LastServerNowArg = 0;
        LastLocalRecv    = 0;
    }

    /// <summary>클라 로컬 단조시간(ms) - Thread Safe</summary>
    public static long LocalNowMs()
        => (long)(System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency);

    /// <summary>현재 서버 시간 추정(ms)</summary>
    public static long ServerNowMs()
        => (long)(LocalNowMs() + OffsetMs);

    /// <summary>serverTargetMs까지 남은 ms (음수면 지남)</summary>
    public static long MillisUntil(long serverTargetMs)
        => serverTargetMs - ServerNowMs();

    // -------------------------------------------------------
    // RTT 없는 초기 동기화 (Handshake 직후 1회용)
    // -------------------------------------------------------
    public static void SetOffsetFromServerNow(long serverNowMs)
    {
        long localRecvMs = LocalNowMs();
        Mark("Welcome/NoRTT", serverNowMs, localRecvMs);

        double targetOffset = serverNowMs - localRecvMs;

        if (_samples < WarmupCount)
            OffsetMs = targetOffset;                              // 초기 스냅
        else
            OffsetMs = DoubleLerp(OffsetMs, targetOffset, 0.05f);// 약하게만 반영

        _samples++;
    }

    // -------------------------------------------------------
    // Ping/Pong 기반 정밀 동기화
    // serverNowAtRecvMs = serverSendMs + RTT/2
    // -------------------------------------------------------
    public static void SetOffsetFromServerNow(long serverNowAtRecvMs, long rttMs)
    {
        long localRecvMs = LocalNowMs();
        Mark("Ping/Pong", serverNowAtRecvMs, localRecvMs);

        double targetOffset = serverNowAtRecvMs - localRecvMs;

        // 1) Warmup: 즉시 스냅
        if (_samples < WarmupCount)
        {
            _prevOffset     = targetOffset;
            OffsetMs        = targetOffset;
            EstimatedRttMs  = rttMs;
            _samples++;
            return;
        }

        // 2) outlier reject: 비정상적으로 큰 diff 무시
        double diffRaw = targetOffset - OffsetMs;
        if (RejectIfDiffAbsMs > 0 && Mathf.Abs((float)diffRaw) > RejectIfDiffAbsMs)
        {
            EstimatedRttMs = rttMs;
            _samples++;
            return;
        }

        // 3) clamp: 갑작스러운 튐 방지
        if (Mathf.Abs((float)diffRaw) > MaxJumpMs)
            targetOffset = OffsetMs + Mathf.Sign((float)diffRaw) * MaxJumpMs;

        // 4) [RTT Fix] 동적 smoothing - 샘플 수에 따라 수렴 속도 자동 조절
        //    핑 간격 500ms 기준:
        //      8~20 샘플 (4~10초)  : alpha=0.5 -> 빠른 수렴
        //     20~50 샘플 (10~25초) : alpha=0.3 -> 점진적 안정
        //       50+ 샘플 (25초+)   : alpha=0.1 -> 노이즈 억제
        float   alpha      = GetDynamicSmoothing(_samples);
        double  prevOffset = OffsetMs;
        OffsetMs = DoubleLerp(OffsetMs, targetOffset, alpha);

        // 5) jitter 모니터링 (offset 변화량 EMA)
        double delta = System.Math.Abs(OffsetMs - prevOffset);
        OffsetJitterMs = DoubleLerp(OffsetJitterMs, delta, 0.2f);

        EstimatedRttMs = rttMs;
        _samples++;
    }

    // -------------------------------------------------------
    // 헬퍼
    // -------------------------------------------------------

    /// <summary>[RTT Fix] 샘플 수 기반 동적 smoothing alpha</summary>
    static float GetDynamicSmoothing(int samples)
    {
        if (samples < 20) return 0.5f;
        if (samples < 50) return 0.3f;
        return 0.1f;
    }

    static double DoubleLerp(double a, double b, float t)
        => a + (b - a) * Mathf.Clamp01(t);

    static void Mark(string by, long serverNowArg, long localRecv)
    {
        LastSetBy        = by;
        LastServerNowArg = serverNowArg;
        LastLocalRecv    = localRecv;
    }
}
