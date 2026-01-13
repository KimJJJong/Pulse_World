using System;
using System.Threading.Tasks;
using UnityEngine;

public static class TownFlow
{
    /// <summary>
    /// HandshakeOk를 받은 순간 호출.
    /// - Town 씬 로드 요청
    /// - Town 씬 로드 완료 시점에 CS_MapEnter 전송
    /// </summary>
    public static void OnHandshakeOk()
    {
        // 1) Town 씬으로 이동(이미 Town이면 재로드 안 하려면 체크 가능)
        //SceneRouter.Load(SceneNames.TownMap);

        // 2) Town 씬 로드 완료될 때까지 대기 후 MapEnter 보내기
        _ = WaitAndSendMapEnterAsync();
    }

    static async Task WaitAndSendMapEnterAsync()
    {
        var ctx = ClientNetContext.Instance;

        // TownSceneBoot가 MarkTownSceneLoaded() 해줄 때까지 대기
        while (!ctx.TownSceneLoaded)
            await Task.Yield();

        // 이미 InitMap까지 진행 중이면 중복 방지
        if (!ctx.HandshakeOk || ctx.InitMapReceived)
            return;

        // MapEnter 전송
        var nowMs = NowLocalMs();

        var req = new CS_MapEnter
        {
            ClientTimeMs = nowMs,
            MapId = "town",
            LastKnownRevision = 0,
            WantSnapshot = true
        };

        Debug.Log("[TownFlow] CS_MapEnter sent!!!");

        NetworkManager.Instance.Send(req.Write());

    }

    static long NowLocalMs()
        => (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
}
