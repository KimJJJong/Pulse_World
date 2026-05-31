#if !UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;


    public sealed class StdWebSocketClient : IWebSocketClient
    {
        ClientWebSocket _ws;
        CancellationTokenSource _loopCts;

        public event Action<string> OnMessage;
        public event Action<string> OnClosed;
        public event Action<Exception> OnError;

        public bool IsOpen => _ws != null && _ws.State == WebSocketState.Open;

        public async Task ConnectAsync(string url, IDictionary<string, string> headers = null, CancellationToken ct = default)
        {
            Debug.Log($"[StdWebSocketClient] Connecting to: {url}");
            _ws = new ClientWebSocket();
            if (headers != null)
                foreach (var kv in headers) _ws.Options.SetRequestHeader(kv.Key, kv.Value);

            try
            {
                await _ws.ConnectAsync(new Uri(url), ct);
                Debug.Log("[StdWebSocketClient] Connection Established.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StdWebSocketClient] ConnectAsync Failed: {ex.GetType().Name} - {ex.Message} / Inner: {ex.InnerException?.Message}");
                throw;
            }

            _loopCts = new CancellationTokenSource();
            _ = ReceiveLoop(_loopCts.Token);
        }

        public async Task SendTextAsync(string text, CancellationToken ct = default)
        {
            if (!IsOpen)
                throw new InvalidOperationException($"WebSocket is not open. State={_ws?.State.ToString() ?? "None"}");

            var buf = Encoding.UTF8.GetBytes(text);
            await _ws.SendAsync(new ArraySegment<byte>(buf), WebSocketMessageType.Text, true, ct);
        }

        public async Task CloseAsync(string reason = null, CancellationToken ct = default)
        {
            try { if (_ws?.State == WebSocketState.Open) await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, reason ?? "client_close", ct); }
            catch { /* ignore */ }
            finally { _loopCts?.Cancel(); }
        }

        async Task ReceiveLoop(CancellationToken ct)
        {
            var buf = new byte[8192];
            try
            {
                while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
                {
                    var sb = new StringBuilder();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        
                        sb.Append(Encoding.UTF8.GetString(buf, 0, result.Count));
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close) break;

                    var msg = sb.ToString();
                    MainThreadDispatcher.Post(() => OnMessage?.Invoke(msg));
                }
            }
            catch (Exception ex)
            {
                MainThreadDispatcher.Post(() => OnError?.Invoke(ex));
            }
            finally
            {
                MainThreadDispatcher.Post(() => OnClosed?.Invoke("closed"));
            }
        }

        public ValueTask DisposeAsync()
        {
            _loopCts?.Cancel();
            _ws?.Dispose();
            _loopCts?.Dispose();
            return default;
        }
    }

#endif
