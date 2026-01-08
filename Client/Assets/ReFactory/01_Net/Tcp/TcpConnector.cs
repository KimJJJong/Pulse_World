using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class TcpConnector : MonoBehaviour
{
    public static TcpConnector Instance { get; private set; } = null!;

    TcpClient? _client;
    NetworkStream? _stream;

    public bool IsConnected => _client?.Connected == true;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public async Task<(bool ok, string msg)> ConnectAsync(string host, int port, int timeoutMs = 5000)
    {
        try
        {
            Close();

            _client = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);

            var connectTask = _client.ConnectAsync(host, port);
            var done = await Task.WhenAny(connectTask, Task.Delay(timeoutMs, cts.Token));
            if (done != connectTask || !_client.Connected)
            {
                Close();
                return (false, "TCP 연결 시간 초과");
            }

            _stream = _client.GetStream();
            return (true, $"TCP 연결 성공: {host}:{port}");
        }
        catch (Exception ex)
        {
            Close();
            return (false, $"TCP 연결 실패: {ex.Message}");
        }
    }

    public void Close()
    {
        try { _stream?.Close(); } catch { }
        try { _client?.Close(); } catch { }
        _stream = null;
        _client = null;
    }

    // TODO(확장): Connect 직후 티켓 기반 핸드셰이크 보내기
    // public Task SendHandshakeAsync(string ticketId, string key) { ... }
}
