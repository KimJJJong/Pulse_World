using System;
using System.Linq;

namespace Server.Workers;

public sealed class TownSnapshotProvider : IUpdatableSnapshotProvider
{
    public string RoleName => "Town";
    public Func<IUpdatable[]> GetSnapshotGetter() => () =>
        TownManager.GetUpdatablesSnapshot()
            .Concat(TownP2PRelayManager.GetUpdatablesSnapshot())
            .ToArray();
    public int DefaultTickMs => 100;
}
