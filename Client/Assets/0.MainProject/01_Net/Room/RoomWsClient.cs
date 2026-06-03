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
        private readonly IRoomSteamPairProbeService _pairProbe = RoomSteamPairProbeServiceFactory.Create();
        private readonly List<MemberTransportState> _latestMemberTransport = new();
        private string _currentRoomId = "";
        private bool _currentRoomUseP2PRelay;
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
        private bool _closing;
        private bool _disposed;

        public RoomWsClient(IWebSocketClient wsImpl, string clientVersion)
        {
            _ws = wsImpl;
            _clientVersion = clientVersion;

            _onMessage = HandleMessage;
            _onClosed = reason =>
            {
                StopHostSelectionReportLoop();
                _pairProbe.Stop();
                LastHostProbeStatus = "Closed";
                OnClosed?.Invoke(reason);
            };
            _onError = ex =>
            {
                if (ex is OperationCanceledException || IsExpectedClosedSocketException(ex))
                    return;

                OnWarn?.Invoke(ex.Message);
            };
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
            if (_disposed)
                throw new ObjectDisposedException(nameof(RoomWsClient));

            var headers = new Dictionary<string, string> { ["X-Client-Version"] = _clientVersion };

            _closing = false;
            LastHostProbeRttMs = -1;
            LastHostProbeStatus = "Connecting";
            _currentRoomId = "";
            _currentRoomUseP2PRelay = false;
            _latestMemberTransport.Clear();
            _pairProbe.Stop();
            StopHostSelectionReportLoop();
            Subscribe();
            // Pass headers
            await _ws.ConnectAsync(wsUrl, headers, ct);
            Debug.Log($"ws connected: {wsUrl}");
            await SendHostProbePingAsync(ct);
            if (_ws.IsOpen)
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
            _closing = true;
            StopHostSelectionReportLoop();
            _pairProbe.Stop();
            // Just close connection or send specific message?
            // Server handles close as leave.
            return _ws.CloseAsync("leave", ct);
        }

        private Task SendHostProbePingAsync(CancellationToken ct = default)
        {
            if (!CanSend(ct))
                return Task.CompletedTask;

            _pendingProbeNonce = Guid.NewGuid().ToString("N");
            _pendingProbeSentAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            LastHostProbeStatus = "Waiting Pong";
            var req = new HostProbePingRequest { nonce = _pendingProbeNonce };
            return CanSend(ct) ? _ws.SendTextAsync(JsonUtility.ToJson(req), ct) : Task.CompletedTask;
        }

        private Task SendHostProbeReportAsync(int rttMs, CancellationToken ct = default)
        {
            if (!CanSend(ct))
                return Task.CompletedTask;

            LastHostProbeRttMs = Mathf.Max(0, rttMs);
            LastHostProbeStatus = $"Reported {LastHostProbeRttMs} ms";
            var req = new HostProbeReportRequest
            {
                rttMs = LastHostProbeRttMs,
                reportedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            return CanSend(ct) ? _ws.SendTextAsync(JsonUtility.ToJson(req), ct) : Task.CompletedTask;
        }

        private Task SendHostSelectionReportAsync(CancellationToken ct = default)
        {
            if (!CanSend(ct))
                return Task.CompletedTask;

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
            RefreshPairProbeState(steamId64, steamReady);

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
                measuredSteamPairs = BuildMeasuredSteamPairs(),
                reportedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            return CanSend(ct) ? _ws.SendTextAsync(JsonUtility.ToJson(req), ct) : Task.CompletedTask;
        }

        public void Tick()
        {
            if (_disposed || _closing || !CanSend())
            {
                _pairProbe.Stop();
                return;
            }

            var steam = AppBootstrap.Instance != null && AppBootstrap.Instance.Root != null
                ? AppBootstrap.Instance.Root.SteamPlatform
                : null;
            string steamId64 = steam != null ? steam.SteamId64 ?? "" : "";
            bool steamReady = steam != null
                              && steam.Enabled
                              && steam.IsInitialized
                              && steam.HasJoinedLobby
                              && !string.IsNullOrWhiteSpace(steamId64);
            RefreshPairProbeState(steamId64, steamReady);
            _pairProbe.Pump();
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
                if (!CanSend(ct))
                    break;

                try
                {
                    await SendHostSelectionReportAsync(ct);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (InvalidOperationException ex) when (IsExpectedClosedSocketException(ex))
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

        private bool CanSend(CancellationToken ct = default)
        {
            return !_disposed && !_closing && !ct.IsCancellationRequested && _ws != null && _ws.IsOpen;
        }

        private static bool IsExpectedClosedSocketException(Exception ex)
        {
            if (ex == null)
                return false;

            if (ex is ObjectDisposedException)
                return true;

            if (ex is InvalidOperationException)
            {
                var message = ex.Message ?? "";
                bool mentionsSocket = message.IndexOf("WebSocket", StringComparison.OrdinalIgnoreCase) >= 0;
                bool closed = message.IndexOf("Closed", StringComparison.OrdinalIgnoreCase) >= 0
                              || message.IndexOf("CloseReceived", StringComparison.OrdinalIgnoreCase) >= 0
                              || message.IndexOf("not open", StringComparison.OrdinalIgnoreCase) >= 0;
                return mentionsSocket && closed;
            }

            return false;
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
                        ApplyRoomSnapshot(initMsg.room);
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
                        RemoveMemberTransportState(leaveMsg.uid);
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
                        ApplyMemberTransportSnapshot(hostCandidate.memberTransport);
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
            if (_disposed)
                return;

            _closing = true;
            _disposed = true;
            StopHostSelectionReportLoop();
            _pairProbe.Stop();
            Unsubscribe();

            try
            {
                await _ws.CloseAsync("dispose");
            }
            catch (Exception ex) when (ex is OperationCanceledException || IsExpectedClosedSocketException(ex))
            {
            }
        }

        private void ApplyRoomSnapshot(WaitingRoomDto room)
        {
            _currentRoomId = room != null ? room.roomId ?? "" : "";
            _currentRoomUseP2PRelay = room != null && room.useP2PRelay;
            ApplyMemberTransportSnapshot(room != null ? room.memberTransport : null);
        }

        private void ApplyMemberTransportSnapshot(List<MemberTransportState> memberTransport)
        {
            _latestMemberTransport.Clear();
            if (memberTransport == null)
                return;

            for (int i = 0; i < memberTransport.Count; i++)
            {
                var src = memberTransport[i];
                if (src == null || string.IsNullOrWhiteSpace(src.uid))
                    continue;

                _latestMemberTransport.Add(new MemberTransportState
                {
                    uid = src.uid ?? "",
                    name = src.name ?? "",
                    steamId64 = src.steamId64 ?? "",
                    clientVersion = src.clientVersion ?? "",
                    hostProbeRttMs = src.hostProbeRttMs,
                    hostProbeReportedAtMs = src.hostProbeReportedAtMs,
                    steamEnabled = src.steamEnabled,
                    steamInitialized = src.steamInitialized,
                    steamLobbyJoined = src.steamLobbyJoined,
                    steamReady = src.steamReady,
                    currentServerRttMs = src.currentServerRttMs,
                    currentServerLossPct = src.currentServerLossPct,
                    currentServerJitterMs = src.currentServerJitterMs,
                    avgFrameMs = src.avgFrameMs,
                    p95FrameMs = src.p95FrameMs,
                    sendQueueDepth = src.sendQueueDepth,
                    measuredSteamPairs = src.measuredSteamPairs != null
                        ? new List<MeasuredSteamPairState>(src.measuredSteamPairs)
                        : null,
                    hostSelectionReportedAtMs = src.hostSelectionReportedAtMs
                });
            }
        }

        private void RemoveMemberTransportState(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            _latestMemberTransport.RemoveAll(x => x != null && string.Equals(x.uid, uid, StringComparison.OrdinalIgnoreCase));
        }

        private void RefreshPairProbeState(string localSteamId64, bool localSteamReady)
        {
            if (!_currentRoomUseP2PRelay)
            {
                _pairProbe.Stop();
                return;
            }

            _pairProbe.Configure(new RoomSteamPairProbeConfig
            {
                RoomId = _currentRoomId,
                LocalSteamId64 = localSteamId64 ?? "",
                LocalSteamReady = localSteamReady,
                Members = _latestMemberTransport
            });
        }

        private List<MeasuredSteamPairState> BuildMeasuredSteamPairs()
        {
            var pairs = _pairProbe.SnapshotMeasurements();
            return pairs != null && pairs.Count > 0
                ? pairs
                : new List<MeasuredSteamPairState>();
        }
    }

