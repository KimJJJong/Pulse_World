using GameServer.Content.Map;
using GameServer.InGame.Manager.Entity;
using System;

public sealed class TownSession : SessionBase
{
    // snapshot 전송(선택)
    private readonly long _snapshotIntervalMs = 100; // 10Hz 권장
    private long _nextSnapshotMs;

    public TownSession(int sessionId, IServerTime time, IGameBroadcaster broadcaster, Map2D map)
        : base(sessionId, time, broadcaster, map)
    {
        _nextSnapshotMs = _time.NowMs + _snapshotIntervalMs;
    }

    public void InitTown(System.Collections.Generic.IEnumerable<MapEntity> players)
    {
        InitPlayers(players);
        Console.WriteLine("[InitTown] End");
    }

    // 마을 init 패킷은 SC_InitGame 그대로 써도 되지만,
    // 보통은 SC_InitTown을 따로 두는 걸 추천.
    // 여기서는 "최소 변경"으로 SC_InitGame 재사용 예시로 작성.
    public override void SendInitPacketToPlayer(ClientSession s)
    {
        int myActorId = s.ActorId;
        if (myActorId < 0) return;
        if (!_actorIds.Contains(myActorId)) return;
        if (!World2D.ContainsEntity(myActorId)) return;

        var pkt = BuildInitPacketForPlayer(myActorId);
        s.Send(pkt.Write());
    }

    private SC_InitGame BuildInitPacketForPlayer(int myActorId)
    {
        SC_InitGame packet = new SC_InitGame();

        // 마을은 액션윈도 의미 없으면 0 or 큰값
        packet.ActionWindowMs = 0;

        packet.MapWidth = _map.Width;
        packet.MapHeight = _map.Height;
        packet.MapName = "Town_01"; // TODO

        packet.playerActorIdss.Clear();
        foreach (var p in _players)
            packet.playerActorIdss.Add(new SC_InitGame.PlayerActorIds { ActorId = p.Id });

        packet.MyActorId = myActorId;

        packet.spawnEntitiess.Clear();
        foreach (var p in _players)
        {
            packet.spawnEntitiess.Add(new SC_InitGame.SpawnEntities
            {
                EntityId = p.Id,
                EntityType = (int)p.Type,

                //  OwnerSlot 같은 slot 개념 제거  -> 그럼 OwnActor?
                // OwnerSlot = ... (없애거나 0 고정)

                X = p.Position.X,
                Y = p.Position.Y,
                Hp = p.GetState<int>("HP")
            });
        }

        return packet;
    }

    // =====================================================
    // 클라이언트 입력 처리 (마을) — ActionRequest 그대로 받는다
    // =====================================================
    public void OnClientActionPacketByActorId(int actorId, CS_ActionRequest req)
    {
        if (actorId < 0)
            return;

        if (!_actorIds.Contains(actorId))
            return;

        if (req.ActionKind != (int)ActionKind.Move)
            return;

        if (!World2D.ContainsEntity(actorId))
            return;

        var ent = _players.Find(e => e.Id == actorId);
        if (ent == null) return;

        // TODO: req에서 dx/dy 추출로 교체
        int dx = 0;
        int dy = 0;

        var nx = ent.Position.X + dx;
        var ny = ent.Position.Y + dy;

        if (!_map.InBounds(nx, ny))
            return;

        ent.Position = new GridPos(nx, ny);
    }

    public override void Update()
    {
        long now = _time.NowMs;
        if (now < _nextSnapshotMs) return;

        _nextSnapshotMs = now + _snapshotIntervalMs;

        // 마을 스냅샷 패킷(새로 만드는 게 제일 좋음)
        // 여기서는 예시로 SC_TownSnapshot이 있다고 가정.
        //var snap = new SC_TownSnapshot();
        //snap.items.Clear();

        //foreach (var p in _players)
        //{
        //    snap.items.Add(new SC_TownSnapshot.Item
        //    {
        //        ActorId = p.Id,
        //        X = p.Position.X,
        //        Y = p.Position.Y
        //    });
        //}

        //_broadcaster.Broadcast(snap.Write());
    }
}
