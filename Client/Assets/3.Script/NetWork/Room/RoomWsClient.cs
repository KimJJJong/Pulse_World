using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Contracts.Packet;   //  생성기로 나온 공통 패킷 (JsonUtility + field DTO + WireJson)

namespace NetClient.Room
{
    public sealed class RoomWsClient : IAsyncDisposable
    {
        private readonly IWebSocketClient _ws;
        private readonly string _clientVersion;

        // 구독 핸들러 캐시
        private Action<string> _onMessage;
        private Action<string> _onClosed;
        private Action<Exception> _onError;
        private bool _subscribed;

        public event Action<WelcomeMsg> OnWelcome;
        public event Action<IReadOnlyList<MemberDto>> OnWelcomeMembers;
        public event Action<string, bool> OnMemberReady;
        public event Action<int, long> OnCountdownStart;
        public event Action OnCountdownCancel;
        public event Action<GameBeginMsg> OnGameBegin;
        public event Action<string> OnClosed;
        public event Action<string> OnWarn;
        public event Action<MemberDto> OnMemberJoin;
        public event Action<string> OnMemberLeave;
        public event Action<int, string, long> OnRoomUpdate;

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

        public async Task ConnectAsync(string wsUrl, string token, CancellationToken ct = default)
        {
            var headers = new Dictionary<string, string> { ["X-Client-Version"] = _clientVersion };

            Subscribe();

            /*_ws.OnMessage += HandleMessage;
            _ws.OnClosed += reason => OnClosed?.Invoke(reason);
            _ws.OnError += ex => OnWarn?.Invoke(ex.Message);
*/
            await _ws.ConnectAsync(wsUrl, headers, ct);

            // hello (Typed)
            var hello = new HelloMsg { token = token, version = _clientVersion }; // op="hello"는 생성물 기본값
            await _ws.SendTextAsync(WireJson.Serialize(hello), ct);

            Debug.Log($"ws open: {wsUrl}");
            Debug.Log($"ws token: {token}");
        }

        public Task ToggleReadyAsync(bool v, CancellationToken ct = default)
            => _ws.SendTextAsync(WireJson.Serialize(new ReadyMsg { value = v }), ct);

        public Task LeaveAsync(CancellationToken ct = default)
            => _ws.SendTextAsync(WireJson.Serialize(new LeaveMsg()), ct);

        // WebSocket Input
        private void HandleMessage(string json)
        {
            try
            {
                var opOnly = WireJson.Deserialize<OpOnly>(json);
                var op = opOnly?.op;
                switch (op)
                {
                    case "welcome":
                        {
                            var msg = WireJson.Deserialize<WelcomeMsg>(json);
                            Debug.Log($"SET : {msg}");
/*                            Debug.Log($"Context_Member01 : {msg.state.members[0].userId}");
                            Debug.Log($"Context_Member02 : {msg.state.members[1].userId}");
                            Debug.Log($"Context_roomCur : {msg.state.room.cur}");*/
                            OnWelcome?.Invoke(msg);
                            OnWelcomeMembers?.Invoke(msg.state.members);
                            break;
                        }
                    case "member.update":
                        {
                            var msg = WireJson.Deserialize<MemberUpdateMsg>(json);
                            OnMemberReady?.Invoke(msg.id, msg.ready);
                            break;
                        }
                    case "member.join":
                        {
                            var msg = WireJson.Deserialize<MemberJoinMsg>(json);
                            OnMemberJoin?.Invoke(msg.member);
                            break;
                        }
                    case "member.leave":
                        {
                            var msg = WireJson.Deserialize<MemberLeaveMsg>(json);
                            OnMemberLeave?.Invoke(msg.id);
                            break;
                        }
                    case "countdown.start":
                        {
                            var msg = WireJson.Deserialize<CountdownStartMsg>(json);
                            OnCountdownStart?.Invoke(msg.seconds, msg.startAtMs);
                            break;
                        }
                    case "countdown.cancel":
                        {
                            OnCountdownCancel?.Invoke();
                            break;
                        }
                    case "room.update":
                        {
                            var msg = WireJson.Deserialize<RoomUpdateMsg>(json);
                            var p = msg.patch; // RoomPatch
                            OnRoomUpdate?.Invoke(p.cur, p.status, p.updatedAtMs);
                            break;
                        }
                    case "game.begin":
                        {
                            var msg = WireJson.Deserialize<GameBeginMsg>(json);
                            OnGameBegin?.Invoke(msg);
                            break;
                        }
                    case "warn":
                        {
                            var w = WireJson.Deserialize<WarnMsg>(json);
                            if (w != null) OnWarn?.Invoke($"warn: {w.code} target={w.target} perSec={w.perSec}");
                            else OnWarn?.Invoke(json);
                            break;
                        }
                    default:
                        OnWarn?.Invoke($"unknown op: {op}\n{json}");
                        break;
                }
            }
            catch (Exception e)
            {
                OnWarn?.Invoke($"parse_error: {e.Message}\n{json}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            Unsubscribe();
            await _ws.CloseAsync("dispose");
        } 
    }
}
