using Microsoft.Extensions.Options;
using static Lobby.Api.Config.AppOptions;

namespace Lobby.Domain.Shared
{
    public interface IGameServerResolver
    {
        Task<(string gsId, string host, int port, int tickRate)> PickAsync(CancellationToken ct);
    }
    public sealed class StaticGsResolver : IGameServerResolver
    {
        private readonly GameServerOptions _opt;
        public StaticGsResolver(IOptions<GameServerOptions> o) => _opt = o.Value;
        public Task<(string gsId, string host, int port, int tickRate)> PickAsync(CancellationToken ct)
            => Task.FromResult((_opt.Static.Id, _opt.Static.PublicHost, _opt.Static.Port, _opt.Static.TickRate));
    }
}
