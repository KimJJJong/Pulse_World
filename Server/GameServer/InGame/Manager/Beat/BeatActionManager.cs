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

        //private readonly int _leadBeat;
        private readonly double _actionWindowMs;
        private readonly int _maxBeatLookAhead;

        public BeatActionManager(
            IServerTime time,
            IGameBroadcaster broadcaster,
            IBeatClock clock,
            IGameWorld world,
            FrozenAttackRegistry frozenAttackRegistry,
            double actionWindowMs,
            int maxBeatLookAhead
            /*int leadBeat*/)
        {
            _time = time;
            _broadcaster = broadcaster;
            _clock = clock;
            _world = world;

            _frozen = frozenAttackRegistry;

            _actionWindowMs = actionWindowMs;
            _maxBeatLookAhead = maxBeatLookAhead;
            //_leadBeat = leadBeat;

        }

        /// <summary>
        /// 클라이언트 입력 도착 시 호출.
        /// CS_ActionRequest를 PlayerActionCmd로 변환하고, Beat 판정 후 스케줄에 등록.
        /// </summary>
        public void OnClientActionRequest(int actorId, CS_ActionRequest req)
        {
            long now = req.ClientSendTimeMs;

            if (!_clock.TryComputeJudge(
                    nowMs: now,
                    actionWindowMs: _actionWindowMs,
                    out var judge))
            {
                // TryComputeJudge 안에서 song not started / out of range 등을 판단
                return;
            }

            // 디버그 바 (항상 출력)
            PrintJudgeBar(judge, now);

            if (!judge.IsAccepted)
            {
                Console.WriteLine($"[Reject] out of window. diff={judge.DiffMs}ms (±{_actionWindowMs}ms)");
                return;
            }

            // Request -> Cmd 변환 (액션별 파싱/검증)
            if (!ActionRequestTranslator.TryBuildCmd(actorId, req, judge.ExecuteBeat, judge.DiffMs, now, out var cmd, out var reason))
            {
                Console.WriteLine($"[Reject] invalid action payload. reason={reason}");
                return;
            }

            _scheduler.Enqueue(cmd);

            Console.WriteLine($"[Accept] kind={cmd.Kind} currBeat={judge.CurrBeat} executeBeat={cmd.ExecuteBeat} diff={judge.DiffMs}ms");
        }

     
        /// <summary>
        /// sync Setting
        /// </summary>
        /// <param name="actorId"></param>
        /// <param name="req"></param>
        public void OnClientCalibRequest(int actorId, CS_CalibHit req)
        {
            var now = req.ClientSendTimeMs;//_time.NowMs;

            // ---- Beat 계산 ----
            var currBeat = _clock.GetCurrentBeatIndex(now);
            if (currBeat < 0)
            {
                Console.WriteLine("[OnClientActionRequest] song not started yet");
                return;
            }

            var nextBeat = currBeat + 1;

            var judgeCenterMs = _clock.GetJudgeTimeMs(currBeat, nextBeat);

            int diff = (int)(now - judgeCenterMs);

            int halfSpanMs = (int)Math.Round(_clock.GetBeatDurationMs() * 0.5); // RhythmSystem에 추가한 getter 필요
            Console.WriteLine(
                RhythmSystem.FormatJudgeBar(
                    currBeat: currBeat,
                    nextBeat: nextBeat,
                    nowMs: now,
                    judgeCenterMs: judgeCenterMs,
                    windowMs: (int)_actionWindowMs,
                    halfSpanMs: halfSpanMs,
                    width: 36,
                    marker: '^'
                )
            );
            
            var send = new SC_CalibResult();

            send.DiffMs = diff;
            send.ServerNowMs = now;
            send.BeatIndex = nextBeat;

            _broadcaster.Broadcast(/*actorId, */send);

        }





        // 서버에서 직접 예약하고 싶은 명령 (몬스터 AI 등)
        public void ScheduleServerCommand(long beatIndex, PlayerActionCmd cmd)
        {
            var currBeat = _clock.GetCurrentBeatIndex(_time.NowMs);

            cmd.ExecuteBeat = beatIndex;
            _scheduler.Enqueue(cmd);
        }


        /// <summary>
        /// RhythmSystem에서 Beat가 도래할 때마다 호출.
        /// 해당 Beat에 예약된 액션들을 꺼내서 World에 적용하고, 결과를 SC_BeatActions로 브로드캐스트.
        /// </summary>
        public void OnBeat(long beatIndex)
        {
            var cmds = _scheduler.PopActions(beatIndex);
            if (cmds.Count == 0) return;
            var results = new List<SC_BeatActions.BeatActionResult>(cmds.Count);

            foreach (var cmd in cmds)
            {


                //if (cmd.ActorId >= 0 && cmd.ActorId < 100) Console.WriteLine($"[OnBeat] ActionId:{cmd.ActorId} || Beat : {beatIndex}");

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
                            //Console.WriteLine($"[MoveRejected] actor={cmd.ActorId} from={fromPos} to={toPos}");
                            toPos = fromPos;
                        }
                        break;
                    case ActionKind.Attack:
                        {
                                accepted = _world.TryUseAttack(cmd.ActorId, cmd.TargetCell.X, cmd.TargetCell.Y, hpUpdates);

                            //toPos = fromPos;
                            break;

                        }

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
   

    ///============== Util ====================
       private void PrintJudgeBar(JudgeResult judge, long nowMs)
        {
            int halfSpanMs = (int)Math.Round(_clock.GetBeatDurationMs() * 0.5);

            Console.WriteLine(
                RhythmSystem.FormatJudgeBar(
                    currBeat: judge.CurrBeat,
                    nextBeat: judge.NextBeat,
                    nowMs: nowMs,
                    judgeCenterMs: judge.CenterMs,
                    windowMs: (int)_actionWindowMs,
                    halfSpanMs: halfSpanMs,
                    width: 36,
                    marker: '^'
                )
            );
        }

    }

}



public readonly struct JudgeResult
{
    public readonly long CurrBeat;
    public readonly long NextBeat;
    public readonly long CenterMs;
    public readonly int DiffMs;
    public readonly bool IsAccepted;
    public readonly long ExecuteBeat;

    public JudgeResult(long currBeat, long nextBeat, long centerMs, int diffMs, bool accepted, long executeBeat)
    {
        CurrBeat = currBeat;
        NextBeat = nextBeat;
        CenterMs = centerMs;
        DiffMs = diffMs;
        IsAccepted = accepted;
        ExecuteBeat = executeBeat;
    }
}
