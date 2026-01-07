using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Runtime;

public interface IRoleModuleResolver
{
    IRoleModule Resolve();
}

public sealed class RoleModuleResolver : IRoleModuleResolver
{
    private readonly IEnumerable<IRoleModule> _modules;
    private readonly RoleContext _ctx;

    public RoleModuleResolver(IEnumerable<IRoleModule> modules, RoleContext ctx)
    {
        _modules = modules;
        _ctx = ctx;
    }

    public IRoleModule Resolve()
    {
        var m = _modules.FirstOrDefault(x => x.Name.Equals(_ctx.RoleName, StringComparison.OrdinalIgnoreCase));
        if (m == null)
            throw new InvalidOperationException($"Unknown role '{_ctx.RoleName}'. Use Game or Town.");
        return m;
    }
}
