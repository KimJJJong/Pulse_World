using System.Collections.Generic;

public sealed class MonsterPatternRuntimeState
{
    public string PhaseId = "P1";

    // selectorId -> nextAvailableBeat (쿨다운 관리)
    public Dictionary<string, long> Cooldowns = new();

    // (attackId, executeBeat) -> 고정 areaCells (텔레그래프와 공격 동일 보장)
    public Dictionary<(string actionKey, long executeBeat), List<GridPos>> FrozenAreas = new();
}
