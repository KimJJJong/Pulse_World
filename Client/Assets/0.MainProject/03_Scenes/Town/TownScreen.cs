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
        //view.GamePreferredRegion.text = "local";
        //view.GameRoomId.text = "room-1";
        //view.GameMap.text = "map_01";
        //view.GameMaxPlayers.text = "2";

        ClearTownResult();

        view.SetBusy(false);
        view.SetStatus("");

        view.TownIssueButton.onClick.AddListener(() => _ = IssueTownTicketAsync());

        view.TownConnectButton.onClick.AddListener(() => _ = ConnectTownAsync());

        view.TownConnectButton.interactable = false;
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



    void ClearTownResult()
    {
        view.TownTicketIdText.text = "TicketId: -";
        view.TownExpireText.text = "ExpireAtMs: -";
        view.TownEndpointText.text = "Endpoint: -";
        view.TownConnectButton.interactable = false;
        _lastTown = null;
    }

}
