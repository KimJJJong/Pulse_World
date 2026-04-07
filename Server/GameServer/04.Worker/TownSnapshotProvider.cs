using System;
using System.Collections.Generic;

namespace Server.Workers;

public sealed class TownSnapshotProvider : IUpdatableSnapshotProvider
{
    public string RoleName => "Town";
    public Func<IUpdatable[]> GetSnapshotGetter() => TownManager.GetUpdatablesSnapshot;
    public int DefaultTickMs => 100;
}
