using System;
using System.Collections.Generic;

namespace Server.Workers;

public interface IUpdatableSnapshotProvider
{
    string RoleName { get; }
    Func<IUpdatable[]> GetSnapshotGetter();
    int DefaultTickMs { get; }
}
