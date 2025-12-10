namespace GameServer.InGame.Manager.Beat;

using global::System;
using global::System.Collections.Generic;

/// <summary>
/// Beat 단위로 액션을 예약/관리하는 스케줄러.
/// - key: BeatIndex
/// - value: 해당 Beat에 실행될 PlayerActionCmd 리스트
/// </summary>
public sealed class BeatScheduler
{
    private readonly Dictionary<long, List<PlayerActionCmd>> _pendingByBeat = new();

    /// <summary>
    /// 주어진 Beat에 액션을 추가 (이미 RequestedBeat가 설정된 상태라고 가정).
    /// </summary>
    public void Enqueue(PlayerActionCmd cmd)
    {
        if (!_pendingByBeat.TryGetValue(cmd.RequestedBeat, out var list))
        {
            list = new List<PlayerActionCmd>();
            _pendingByBeat[cmd.RequestedBeat] = list;
        }

        list.Add(cmd);
    }

    /// <summary>
    /// 특정 Beat에 예약된 액션들을 모두 가져오고, 내부에서는 제거.
    /// - Beat 처리 시점에 한 번 호출해서 사용.
    /// </summary>
    public List<PlayerActionCmd> PopActions(long beatIndex)
    {
        if (!_pendingByBeat.TryGetValue(beatIndex, out var list))
            return new List<PlayerActionCmd>();

        _pendingByBeat.Remove(beatIndex);
        return list;
    }

    /// <summary>
    /// 특정 Beat에 예약된 액션들을 제거하지 않고 조회만 할 때 사용.
    /// (디버깅/관찰용, 실제 게임 로직에서는 보통 PopActions만 쓰면 된다.)
    /// </summary>
    public IReadOnlyList<PlayerActionCmd> PeekActions(long beatIndex)
    {
        if (_pendingByBeat.TryGetValue(beatIndex, out var list))
            return list;
        return Array.Empty<PlayerActionCmd>();
    }

    /// <summary>
    /// 모든 예약 액션 초기화 (세션 종료, 리셋 등).
    /// </summary>
    public void Clear()
    {
        _pendingByBeat.Clear();
    }
}
