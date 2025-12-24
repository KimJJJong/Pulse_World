using GameServer.Content.Map;
using GameServer.Content.Map.Interface;
using GameServer.InGame.System.Rhythm;
using System;
using System.Collections.Generic;



namespace GameServer.InGame.Manager.Beat
{
    public sealed class BeatActionManager
    {
        private readonly IServerTime _time;
        private readonly IGameBroadcaster _broadcaster;
        private readonly IBeatClock _clock;
        private readonly IGameWorld _world;

        private readonly BeatScheduler _scheduler = new();
        private readonly FrozenAttackRegistry _frozen;

        private readonly double _actionWindowMs;
        private readonly int _maxBeatLookAhead;

        public BeatActionManager(
            IServerTime time,
            IGameBroadcaster broadcaster,
            IBeatClock clock,
            IGameWorld world,
            FrozenAttackRegistry frozenAttackRegistry,
            double actionWindowMs,
            int maxBeatLookAhead)
        {
            _time = time;
            _broadcaster = broadcaster;
            _clock = clock;
            _world = world;

            _frozen = frozenAttackRegistry;

            _actionWindowMs = actionWindowMs;
            _maxBeatLookAhead = maxBeatLookAhead;

        }

        /// <summary>
        /// 클라이언트 입력 도착 시 호출.
        /// CS_ActionRequest를 PlayerActionCmd로 변환하고, Beat 판정 후 스케줄에 등록.
        /// </summary>
        public void OnClientActionRequest(int actorId, CS_ActionRequest req)
        {
            var now = _time.NowMs;

            //Console.WriteLine($" In OnClientActionRequest");
            // 1) 현재 Beat 계산
            var currBeat = _clock.GetCurrentBeatIndex(now);

            // 2) 입력 도착 시점을 기준으로 가장 가까운 Beat 선택
            var nearestBeat = _clock.GetNearestBeatIndex(now);
            var nearestBeatTime = _clock.GetBeatTimeMs(nearestBeat);
            var diff = Math.Abs(now - nearestBeatTime);

            // 3) 판정 윈도우 밖이면 무시 (원하면 Fail 패킷 보내도 됨)
            if (diff > _actionWindowMs)
            {
                //Console.WriteLine($"diff > _actionWindowMs :{diff} > {_actionWindowMs}");
                // TODO: 필요하면 SC_ActionRejected 같은 패킷 보내기
                return;
            }

            // 4) 너무 과거/미래 Beat는 거절
            if (nearestBeat < currBeat || nearestBeat > currBeat + _maxBeatLookAhead)
            {
                Console.WriteLine($"nearestBeat < currBeat || nearestBeat > currBeat + _maxBeatLookAhead");
                return;
            }

            // 5) PlayerActionCmd 생성 (CS_ActionRequest -> 내부 명령으로 변환)
            var cmd = new PlayerActionCmd
            {
                ActorId = actorId,                          
                Kind = (ActionKind)req.ActionKind,               
                TargetCell = new GridPos(req.TargetX, req.TargetY),
                RequestedBeat = nearestBeat,
                ClientSendTimeMs = req.ClientSendTimeMs,
                ServerReceiveTimeMs = now
            };
            _scheduler.Enqueue(cmd);
        }
        // 서버에서 직접 예약하고 싶은 명령 (몬스터 AI 등)
        public void ScheduleServerCommand(long beatIndex, PlayerActionCmd cmd)
        {
            var currBeat = _clock.GetCurrentBeatIndex(_time.NowMs);
            //Console.WriteLine($"[Enqueue] actor={cmd.ActorId} kind={cmd.Kind} reqBeat={cmd.RequestedBeat}");

            cmd.RequestedBeat = beatIndex;
            _scheduler.Enqueue(cmd);
        }


