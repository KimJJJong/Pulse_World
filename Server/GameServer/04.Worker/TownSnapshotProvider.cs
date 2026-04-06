using System;
using System.Collections.Generic;

namespace Server.Workers;

public sealed class TownSnapshotProvider : IUpdatableSnapshotProvider
{
    public string RoleName => "Town";
    public Func<IUpdatable[]> GetSnapshotGetter() => TownManager.GetUpdatablesSnapshot;
    // [RTT Fix] 100ms(10Hz) -> 33ms(30Hz): 클라 입력 패킷의 최대 큐 대기시간을 100ms->33ms로 감소
    // 평균 대기: 50ms->16ms, RTT 실질 개선 ~34ms 기대
    public int DefaultTickMs => 33;
}
