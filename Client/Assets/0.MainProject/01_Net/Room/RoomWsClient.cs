using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;


    public sealed class RoomWsClient : IAsyncDisposable
    {
        private readonly IWebSocketClient _ws;
        
        // New Events
        private readonly string _clientVersion;
        public event Action<WaitingRoomDto> OnInit;
        public event Action<string, string> OnMemberJoin; // uid, name
        public event Action<string> OnMemberLeave; // uid
        public event Action<string, bool> OnMemberUpdate; // uid, ready
        public event Action<EndpointDto, string> OnGameStart; // endpoint, ticket
        public event Action<string> OnErrorMsg;
        
        // Connection Events
        public event Action<string> OnClosed;
        public event Action<string> OnWarn;

        private Action<string> _onMessage;
        private Action<string> _onClosed;
        private Action<Exception> _onError;
        private bool _subscribed;

        public RoomWsClient(IWebSocketClient wsImpl, string clientVersion)
        {
            _ws = wsImpl;
            _clientVersion = clientVersion;

            _onMessage = HandleMessage;
            _onClosed = reason => OnClosed?.Invoke(reason);
            _onError = ex => OnWarn?.Invoke(ex.Message);
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _ws.OnMessage += _onMessage;
            _ws.OnClosed += _onClosed;
            _ws.OnError += _onError;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _ws.OnMessage -= _onMessage;
            _ws.OnClosed -= _onClosed;
            _ws.OnError -= _onError;
            _subscribed = false;
        }

        public async Task ConnectAsync(string wsUrl, CancellationToken ct = default)
        {
            var headers = new Dictionary<string, string> { ["X-Client-Version"] = _clientVersion };
            
            Subscribe();
            // Pass headers
            await _ws.ConnectAsync(wsUrl, headers, ct);
            Debug.Log($"ws connected: {wsUrl}");
        }

        public Task ToggleReadyAsync(bool v, CancellationToken ct = default)
        {
            var req = new ReadyRequest { value = v };
            return _ws.SendTextAsync(JsonUtility.ToJson(req), ct);
        }

        public Task StartGameAsync(CancellationToken ct = default)
        {
            var req = new StartRequest();
            return _ws.SendTextAsync(JsonUtility.ToJson(req), ct);
        }

        public Task LeaveAsync(CancellationToken ct = default)
        {
            // Just close connection or send specific message?
            // Server handles close as leave.
            return _ws.CloseAsync("leave", ct);
        }

        private void HandleMessage(string json)
        {
            try
            {
                // Parse base to get type
                var basePkt = JsonUtility.FromJson<BasePacket>(json);
                if (basePkt == null || string.IsNullOrEmpty(basePkt.type))
                {
                     // Fallback or ignore
                     return;
                }

                switch (basePkt.type)
                {
                    case "Init":
                        Debug.Log($"[RoomWsClient] Init Packet: {json}");
                        var initMsg = JsonUtility.FromJson<InitMsg>(json);
                        var count = initMsg.room?.memberUids?.Count ?? 0;
                        Debug.Log($"[RoomWsClient] Init Parsed Members: {count}");
                        OnInit?.Invoke(initMsg.room);
                        break;
                    case "MemberJoin":
                        var joinMsg = JsonUtility.FromJson<MemberJoinMsg>(json);
                        OnMemberJoin?.Invoke(joinMsg.uid, joinMsg.name);
                        break;
                    case "MemberLeave":
                        var leaveMsg = JsonUtility.FromJson<MemberLeaveMsg>(json);
                        OnMemberLeave?.Invoke(leaveMsg.uid);
                        break;
                    case "MemberUpdate":
                        var updateMsg = JsonUtility.FromJson<MemberUpdateMsg>(json);
                        OnMemberUpdate?.Invoke(updateMsg.uid, updateMsg.ready);
                        break;
                    case "GameStart":
                        var startMsg = JsonUtility.FromJson<GameStartMsg>(json);
                        OnGameStart?.Invoke(startMsg.endpoint, startMsg.ticket);
                        break;
                    case "Error":
                        var errMsg = JsonUtility.FromJson<ErrorMsg>(json);
                        OnErrorMsg?.Invoke(errMsg.message ?? json);
                        OnWarn?.Invoke($"Server Error: {errMsg.message}");
                        break;
                    default:
                        OnWarn?.Invoke($"Unknown type: {basePkt.type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                OnWarn?.Invoke($"Parse Error: {ex.Message}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            Unsubscribe();
            await _ws.CloseAsync("dispose");
        }
    }

