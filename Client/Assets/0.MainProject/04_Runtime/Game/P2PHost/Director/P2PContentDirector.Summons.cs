using System;
using System.Collections.Generic;
using GameServer.InGame.Director.Data;
using UnityEngine;

public sealed partial class P2PContentDirector
{
    private const int DefaultSummonMonsterId = 1027;

    private readonly Dictionary<string, StageSummonPortalRuntime> _summonPortals = new(StringComparer.Ordinal);

    public void SetSummonPortalActive(StageSummonPortalData data)
    {
        data ??= new StageSummonPortalData();

        string portalKey = BuildSummonPortalKey(data);
        if (!data.Active)
        {
            if (_summonPortals.Remove(portalKey) && P2PDebugConfig.TraceContent)
                Debug.Log($"[P2PContentDirector] SummonPortal stop key={portalKey}");
            return;
        }

        if (!_summonPortals.TryGetValue(portalKey, out var portal))
        {
            portal = new StageSummonPortalRuntime();
            _summonPortals[portalKey] = portal;
        }

        int intervalBeats = Math.Max(1, data.IntervalBeats);
        int maxAlive = Math.Max(1, data.MaxAlive);
        int spawnMapY = ResolveMapY(data.SpawnY, data.SpawnZ);
        int[] monsterIds = ParseMonsterIds(data.MonsterIdsCsv);
        long currentBeat = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : 0;

        bool resetTiming = !portal.Active
                           || portal.SpawnGroupId != data.SpawnGroupId
                           || portal.SpawnX != data.SpawnX
                           || portal.SpawnY != spawnMapY
                           || portal.IntervalBeats != intervalBeats;

        portal.PortalKey = portalKey;
        portal.Active = true;
        portal.SpawnGroupId = data.SpawnGroupId;
        portal.MaxAlive = maxAlive;
        portal.IntervalBeats = intervalBeats;
        portal.SpawnX = data.SpawnX;
        portal.SpawnY = spawnMapY;
        portal.MonsterIds = monsterIds;
        portal.PatternKey = data.PatternKey ?? string.Empty;

        if (resetTiming)
            portal.NextSpawnBeat = currentBeat + Math.Max(0, data.InitialDelayBeats);

        if (P2PDebugConfig.TraceContent)
        {
            Debug.Log(
                $"[P2PContentDirector] SummonPortal start key={portalKey} group={portal.SpawnGroupId} " +
                $"pos=({portal.SpawnX},{portal.SpawnY}) maxAlive={portal.MaxAlive} interval={portal.IntervalBeats} nextBeat={portal.NextSpawnBeat}");
        }
    }

    private void ResetSummonPortals()
    {
        _summonPortals.Clear();
    }

    private void TickSummonPortals(long beat)
    {
        if (_summonPortals.Count == 0)
            return;

        foreach (var portal in _summonPortals.Values)
        {
            if (portal == null || !portal.Active || beat < portal.NextSpawnBeat)
                continue;

            if (portal.SpawnGroupId <= 0)
            {
                portal.NextSpawnBeat = beat + portal.IntervalBeats;
                continue;
            }

            if (CountAliveSummonMonsters(portal.SpawnGroupId) >= portal.MaxAlive)
            {
                portal.NextSpawnBeat = beat + 1;
                continue;
            }

            int monsterId = portal.MonsterIds.Length > 0
                ? portal.MonsterIds[_rng.Next(portal.MonsterIds.Length)]
                : DefaultSummonMonsterId;

            SpawnMonster(new SpawnData
            {
                MonsterId = monsterId,
                X = portal.SpawnX,
                Y = 0,
                Z = portal.SpawnY,
                AI = portal.PatternKey,
                Pattern = portal.PatternKey,
                PatternId = portal.PatternKey,
                PatternKey = portal.PatternKey,
                GroupId = portal.SpawnGroupId
            });

            portal.NextSpawnBeat = beat + portal.IntervalBeats;
        }
    }

    private int CountAliveSummonMonsters(int groupId)
    {
        int count = 0;
        foreach (var state in _monsterStates.Values)
        {
            if (state.GroupId != groupId)
                continue;

            if (!state.IsAlive)
                continue;

            if (ClientGameState.Instance != null)
            {
                if (!ClientGameState.Instance.TryGetEntity(state.EntityId, out var entity) || entity.Hp <= 0)
                {
                    state.IsAlive = false;
                    continue;
                }
            }

            count++;
        }

        return count;
    }

    private static string BuildSummonPortalKey(StageSummonPortalData data)
    {
        string key = data.PortalKey?.Trim();
        if (!string.IsNullOrWhiteSpace(key))
            return key;

        return $"{data.SpawnGroupId}:{data.SpawnX}:{ResolveMapY(data.SpawnY, data.SpawnZ)}";
    }

    private static int[] ParseMonsterIds(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return new[] { DefaultSummonMonsterId };

        string[] tokens = csv.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var ids = new List<int>(tokens.Length);
        foreach (string token in tokens)
        {
            if (int.TryParse(token.Trim(), out int id) && id > 0 && !ids.Contains(id))
                ids.Add(id);
        }

        return ids.Count > 0 ? ids.ToArray() : new[] { DefaultSummonMonsterId };
    }

    private sealed class StageSummonPortalRuntime
    {
        public string PortalKey = string.Empty;
        public bool Active;
        public int SpawnGroupId;
        public int MaxAlive = 2;
        public int IntervalBeats = 8;
        public long NextSpawnBeat;
        public int SpawnX;
        public int SpawnY;
        public int[] MonsterIds = Array.Empty<int>();
        public string PatternKey = string.Empty;
    }
}
