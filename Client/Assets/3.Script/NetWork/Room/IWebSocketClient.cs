
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IWebSocketClient : IAsyncDisposable
{
    event Action<string> OnMessage;
    event Action<string> OnClosed;
    event Action<Exception> OnError;

    Task ConnectAsync(string url, IDictionary<string, string> headers = null, CancellationToken ct = default);
    Task SendTextAsync(string text, CancellationToken ct = default);
    Task CloseAsync(string reason = null, CancellationToken ct = default);
}
