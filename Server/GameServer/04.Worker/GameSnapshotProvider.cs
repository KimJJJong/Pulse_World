using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Workers;

public sealed class GameSnapshotProvider : IUpdatableSnapshotProvider
{
    public string RoleName => "Game";
    public Func<IUpdatable[]> GetSnapshotGetter() => () =>
        GameManager.GetUpdatablesSnapshot()
            .Concat(P2PRelayManager.GetUpdatablesSnapshot())
            .ToArray();
    public int DefaultTickMs => 15;
}
