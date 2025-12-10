using System;
using System.Threading;

/// <summary>
/// 서버-클라 시간 동기 유틸 (스레드 세이프)
/// - 서버 절대시각 수신 시: SetOffsetFromServerNow(serverTimeMs)
/// - Ping/Pong 왕복 시:      UpdateFromPingPong(tClientSendMs, tServerMs, tClientRecvMs)
/// </summary>
public static class TimeSync
{
	// serverNowMs = clientNowMs + _offsetMs
	static long _offsetMs;               // 서버-클라 시간 오프셋
	static long _rttMs;                  // 추정 RTT
	static int _samples;                // 샘플 수 (진행상황 파악용)

	/// <summary> 서버에서 보낸 "현재 시각(serverTimeMs)"를 그대로 신뢰하여 오프셋을 맞춘다. </summary>
	public static void SetOffsetFromServerNow(long serverTimeMs)
	{
		long clientNow = ClientNowMs();
		Interlocked.Exchange(ref _offsetMs, serverTimeMs - clientNow);
		// RTT는 알 수 없으므로 유지
	}

	/// <summary>
	/// Ping/Pong을 이용해 보다 정확한 오프셋 추정 (NTP 기본식)
	/// tC0=클라전송, tS=서버수신/응답 내장시각, tC1=클라수신
	/// offset ≈ tS - (tC0 + tC1)/2, rtt ≈ tC1 - tC0
	/// </summary>
	public static void UpdateFromPingPong(long tClientSendMs, long tServerMs, long tClientRecvMs)
	{
		long rtt = Math.Max(0, tClientRecvMs - tClientSendMs);
		long mid = (tClientSendMs + tClientRecvMs) / 2;
		long estimatedOffset = tServerMs - mid;

		// 간단 가중치(EMA 비슷하게): 최근치에 0.25 가중
		long prevOff = Interlocked.Read(ref _offsetMs);
		long newOff = prevOff + (long)((estimatedOffset - prevOff) * 0.25);
		Interlocked.Exchange(ref _offsetMs, newOff);

		long prevRtt = Interlocked.Read(ref _rttMs);
		long newRtt = prevRtt + (long)((rtt - prevRtt) * 0.25);
		Interlocked.Exchange(ref _rttMs, newRtt);

		Interlocked.Increment(ref _samples);
	}

	/// <summary> 서버 기준 "지금" (ms) </summary>
	public static long ServerNowMs()
		=> ClientNowMs() + Interlocked.Read(ref _offsetMs);

	/// <summary> 서버 절대시각까지 남은 ms(음수면 0) </summary>
	public static int MillisUntil(long serverAbsoluteMs)
	{
		long remain = serverAbsoluteMs - ServerNowMs();
		return (int)Math.Max(0, remain);
	}

	/// <summary> 추정 RTT(ms). 없으면 0 </summary>
	public static long EstimatedRttMs() => Interlocked.Read(ref _rttMs);

	/// <summary> 몇 번 보정했는지 </summary>
	public static int SampleCount() => Interlocked.CompareExchange(ref _samples, 0, 0);

	/// <summary> 오프셋/RTT 초기화 </summary>
	public static void Reset()
	{
		Interlocked.Exchange(ref _offsetMs, 0);
		Interlocked.Exchange(ref _rttMs, 0);
		Interlocked.Exchange(ref _samples, 0);
	}

	/// <summary> 클라 현재 시각(ms) </summary>
	public static long ClientNowMs()
		=> DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
