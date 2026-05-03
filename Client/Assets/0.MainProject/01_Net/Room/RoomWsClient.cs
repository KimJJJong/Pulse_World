using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetClient.Room.UI;
using UnityEngine;


    public sealed class RoomWsClient : IAsyncDisposable
    {
        private readonly IWebSocketClient _ws;
        
        // New Events
        private readonly string _clientVersion;
        private string _pendingProbeNonce = "";
        private long _pendingProbeSentAtMs;
        private CancellationTokenSource _hostSelectionReportCts;
        public int LastHostProbeRttMs { get; private set; } = -1;
        public string LastHostProbeStatus { get; private set; } = "Idle";
        public event Action<WaitingRoomDto> OnInit;
        public event Action<string, string> OnMemberJoin; // uid, name
        public event Action<string> OnMemberLeave; // uid
        public event Action<string, bool> OnMemberUpdate; // uid, ready
        public event Action<int> OnHostProbeMeasured;
        public event Action<string, int> OnHostCandidateUpdate; // preferredHostUid, hostEpoch
        public event Action<HostCandidateUpdateMsg> OnHostSelectionUpdated;
        public event Action<string> OnSteamLobbyBound; // steamLobbyId
        public event Action<EndpointDto, string, string, int, bool, WsMatchManifestDto> OnGameStart; // endpoint, ticket, mapId, maxPlayers, useP2PRelay, matchManifest
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
            _onClosed = reason =>
            {
                StopHostSelectionReportLoop();
                OnClosed?.Invoke(reason);
            };
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

            LastHostProbeRttMs = -1;
            LastHostProbeStatus = "Connecting";
            StopHostSelectionReportLoop();
            Subscribe();
            // Pass headers
            await _ws.ConnectAsync(wsUrl, headers, ct);
            Debug.Log($"ws connected: {wsUrl}");
            await SendHostProbePingAsync(ct);
            StartHostSelectionReportLoop();
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

        public Task BindSteamLobbyAsync(string steamLobbyId, CancellationToken ct = default)
        {
            var req = new BindSteamLobbyRequest { steamLobbyId = steamLobbyId ?? "" };
            _ = SendHostSelectionReportAsync(ct);
            return _ws.SendTextAsync(JsonUtility.ToJson(req), ct);
        }

        public Task LeaveAsync(CancellationToken ct = default)
        {
            // Just close connection or send specific message?
            // Server handles close as leave.
            return _ws.CloseAsync("leave", ct);
        }

        private Task SendHostProbePingAsync(CancellationToken ct = default)
        {
            _pendingProbeNonce = Guid.NewGuid().ToString("N");
            _pendingProbeSentAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            LastHostProbeStatus = "Waiting Pong";
            var req = new HostProbePingRequest { nonce = _pendingProbeNonce };
            return _ws.SendTextAsync(JsonUtility.ToJson(req), ct);
        }

        private Task SendHostProbeReportAsync(int rttMs, CancellationToken ct = default)
        {
            LastHostProbeRttMs = Mathf.Max(0, rttMs);
            LastHostProbeStatus = $"Reported {LastHostProbeRttMs} ms";
            var req = new HostProbeReportRequest
            {
                rttMs = LastHostProbeRttMs,
                reportedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            return _ws.SendTextAsync(JsonUtility.ToJson(req), ct);
        }

        private Task SendHostSelectionReportAsync(CancellationToken ct = default)
        {
            var room = RoomUiController.ActiveInstance;
            var steam = AppBootstrap.Instance != null && AppBootstrap.Instance.Root != null
                ? AppBootstrap.Instance.Root.SteamPlatform
                : null;
            var ping = PingManager.Instance;

            bool steamEnabled = steam != null && steam.Enabled;
            bool steamInitialized = steam != null && steam.IsInitialized;
            bool steamLobbyJoined = steam != null && steam.HasJoinedLobby;
            string steamId64 = steam != null ? steam.SteamId64 ?? "" : "";
            bool steamReady = steamEnabled && steamInitialized && steamLobbyJoined && !string.IsNullOrWhiteSpace(steamId64);

            var req = new HostSelectionReportRequest
            {
                steamId64 = steamId64,
                steamEnabled = steamEnabled,
                steamInitialized = steamInitialized,
                steamLobbyJoined = steamLobbyJoined,
                steamReady = steamReady,
                currentServerRttMs = ping != null ? (int)ping.AvgRttMs : -1,
                currentServerLossPct = ping != null ? ping.PacketLossPercent : 0f,
                currentServerJitterMs = ping != null ? (int)ping.AvgJitterMs : -1,
                avgFrameMs = room != null ? room.HostSelectionAvgFrameMs : Mathf.Max(1f, Time.smoothDeltaTime * 1000f),
                p95FrameMs = room != null ? room.HostSelectionP95FrameMs : Mathf.Max(1f, Time.smoothDeltaTime * 1000f),
                sendQueueDepth = 0,
                reportedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            return _ws.SendTextAsync(JsonUtility.ToJson(req), ct);
        }

        private void StartHostSelectionReportLoop()
        {
            if (_hostSelectionReportCts != null)
                return;

            _hostSelectionReportCts = new CancellationTokenSource();
            _ = HostSelectionReportLoopAsync(_hostSelectionReportCts.Token);
        }

        private void StopHostSelectionReportLoop()
        {
            if (_hostSelectionReportCts == null)
                return;

            _hostSelectionReportCts.Cancel();
            _hostSelectionReportCts.Dispose();
            _hostSelectionReportCts = null;
        }

        private async Task HostSelectionReportLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await SendHostSelectionReportAsync(ct);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnWarn?.Invoke($"HostSelectionReport failed: {ex.Message}");
                }

                try
                {
                    await Task.Delay(3000, ct);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
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
                        Debug.Log($"[RoomWsClient] MemberJoin Packet: {json}");
                        var joinMsg = JsonUtility.FromJson<MemberJoinMsg>(json);
                        Debug.Log($"[RoomWsClient] MemberJoin Parsed: uid={joinMsg.uid}, name={joinMsg.name}");
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
                    case "HostProbePong":
                        var probePong = JsonUtility.FromJson<HostProbePongMsg>(json);
                        if (!string.IsNullOrEmpty(probePong.nonce) && string.Equals(probePong.nonce, _pendingProbeNonce, StringComparison.Ordinal))
                        {
                            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            var deltaMs = nowMs - _pendingProbeSentAtMs;
                            var rttMs = (int)Math.Max(0L, Math.Min(deltaMs, int.MaxValue));
                            LastHostProbeRttMs = rttMs;
                            LastHostProbeStatus = $"Measured {rttMs} ms";
                            OnHostProbeMeasured?.Invoke(rttMs);
                            _ = SendHostProbeReportAsync(rttMs);
                        }
                        break;
                    case "HostCandidateUpdate":
                        var hostCandidate = JsonUtility.FromJson<HostCandidateUpdateMsg>(json);
                        OnHostCandidateUpdate?.Invoke(
                            hostCandidate.preferredHostUid,
                            hostCandidate.hostSelectionEpoch > 0 ? hostCandidate.hostSelectionEpoch : hostCandidate.hostEpoch);
                        OnHostSelectionUpdated?.Invoke(hostCandidate);
                        break;
                    case "SteamLobbyBound":
                        var lobbyBound = JsonUtility.FromJson<SteamLobbyBoundMsg>(json);
                        OnSteamLobbyBound?.Invoke(lobbyBound.steamLobbyId ?? "");
                        break;
                    case "GameStart":
                        var startMsg = JsonUtility.FromJson<GameStartMsg>(json);
                        OnGameStart?.Invoke(startMsg.endpoint, startMsg.ticket, startMsg.mapId, startMsg.maxPlayers, startMsg.useP2PRelay, startMsg.matchManifest);
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
            StopHostSelectionReportLoop();
            Unsubscribe();
            await _ws.CloseAsync("dispose");
        }
    }

