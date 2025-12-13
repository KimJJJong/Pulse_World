using GameServer.InGame.Manager.Beat;
using GameServer.InGame.Manager.Entity;
using GameServer.InGame.Manager.Map.Interface;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class MonsterAIController
{
    private readonly IGameWorld _world;
    private readonly BeatActionManager _beatActions;

    // monsterId  상태
    private readonly Dictionary<int, MonsterAiState> _states = new();

    public MonsterAIController(IGameWorld world, BeatActionManager beatActions)
    {
        _world = world;
        _beatActions = beatActions;
    }

    public void RegisterMonster(MapEntity monster)
    {
        _states[monster.Id] = MonsterAiState.Idle;
    }

    public void UnregisterMonster(int monsterId)
    {
        _states.Remove(monsterId);
    }

    // GameSession.OnBeat 직전에 호출해도 되고, Update에서 주기적으로 호출해도 됨
    public void UpdateAI(long beatIndex, IEnumerable<MapEntity> monsters, IEnumerable<MapEntity> players)
    {
        var playerList = players.ToList();
        if (playerList.Count == 0)
            return;

        foreach (var m in monsters)
        {
            if (!m.IsAlive)
                continue;

            if (!_states.TryGetValue(m.Id, out var state))
                state = MonsterAiState.Idle;

            // 1) 가장 가까운 플레이어 찾기
            var targetPlayer = FindClosestPlayer(m, playerList, out int dist);

            // 2) 상태 전이 (간단 예시)
            if (dist <= 1)
            {
                state = MonsterAiState.Attack;
            }
            else if (dist <= 5)
            {
                state = MonsterAiState.Chase;
            }
            else
            {
                state = MonsterAiState.Idle;
            }

            _states[m.Id] = state;

            // 3) 상태에 따른 행동  Beat에 명령 예약
            switch (state)
            {
                case MonsterAiState.Idle:
                    {
                        // 가끔 대기 액션 넣어도 되고, 아무것도 안 해도 됨
                        //_beatActions.ScheduleServerCommand(beatIndex, new PlayerActionCmd { ... Wait ... });
                        Console.WriteLine($"[MonsterAiState.Idel] ID :{m.Id} || IsAlive : {m.IsAlive} || Pos :({targetPlayer.Position}) ");

                        break;
                    }

                case MonsterAiState.Chase:
                    {
                        var nextPos = StepTowards(m.Position, targetPlayer.Position);
                        var cmd = new PlayerActionCmd
                        {
                            ActorId = m.Id,
                            Kind = ActionKind.Move,
                            TargetCell = nextPos,
                            ClientSendTimeMs = 0,
                            ServerReceiveTimeMs = 0
                        };
                        Console.WriteLine($"[MonsterAiState.Chase] ID :{m.Id} || IsAlive : {m.IsAlive} || Pos : ({nextPos}) -> ({targetPlayer.Position})");
                        _beatActions.ScheduleServerCommand(beatIndex, cmd);
                        break;
                    }

                case MonsterAiState.Attack:
                    {
                        // 공격은 위치 변화 없이 Skill 처리만 한다고 가정
                        var cmd = new PlayerActionCmd
                        {
                            ActorId = m.Id,
                            Kind = ActionKind.Skill,
                            TargetCell = m.Position,
                            ClientSendTimeMs = 0,
                            ServerReceiveTimeMs = 0
                        };
                        Console.WriteLine($"[MonsterAiState.Attack] ID :{m.Id} || IsAlive : {m.IsAlive} || Pos :({targetPlayer.Position}) || HP :({m.GetState<int>("HP")}) -> ({targetPlayer.GetState<int>("HP")})");

                        _beatActions.ScheduleServerCommand(beatIndex, cmd);
                        break;
                    }

                case MonsterAiState.Retreat:
                    {
                        // 필요하면 도망 로직 추가
                        break;
                    }
            }
        }
    }

    private MapEntity FindClosestPlayer(MapEntity m, List<MapEntity> players, out int dist)
    {
        MapEntity best = players[0];
        dist = int.MaxValue;

        foreach (var p in players)
        {
            int d = Math.Abs(p.Position.X - m.Position.X) + Math.Abs(p.Position.Y - m.Position.Y);
            if (d < dist)
            {
                dist = d;
                best = p;
            }
        }

        return best;
    }

    private GridPos StepTowards(GridPos from, GridPos to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;

        if (Math.Abs(dx) > Math.Abs(dy))
        {
            return new GridPos(from.X + Math.Sign(dx), from.Y);
        }
        else if (dy != 0)
        {
            return new GridPos(from.X, from.Y + Math.Sign(dy));
        }
        else
        {
            return from;
        }
    }
}
