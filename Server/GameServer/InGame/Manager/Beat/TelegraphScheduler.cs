using System;
using System.Collections.Generic;

namespace GameServer.InGame.Manager.Beat;
    public sealed class TelegraphScheduler
    {
        private readonly IGameBroadcaster _broadcaster;

        // beatIndex -> entries
        private readonly Dictionary<long, List<SC_BeatTelegraphs.Telegraphs>> _scheduled = new();

        public TelegraphScheduler(IGameBroadcaster broadcaster)
        {
            _broadcaster = broadcaster;
        }

        public void Schedule(long beatIndex, SC_BeatTelegraphs.Telegraphs telegraph)
        {
            if (!_scheduled.TryGetValue(beatIndex, out var list))
                _scheduled[beatIndex] = list = new List<SC_BeatTelegraphs.Telegraphs>(8);

            list.Add(telegraph);
        }

    public void OnBeat(long beatIndex)
    {
        if (!_scheduled.TryGetValue(beatIndex, out var list) || list.Count == 0)
            return;

        Console.WriteLine($"[TelegraphScheduler.OnBeat] Beat={beatIndex} Count={list.Count}");



        _scheduled.Remove(beatIndex);

        _broadcaster.Broadcast(new SC_BeatTelegraphs
        {
            BeatIndex = beatIndex,
            telegraphss = list
        });

        // 너무 쌓이지 않게 정리
        DropBefore(beatIndex - 16);
    }


    public void DropBefore(long beatIndex)
        {
            if (_scheduled.Count == 0) return;

            // 키 스캔(규모 커지면 SortedDictionary/MinHeap로 최적화 가능)
            var removeKeys = new List<long>();
            foreach (var k in _scheduled.Keys)
                if (k < beatIndex) removeKeys.Add(k);

            foreach (var k in removeKeys)
                _scheduled.Remove(k);
        }
    }