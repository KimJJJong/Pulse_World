using System.Collections.Concurrent;
using System.Threading.Channels;
using ControlPlane.Grpc.V1;

namespace ControlPlaneServer.Domain.Presence;

public sealed class ControlEventHub
{
    // serverId -> subscribers
    private readonly ConcurrentDictionary<string, ConcurrentBag<Channel<ControlEvent>>> _subs = new();

    public ChannelReader<ControlEvent> Subscribe(string serverId)
    {
        var ch = Channel.CreateUnbounded<ControlEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var bag = _subs.GetOrAdd(serverId, _ => new ConcurrentBag<Channel<ControlEvent>>());
        bag.Add(ch);

        return ch.Reader;
    }

    public void PublishToServer(string serverId, ControlEvent ev)
    {
        if (!_subs.TryGetValue(serverId, out var bag))
            return;

        foreach (var ch in bag)
        {
            // 느린 구독자는 drop될 수 있음(서버 안정성 우선)
            ch.Writer.TryWrite(ev);
        }
    }
}
