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
            var now = req.ClientSendTimeMs;//_time.NowMs;

            // ---- Beat 계산 ----
            var currBeat = _clock.GetCurrentBeatIndex(now);
            if (currBeat < 0)
            {
                Console.WriteLine("[OnClientActionRequest] song not started yet");
                return;
            }

            var nextBeat = currBeat + 1;

            // ---- 판정 중심(비트와 비트 사이 중간점) ----
            // 네 설계: 입력은 항상 nextBeat 실행으로 묶으니까
            // judge center는 (currBeat ~ nextBeat) 중간점 = GetJudgeTimeMs(nextBeat)
            var judgeCenterMs = _clock.GetJudgeTimeMs(currBeat, nextBeat);

            var diff = now - judgeCenterMs;
            var abs = Math.Abs(diff);

            // ---- 디버그 바 출력 (항상 찍고 싶으면 여기) ----
            // halfSpanMs: "비트 사이 전체 폭"을 보여주고 싶으면 beatMs/2
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

            // ---- Window 체크 ----
            if (abs > _actionWindowMs)
            {
                Console.WriteLine($"[Reject] out of window. diff={diff}ms (±{_actionWindowMs}ms)");
                return;
            }

            // ---- 실행 Beat (기본: nextBeat 고정) ----
            var executeBeat = nextBeat;

            Console.WriteLine($"[Accept] currBeat={currBeat} executeBeat={executeBeat} diff={diff}ms");

            var cmd = new PlayerActionCmd
            {
                ActorId = actorId,
                Kind = (ActionKind)req.ActionKind,
                TargetCell = new GridPos(req.TargetX, req.TargetY),

                ExecuteBeat = executeBeat,

                // debug 남기고 싶으면 주석 해제
                //JudgedBeat = nextBeat,
                //JudgeDiffMs = (int)diff,

                ClientSendTimeMs = req.ClientSendTimeMs,
                ServerReceiveTimeMs = now
            };

            _scheduler.Enqueue(cmd);
        }



        private static string FormatJudgeBar(
    long currBeat, long nextBeat,
    long nowMs, long judgeCenterMs,
    int windowMs,
    int halfSpanMs = 250,   // 바가 표현하는 "중간점 기준 좌/우 범위" (ms)
    int width = 32,         // 바 내부 폭
    char marker = '^')
        {
            // halfSpanMs는 최소 window보다 커야 그림이 의미 있음
            halfSpanMs = Math.Max(halfSpanMs, windowMs + 1);

            long startMs = judgeCenterMs - halfSpanMs;
            long endMs = judgeCenterMs + halfSpanMs;

            // nowMs -> [0..width-1] 위치
            double t = (nowMs - startMs) / (double)(endMs - startMs);
            int pos = (int)Math.Round(t * (width - 1));
            pos = Math.Max(0, Math.Min(width - 1, pos));

            // judge center 위치(항상 중앙에 오게끔 설계)
            int center = width / 2;

            // window 구간을 width로 변환
            int winHalf = (int)Math.Round(windowMs / (double)halfSpanMs * (width / 2.0));
            winHalf = Math.Max(0, Math.Min(center, winHalf));

            int winL = center - winHalf;
            int winR = center + winHalf;

            var chars = new char[width];

            for (int i = 0; i < width; i++)
            {
                bool inWindow = (i >= winL && i <= winR);

                // 기본은 window 밖 '='
                chars[i] = inWindow ? '=' : '=';  // 일단 '='로 깔고
                                                  // window는 '=' 대신 '|' 같은 걸 원하면 여기 변경 가능
                chars[i] = inWindow ? '=' : '=';  // 유지 (요청대로 == 영역을 만들 거라서)

                // window 밖을 더 옅게 보이고 싶으면 '.'로 바꿔도 됨
                // chars[i] = inWindow ? '=' : '-';
                chars[i] = inWindow ? '=' : '=';
            }

            // window를 "==", 바깥을 "===="로 이미 같아서 구분이 안 되니까,
            // 요청한 느낌(== 구간 강조)을 위해 바깥을 '-'로, window를 '='로 추천:
            for (int i = 0; i < width; i++)
            {
                bool inWindow = (i >= winL && i <= winR);
                chars[i] = inWindow ? '=' : '-';
            }

            // center 표시
            chars[center] = '|';

            // 입력 마커 (center와 겹치면 '^'가 이길지 '|'가 이길지 선택)
            chars[pos] = marker;

            return $"curBeat[{new string(chars)}]nextBeat  diff={nowMs - judgeCenterMs}ms  win=±{windowMs}ms";
        }

        // 서버에서 직접 예약하고 싶은 명령 (몬스터 AI 등)
        public void ScheduleServerCommand(long beatIndex, PlayerActionCmd cmd)
        {
            var currBeat = _clock.GetCurrentBeatIndex(_time.NowMs);
            //Console.WriteLine($"[Enqueue] actor={cmd.ActorId} kind={cmd.Kind} reqBeat={cmd.RequestedBeat}");

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


                if (cmd.ActorId >= 0 && cmd.ActorId < 100) Console.WriteLine($"[OnBeat] ActionId:{cmd.ActorId} || Beat : {beatIndex}");

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
