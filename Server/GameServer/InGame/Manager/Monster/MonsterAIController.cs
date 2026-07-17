using GameServer.InGame.Manager.Entity;
using System;
using System.Collections.Generic;

public sealed class MonsterAIController
{
    private readonly PatternRunner _runner;

    // monsterId -> type
    private readonly Dictionary<int, string> _monsterTypes = new();

    public MonsterAIController(PatternRunner runner)
    {
        _runner = runner;
    }

    /// <summary>
    /// monsterType을 반드시 지정해서 등록
    /// </summary>
    public void RegisterMonster(MapEntity monster, string monsterType)
    {
        _monsterTypes[monster.Id] = monsterType;
        _runner.InitMonster(monster.Id);
        Console.WriteLine($"[ RegisterMosnter ] monsterType : {monsterType} || monster.Id :{monster.Id} || Position :{monster.Position}");
    }


    public void UnregisterMonster(int monsterId)
    {
        _monsterTypes.Remove(monsterId);
        _runner.RemoveMonster(monsterId);
    }

    public void UpdateAI(long beatIndex, IEnumerable<MapEntity> monsters, IList<MapEntity> players)
    {
        if (players.Count == 0) return;

        foreach (var m in monsters)
        {
            if (!m.IsAlive) continue;
            if (!_monsterTypes.TryGetValue(m.Id, out var type)) continue;

            _runner.Run(beatIndex, m, type, players);
        }
    }
}