        /// <summary>
        /// RhythmSystem에서 Beat가 도래할 때마다 호출.
        /// 해당 Beat에 예약된 액션들을 꺼내서 World에 적용하고, 결과를 SC_BeatActions로 브로드캐스트.
        /// </summary>
        public void OnBeat(long beatIndex)
        {
                //Console.WriteLine($"[OnBeat] BeatIndex:{beatIndex} ");
            var cmds = _scheduler.PopActions(beatIndex);
            Console.WriteLine($"[OnBeat] BeatIndex:{beatIndex} || CommandCount :{cmds.Count}");
            if (cmds.Count == 0) return;
            var results = new List<SC_BeatActions.BeatActionResult>(cmds.Count);
            //var used = new HashSet<int>(); //  Actor당 1 action/beat

            foreach (var cmd in cmds)
            {
                //if (!used.Add(cmd.ActorId))
                //    continue;

                //if (cmd.Kind == ActionKind.Skill) Console.WriteLine("여기가 실 데미지 적용");

                if (!_world.TryGetActorPosition(cmd.ActorId, out var fromPos))
                {
                    //Console.WriteLine($"[BeatAction] unknown actorId={cmd.ActorId} kind={cmd.Kind}");

                    // 프로토: 결과라도 내려주고 싶으면 아래 블록 유지
                    results.Add(new SC_BeatActions.BeatActionResult
                    {
                        ActorId = cmd.ActorId,
                        ActionKind = (int)cmd.Kind,
                        FromX = 0,
                        FromY = 0,
                        ToX = 0,
                        ToY = 0,
                        Accepted = false
                    });
                    continue;
                }

                var toPos = fromPos;
                bool accepted;

                var hpUpdates = new List<HpUpdate>(4);

                switch (cmd.Kind)
                {
                    case ActionKind.Move:
                        toPos = cmd.TargetCell;
                        accepted = _world.TryMove(cmd.ActorId, toPos);
                        //Console.WriteLine($"[Move] Entity : {cmd.ActorId} || {fromPos} -> {toPos}");
                        if (!accepted)
                        {
                            Console.WriteLine($"[MoveRejected] actor={cmd.ActorId} from={fromPos} to={toPos}");
                            toPos = fromPos;
                        }
                        break;

                    case ActionKind.Skill:
                        {
                            //  Frozen cells가 있으면 그걸로 판정(예고와 동일)
                            if (_frozen.TryPop(cmd.ActorId, beatIndex, out var frozen))
                            {
                                //Console.WriteLine($"[ActionKin.Skill] Infrozen || Attacker : {cmd.ActorId}");
                                accepted = _world.TryUseSkillArea(cmd.ActorId, frozen.SkillId, frozen.Cells, hpUpdates);
                            }
                            else
                            {
                                //  fallback (플레이어 입력 등)
                                accepted = _world.TryUseSkill(cmd.ActorId, cmd.SkillId, cmd.TargetCell.X, cmd.TargetCell.Y, hpUpdates);
                            }

                            toPos = fromPos;
                            break;

                         }

                    case ActionKind.Wait:
                    default:
                        accepted = true;
                        toPos = fromPos;
                        break;
                }

                var pktHpUpdates = new List<SC_BeatActions.BeatActionResult.HpUpdate>(hpUpdates.Count);
                if (accepted && hpUpdates.Count > 0)
                {
                    foreach (var u in hpUpdates)
                    {
                        pktHpUpdates.Add(new SC_BeatActions.BeatActionResult.HpUpdate
                        {
                            EntityId = u.EntityId,
                            NewHp = u.NewHp
                        });
                    }
                }

                results.Add(new SC_BeatActions.BeatActionResult
                {
                    ActorId = cmd.ActorId,
                    ActionKind = (int)cmd.Kind,
                    FromX = fromPos.X,
                    FromY = fromPos.Y,
                    ToX = toPos.X,
                    ToY = toPos.Y,
                    Accepted = accepted,

                    hpUpdates = pktHpUpdates
                });
            }

            if (results.Count == 0) return;

            _broadcaster.Broadcast(new SC_BeatActions
            {
                BeatIndex = beatIndex,
                beatActionResults = results
            });

            _scheduler.DropBefore(beatIndex - 4);
            _frozen.DropBefore(beatIndex - 16);

        }

        public void CancelActor(int actorId)
        {
            _scheduler.RemoveByActor(actorId);       
        }
    }
}
