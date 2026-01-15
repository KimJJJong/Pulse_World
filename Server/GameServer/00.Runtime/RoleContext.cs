using Microsoft.Extensions.Options;
using Server.Bootstrap;
using System;

namespace Server.Runtime;

public sealed class RoleContext
{
    public string RoleName { get; }
    public bool IsGame => RoleName.Equals("Game", StringComparison.OrdinalIgnoreCase);
    public bool IsTown => RoleName.Equals("Town", StringComparison.OrdinalIgnoreCase);

    public ServerOptions Options { get; }

    public RoleContext(IOptions<ServerOptions> opt)
    {
        Options = opt.Value;
        RoleName = Options.Role.Name;
    }
}
