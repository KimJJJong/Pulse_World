using GameServer.Content.Map;
using GameServer.InGame.Manager.Beat;
using GameServer.InGame.Manager.Entity;
using GameServer.InGame.System.Rhythm;
using System;
using System.Linq;
using static SC_BeatTelegraphs;

public sealed class TownSession : SessionBase
{
    // [RTT Fix] 100ms -> 33ms: Town 통 틱 주기와 동일하게 맞춤
    // 스냅샷은 여전히 10Hz(100ms)로도 충분하지만, 답변 지연 크기를 맞춰 연산
    private readonly long _snapshotIntervalMs = 100; // 실제 broadcast는 100ms 유지 (bandwidth 절약)
    private readonly long _tickIntervalMs = 33;      // 클라 패킷 큐 처리는 33ms
    private long _nextSnapshotMs;

    public BeatActionManager BeatActions { get; }

    private readonly RhythmSystem _rhythm;
    private readonly RhythmConfig _rhythmConfig;

    public TownSession(
        int sessionId,
        IServerTime time,
        IGameBroadcaster broadcaster,
        RhythmSystem rhythm,
        RhythmConfig rhythmConfig,
        Map2D map
        )
        : base(sessionId, time, broadcaster, map)
    {
        _rhythm = rhythm;
        _rhythmConfig = rhythmConfig;

        var telegraph = new TelegraphScheduler(broadcaster);
        BeatActions = new BeatActionManager(
            time,
            broadcaster,
            rhythm,
            World,
            null,
            telegraph,
            _rhythmConfig.ActionWindowMs,       //Town은 Game과 차이를 둔다
            _rhythmConfig.MaxBeatLookAhead      //필요없을거 같은데 지금은
            );

        _nextSnapshotMs = _time.NowMs + _snapshotIntervalMs;

    }

    public void InitTown(System.Collections.Generic.IEnumerable<MapEntity> players)
    {
        InitPlayers(players);
    }
    public void OnPlayerJoined(MapEntity player)
    {
        if (player == null) return;

        // 중복 방지
        if (_actorIds.Contains(player.Id))
            return;

        if (!World2D.TrySpawn(player, player.Position))
        {
            Console.WriteLine($"[OnPlayerJoined] Spawn failed actorId={player.Id} pos={player.Position.X},{player.Position.Y}");
            return;
        }

        _players.Add(player);
        _actorIds.Add(player.Id);

        Console.WriteLine($"[OnPlayerJoined] OK actorId={player.Id}");

        var beat = _rhythm.GetCurrentBeatIndex(_time.NowMs);

        _broadcaster.Broadcast(new SC_EntitySpawn
        {
            BeatIndex = beat,
            EntityId = player.Id,
            EntityType = (int)player.Type,
            X = player.Position.X,
            Y = player.Position.Y,
            Hp = player.GetState<int>("HP")
        });

    }



    // 마을 init 패킷은 SC_InitGame 그대로 써도 되지만,
    // 보통은 SC_InitTown을 따로 두는 걸 추천.
    // 여기서는 "최소 변경"으로 SC_InitGame 재사용 예시로 작성.
    public override void SendInitPacketToPlayer(ClientSession s)
    {
        int myActorId = s.ActorId;
        if (myActorId < 0)
        {
            Console.WriteLine($"[Init] FAIL myActorId<0 uid={s.Uid} epoch={s.Epoch} seat={s.SeatIndex} actor={s.ActorId}");
            return;
        }

        if (!_actorIds.Contains(myActorId))
        {
            Console.WriteLine($"[Init] FAIL !_actorIds.Contains actor={myActorId} players={_players.Count} actorIds={_actorIds.Count}");
            return;
        }

        if (!World2D.ContainsEntity(myActorId))
        {
            Console.WriteLine($"[Init] FAIL !World2D.ContainsEntity actor={myActorId}");
            return;
        }

        Console.WriteLine($"[Init] OK actor={myActorId} players={_players.Count}");
        var pkt = BuildInitPacketForPlayer(myActorId);

        s.Send(pkt.Write());

        //  Inventory Init
        System.Threading.Tasks.Task.Run(async () =>
        {
            // Force Reload from API to ensure sync (e.g. Equip changed in Home)
            var invItems = await ServerServices.InventoryManager.LoadInventoryAsync(s.Uid, forceReload: true);
            
            var invPkt = new SC_Inventory();
            
            // Sort by SlotIndex
            var sortedItems = invItems.OrderBy(x => x.SlotIndex).ToList();

            foreach(var item in sortedItems)
            {
                var equipTmpl = ServerServices.ItemTemplates.GetEquipment(item.TemplateId);
                if (equipTmpl != null)
                {
                    invPkt.equipmentss.Add(new SC_Inventory.Equipments
                    {
                        InstanceId = item.InstanceId,
                        TemplateId = item.TemplateId,
                        SlotIndex = item.SlotIndex,
                        EnhancementLevel = item.EnhancementLevel,
                        IsEquipped = item.IsEquipped,
                        BaseStats = Newtonsoft.Json.JsonConvert.SerializeObject(item.BaseStats),
                        RandomOptions = Newtonsoft.Json.JsonConvert.SerializeObject(item.RandomOptions),
                        AcquiredAt = item.AcquiredAt.ToString("O")
                    });
                }
                else
                {
                    invPkt.itemss.Add(new SC_Inventory.Items
                    {
                        InstanceId = item.InstanceId,
                        TemplateId = item.TemplateId,
                        Amount = item.Amount,
                        SlotIndex = item.SlotIndex,
                        AcquiredAt = item.AcquiredAt.ToString("O")
                    });
                }
            }
            s.Send(invPkt.Write());
            Console.WriteLine($"[TownSession] Sent SC_Inventory to Actor {myActorId} ({invPkt.itemss.Count} Items, {invPkt.equipmentss.Count} Equipments)");
        });
    }


    private SC_InitMap BuildInitPacketForPlayer(int myActorId)
    {
        SC_InitMap packet = new SC_InitMap();


        packet.Mode = 1;    // tmp 마을, 게임 구분용
        packet.ActionWindowMs = _rhythmConfig.ActionWindowMs;
        packet.SongId = "TestSong";
        packet.Bpm = _rhythmConfig.Bpm;
        packet.BaseBeatDivision = _rhythmConfig.BaseBeatDivision;
        packet.SongStartServerTime = _rhythm.SongStartServerTimeMs;

        packet.MapWidth = _map.Width;
        packet.MapHeight = _map.Height;
        packet.MapId = "Town_01"; // TODO

        packet.playerss.Clear();
        foreach (var p in _players)
            packet.playerss.Add(new SC_InitMap.Players { ActorId = p.Id, Name="Test", Uid="RealNeedit?" }); 

        packet.MyActorId = myActorId;

        packet.entitiess.Clear();
        foreach (var p in _players)
        {
            packet.entitiess.Add(new SC_InitMap.Entities
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
    public void OnClientActionPacketByActorId(int actorId, CS_TownActionRequest req)
    {
        if (actorId < 0) return;
        BeatActions.OnTownClientActionRequest(actorId, req);
    }

    public void OnClientCalibPacketByActorId(int actorId, CS_CalibHit req)
    {
        if (actorId < 0) return;
        BeatActions.OnClientCalibRequest(actorId, req);
    }

    public void OnBeat(long beatIndex)
    {

        BeatActions.OnBeat(beatIndex);
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
