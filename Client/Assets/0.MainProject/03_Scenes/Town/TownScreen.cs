using System;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

public sealed class TownScreen : MonoBehaviour
{
    [SerializeField] TownView view = null!;

    SessionDtos.IssueTownTicketResponse? _lastTown;
    SessionDtos.IssueGameTicketResponse? _lastGame;

    void Awake()
    {
        // 기본값(UX)
        view.TownPreferredRegion.text = "local";
        view.GamePreferredRegion.text = "local";
        view.GameRoomId.text = "room-1";
        view.GameMap.text = "map_01";
        view.GameMaxPlayers.text = "2";

        ClearTownResult();
        ClearGameResult();

        view.SetBusy(false);
        view.SetStatus("");

        view.TownIssueButton.onClick.AddListener(() => _ = IssueTownTicketAsync());
        view.GameIssueButton.onClick.AddListener(() => _ = IssueGameTicketAsync());

        view.TownConnectButton.onClick.AddListener(() => _ = ConnectTownAsync());
        view.GameConnectButton.onClick.AddListener(() => _ = ConnectGameAsync());

        view.TownConnectButton.interactable = false;
        view.GameConnectButton.interactable = false;
    }

    async Task IssueTownTicketAsync()
    {
        view.SetStatus("");
        view.SetBusy(true);

        var root = AppBootstrap.Instance.Root;
        var region = (view.TownPreferredRegion.text ?? "").Trim();


        var r = await root.SessionApi.IssueTownTicketAsync(region);
        view.SetBusy(false);

        if (!r.Ok)
        {
            view.SetStatus(r.Error);
            ClearTownResult();
            return;
        }

        _lastTown = r.Data;
        RenderTownResult(r.Data);
        view.SetStatus("Town 티켓 발급 완료");
        view.TownConnectButton.interactable = true;
    }

    async Task IssueGameTicketAsync()
    {
        view.SetStatus("");
        view.SetBusy(true);

        var root = AppBootstrap.Instance.Root;

        var region = (view.GamePreferredRegion.text ?? "").Trim();
        var roomId = (view.GameRoomId.text ?? "").Trim();
        var map = (view.GameMap.text ?? "").Trim();

        if (!int.TryParse((view.GameMaxPlayers.text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxPlayers))
            maxPlayers = 2;

        Debug.Log($"[IssueGameTicketAsync] regin: {region} || roomId: {roomId} || Map :{map} || MaxPlayer: {maxPlayers}");
        var r = await root.SessionApi.IssueGameTicketAsync(roomId, map, maxPlayers, region);
        view.SetBusy(false);

        if (!r.Ok)
        {
            Debug.LogWarning(r.Error);
            view.SetStatus(r.Error);
            ClearGameResult();
            return;
        }

        _lastGame = r.Data;
        RenderGameResult(r.Data);
        view.SetStatus("Game 티켓 발급 완료");
        view.GameConnectButton.interactable = true;
    }

    async Task ConnectTownAsync()
    {
        if (_lastTown == null)
        {
            view.SetStatus("먼저 Town 티켓을 발급하세요.");
            return;
        }

        // clientNonce는 네 규격에 맞게(예: MatchId/Guid)
        var clientNonce = "town-" + System.Guid.NewGuid().ToString("N");

        view.SetStatus("연결 시도 중... (Handshake 대기)"); 
        await ClientFlow.Instance.ConnectTown(_lastTown, clientNonce);
    }

    async Task ConnectGameAsync()
    {

            view.SetStatus("GameRoom은 분리");
    }


    void RenderTownResult(SessionDtos.IssueTownTicketResponse d)
    {
        view.TownTicketIdText.text = $"TicketId: {d.TicketId}";
        view.TownExpireText.text = $"ExpireAtMs: {d.ExpireAtMs}";
        view.TownEndpointText.text = $"Endpoint: {d.Endpoint.Host}:{d.Endpoint.Port}";
    }

    void RenderGameResult(SessionDtos.IssueGameTicketResponse d)
    {
        view.GameTransitionIdText.text = $"TransitionId: {d.TransitionId}";
        view.GameTicketIdText.text = $"TicketId: {d.TicketId}";
        view.GameExpireText.text = $"ExpireAtMs: {d.ExpireAtMs}";
        view.GameServerIdText.text = $"ServerId: {d.ServerId}";
        view.GameEndpointText.text = $"Endpoint: {d.Endpoint.Host}:{d.Endpoint.Port}";
        view.GameKeyText.text = $"Key: {d.Key}";
    }

    void ClearTownResult()
    {
        view.TownTicketIdText.text = "TicketId: -";
        view.TownExpireText.text = "ExpireAtMs: -";
        view.TownEndpointText.text = "Endpoint: -";
        view.TownConnectButton.interactable = false;
        _lastTown = null;
    }

    void ClearGameResult()
    {
        view.GameTransitionIdText.text = "TransitionId: -";
        view.GameTicketIdText.text = "TicketId: -";
        view.GameExpireText.text = "ExpireAtMs: -";
        view.GameServerIdText.text = "ServerId: -";
        view.GameEndpointText.text = "Endpoint: -";
        view.GameKeyText.text = "Key: -";
        view.GameConnectButton.interactable = false;
        _lastGame = null;
    }
}
