using System;
using System.Collections.Generic;

namespace Server.Workers;

public sealed class GameSnapshotProvider : IUpdatableSnapshotProvider
{
    public string RoleName => "Game";
    public Func<IUpdatable[]> GetSnapshotGetter() => GameManager.GetUpdatablesSnapshot;
    public int DefaultTickMs => 15;
}
