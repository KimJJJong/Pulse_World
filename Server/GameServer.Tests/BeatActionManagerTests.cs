using System.Collections.Generic;
using System.Linq;
using GameServer.Content.Map;
using GameServer.Content.Map.Interface;
using GameServer.InGame.Manager.Beat;
using GameServer.InGame.Manager.Entity;
using GameServer.InGame.System.Rhythm;

namespace GameServer.Tests;

public sealed class BeatActionManagerTests
{
    [Fact]
    public void LateSkillRequestAfterJudgeWindowEndStillBroadcastsBeatAction()
    {
        var time = new FakeServerTime { NowMs = 1200 };
        var rhythm = new RhythmSystem(
            time,
            new RhythmConfig
            {
                Bpm = 120,
                BaseBeatDivision = 1,
                ActionWindowMs = 100,
                MaxBeatLookAhead = 2
            },
            songStartServerTimeMs: 1000);

        var broadcaster = new CapturingBroadcaster();
        var actor = new MapEntity(1, EntityType.Player, new GridPos(3, 4));
        var world = new FakeWorld(actor);

        var manager = new BeatActionManager(
            time,
            broadcaster,
            rhythm,
            world,
            new FrozenAttackRegistry(),
            new TelegraphScheduler(broadcaster),
            actionWindowMs: 100,
            maxBeatLookAhead: 2);

        manager.OnJudgeWindowEnd(0);

        manager.OnClientActionRequest(actor.Id, new CS_ActionRequest
        {
            ActorId = actor.Id,
            ActionKind = (int)ActionKind.Skill,
            SlotIndex = 99,
            TargetX = 4,
            TargetY = 4,
            Rotation = 90f,
            ClientSendTimeMs = 1000
        });

        Assert.Contains(
            broadcaster.Packets.OfType<SC_ActionInstantBroadcast>(),
            p => p.ActorId == actor.Id && p.StartTick == 0);

        var beatPacket = Assert.Single(broadcaster.Packets.OfType<SC_BeatActions>());
        Assert.Equal(0, beatPacket.BeatIndex);

        var result = Assert.Single(beatPacket.beatActionResults);
        Assert.Equal(actor.Id, result.ActorId);
        Assert.Equal((int)ActionKind.Skill, result.ActionKind);
        Assert.False(result.Accepted);
    }

    private sealed class FakeServerTime : IServerTime
    {
        public long NowMs { get; set; }
    }

    private sealed class CapturingBroadcaster : IGameBroadcaster
    {
        public List<IPacket> Packets { get; } = new();

        public void Broadcast(IPacket pkt)
        {
            Packets.Add(pkt);
        }
    }

    private sealed class FakeWorld : IGameWorld
    {
        private readonly MapEntity _actor;

        public FakeWorld(MapEntity actor)
        {
            _actor = actor;
        }

        public GridPos GetActorPosition(int actorId) => _actor.Position;

        public bool TryGetActorPosition(int actorId, out GridPos pos)
        {
            if (actorId == _actor.Id)
            {
                pos = _actor.Position;
                return true;
            }

            pos = default;
            return false;
        }

        public bool ContainsEntity(int actorId) => actorId == _actor.Id;

        public bool TryGetEntity(int entityId, out MapEntity entity)
        {
            if (entityId == _actor.Id)
            {
                entity = _actor;
                return true;
            }

            entity = null!;
            return false;
        }

        public IEnumerable<MapEntity> GetEntitiesAt(GridPos pos)
            => Enumerable.Empty<MapEntity>();

        public bool TryMove(int actorId, GridPos target) => false;

        public bool TryDash(int actorId, int dirX, int dirY, int distance, out GridPos landedPos)
        {
            landedPos = _actor.Position;
            return false;
        }

        public bool TryPreviewDash(GridPos from, int dirX, int dirY, int distance, out GridPos landedPos)
        {
            landedPos = from;
            return false;
        }

        public bool TryBlink(int actorId, int dirX, int dirY, int distance, out GridPos landedPos)
        {
            landedPos = _actor.Position;
            return false;
        }

        public bool TryPreviewBlink(GridPos from, int dirX, int dirY, int distance, out GridPos landedPos)
        {
            landedPos = from;
            return false;
        }

        public bool TryUseCustomSkill(
            int actorId,
            long currentTick,
            FrozenAttackRegistry.FrozenAttack frozen,
            List<HpUpdate> hpUpdates)
            => false;
    }
}
