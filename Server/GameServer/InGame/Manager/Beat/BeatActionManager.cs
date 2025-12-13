using GameServer.InGame.Manager.Map.Interface;
using GameServer.InGame.System.Rhythm;
using System;
using System.Collections.Generic;

// 패킷 네임스페이스는 너 프로젝트에 맞게 수정
//using Contracts.Packet; // CS_ActionRequest, SC_BeatActions, BeatActionResult 등이 여기 있다고 가정

namespace GameServer.InGame.Manager.Beat
{
    public sealed class BeatActionManager
    {
        private readonly IServerTime _time;
        private readonly IGameBroadcaster _broadcaster;
        private readonly IBeatClock _clock;
        private readonly IGameWorld _world;

        private readonly BeatScheduler _scheduler = new();

        private readonly double _actionWindowMs;
        private readonly int _maxBeatLookAhead;

        public BeatActionManager(
            IServerTime time,
            IGameBroadcaster broadcaster,
            IBeatClock clock,
            IGameWorld world,
            double actionWindowMs,
            int maxBeatLookAhead)
        {
            _time = time;
            _broadcaster = broadcaster;
            _clock = clock;
            _world = world;
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
                Console.WriteLine($"diff > _actionWindowMs :{diff} > {_actionWindowMs}");
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
            cmd.RequestedBeat = beatIndex;
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
            var used = new HashSet<int>(); //  Actor당 1 action/beat

            foreach (var cmd in cmds)
            {
                if (!used.Add(cmd.ActorId))
                    continue;

                if (!_world.TryGetActorPosition(cmd.ActorId, out var fromPos))
                {
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

                switch (cmd.Kind)
                {
                    case ActionKind.Move:
                        toPos = cmd.TargetCell;
                        accepted = _world.TryMove(cmd.ActorId, toPos);
                        break;

                    case ActionKind.Skill:
                        accepted = _world.TryUseSkill(cmd.ActorId, cmd.TargetCell.X, cmd.TargetCell.Y);
                        toPos = fromPos;
                        break;

                    case ActionKind.Wait:
                    default:
                        accepted = true;
                        toPos = fromPos;
                        break;
                }

                results.Add(new SC_BeatActions.BeatActionResult
                {
                    ActorId = cmd.ActorId,
                    ActionKind = (int)cmd.Kind,
                    FromX = fromPos.X,
                    FromY = fromPos.Y,
                    ToX = toPos.X,
                    ToY = toPos.Y,
                    Accepted = accepted
                });
            }

            if (results.Count == 0) return;

            _broadcaster.Broadcast(new SC_BeatActions
            {
                BeatIndex = beatIndex,
                beatActionResults = results
            });

            _scheduler.DropBefore(beatIndex - 4);
        }
    }
}
