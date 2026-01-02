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
        int slot = s.Slot;
        int actorId = GetActorIdBySlot(slot);
        if (actorId < 0) return;
        if (!World2D.ContainsEntity(actorId)) return;

        var pkt = BuildInitPacketForPlayer(slot);
        s.Send(pkt.Write());
    }

    private SC_InitGame BuildInitPacketForPlayer(int playerSlot)
    {
        SC_InitGame packet = new SC_InitGame();

        // 마을은 액션윈도 의미 없으면 0 or 큰값
        packet.ActionWindowMs = 0;

        packet.MapWidth = _map.Width;
        packet.MapHeight = _map.Height;
        packet.MapName = "TownMap"; // TODO

        packet.playerActorIdss.Clear();
        foreach (var p in _players)
            packet.playerActorIdss.Add(new SC_InitGame.PlayerActorIds { ActorId = p.Id });

        packet.MyActorId = GetActorIdBySlot(playerSlot);

        packet.spawnEntitiess.Clear();
        foreach (var p in _players)
        {
            packet.spawnEntitiess.Add(new SC_InitGame.SpawnEntities
            {
                EntityId = p.Id,
                EntityType = (int)p.Type,
                OwnerSlot = p.GetState<int>("Slot"),
                X = p.Position.X,
                Y = p.Position.Y,
                Hp = p.GetState<int>("HP") // 마을이면 굳이 안 쓰면 0으로
            });
        }

        return packet;
    }

    // =====================================================
    // 클라이언트 입력 처리 (마을) — ActionRequest 그대로 받는다
    // =====================================================
    public void OnClientActionPacketBySlot(int slot, CS_ActionRequest req)
    {
        int actorId = GetActorIdBySlot(slot);
        if (actorId < 0)
        {
            Console.WriteLine($"[Town ActionReq] Invalid slot={slot}");
            return;
        }

        // 여기서 Move만 처리(나머지는 무시/거절)
        if (req.ActionKind != (int)ActionKind.Move)
            return;

        // --- Move 파싱(프로젝트에 맞게 교체 필요) ---
        // 가정 A) req에 "DirX/DirY"가 있고, 그리드 1칸 이동
        // int dx = req.DirX;
        // int dy = req.DirY;

        // 가정 B) req에 목표 좌표 "X/Y"가 있음
        // int x = req.X;
        // int y = req.Y;

        // 아래는 "Dir 기반 1칸 이동" 예시.
        // 네 CS_ActionRequest 구조에 맞게 한 줄만 바꾸면 됨.
        if (!World2D.ContainsEntity(actorId))
            return;

        // World2D에서 entity를 가져오는 API가 있으면 그걸 쓰고,
        // 없으면 _players에서 찾아서 Position 수정해도 됨.
        var ent = _players.Find(e => e.Id == actorId);
        if (ent == null) return;

        // TODO: 너 req에서 dx/dy를 꺼내라
        int dx = 0;
        int dy = 0;

        // 예: req에서 방향을 가져오는 코드로 교체
        // dx = req.MoveX;
        // dy = req.MoveY;

        var nx = ent.Position.X + dx;
        var ny = ent.Position.Y + dy;

        // 맵 범위 체크 (없으면 너 맵 API로 교체)
        if (!_map.InBounds(nx, ny))
            return;

        ent.Position = new GridPos(nx, ny);

        // 마을은 즉시 브로드캐스트(스팸) 대신 Update()에서 snapshot으로 묶어 보내는 걸 추천
        // 급하면 여기서 단건 broadcast도 가능:
        // _broadcaster.Broadcast(BuildSingleMovePacket(actorId, nx, ny));
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
