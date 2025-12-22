

using System.Collections.Generic;

namespace GameServer.InGame.Manager.Beat
{
    public sealed class BeatScheduler
    {
        // beatIndex -> (actorId -> cmd)
        private readonly Dictionary<long, Dictionary<int, PlayerActionCmd>> _byBeat = new();

        /// <summary>
        /// 예약: (RequestedBeat, ActorId) 키로 저장.
        /// 이미 있으면 "마지막 입력으로 교체"한다.
        /// </summary>
        public void Enqueue(PlayerActionCmd cmd)
        {
            long beat = cmd.RequestedBeat;
            int actorId = cmd.ActorId;

            if (!_byBeat.TryGetValue(beat, out var perActor))
            {
                perActor = new Dictionary<int, PlayerActionCmd>(capacity: 8);
                _byBeat[beat] = perActor;
            }

            //  같은 beat에 같은 actor가 이미 있으면 덮어씀(마지막 입력 우선)
            perActor[actorId] = cmd;
        }

        /// <summary>
        /// 해당 beat에 예약된 액션들을 모두 반환하고, 내부에서 제거한다.
        /// </summary>
        public List<PlayerActionCmd> PopActions(long beatIndex)
        {
            if (!_byBeat.TryGetValue(beatIndex, out var perActor) || perActor.Count == 0)
                return new List<PlayerActionCmd>(0);

            var list = new List<PlayerActionCmd>(perActor.Count);

            foreach (var kv in perActor)
                list.Add(kv.Value);

            _byBeat.Remove(beatIndex);
            return list;
        }

        /// <summary>
        /// 과거 beat 데이터 정리(옵션).
        /// 룸이 오래 돌면 메모리 보호용으로 호출해도 좋다.
        /// </summary>
        public void DropBefore(long beatExclusive)
        {
            if (_byBeat.Count == 0) return;

            // Dictionary 순회 중 삭제 방지
            var toRemove = new List<long>();
            foreach (var kv in _byBeat)
            {
                if (kv.Key < beatExclusive)
                    toRemove.Add(kv.Key);
            }
            foreach (var b in toRemove)
                _byBeat.Remove(b);
        }


        public void RemoveByActor(int actorId)
        {
            if (_byBeat.Count == 0) return;

            var emptyBeats = new List<long>();

            foreach (var kv in _byBeat)
            {
                if (kv.Value.Remove(actorId) && kv.Value.Count == 0)
                    emptyBeats.Add(kv.Key);
            }

            foreach (var b in emptyBeats)
                _byBeat.Remove(b);
        }



        /// <summary>디버그용: 특정 beat에 예약된 actor 수</summary>
        public int Count(long beatIndex)
            => _byBeat.TryGetValue(beatIndex, out var perActor) ? perActor.Count : 0;
    }
}
