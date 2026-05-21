using System.Collections.Generic;

public sealed class FrozenAttackRegistry
{
    public sealed class FrozenAttack
    {
        public string SkillId = "";
        public List<GridPos> Cells = new();
        public int? CustomDamage = null; // NewSkill 시스템용 직접 데미지
        public int StunDurationTicks = 0;
        public int KnockbackDistance = 0;

        public bool HitPlayers = true;
        public bool HitMonsters = false;
    }

    // (actorId, beat) -> frozen
    private readonly Dictionary<(int actorId, long beat), FrozenAttack> _map = new();
    private readonly List<(int actorId, long beat)> _removeBuffer = new();

    public void PutRaw(int actorId, long beat, int damage, List<GridPos> cells, int stunTicks = 0, int knockback = 0, bool hitPlayers = true, bool hitMonsters = false)
    {
        _map[(actorId, beat)] = new FrozenAttack
        {
            SkillId = string.Empty, // SkillId 없음
            Cells = cells ?? new List<GridPos>(),
            CustomDamage = damage,
            StunDurationTicks = stunTicks,
            KnockbackDistance = knockback,
            HitPlayers = hitPlayers,
            HitMonsters = hitMonsters
        };
    }

    public bool TryPop(int actorId, long beat, out FrozenAttack frozen)
    {
        var key = (actorId, beat);
        if (_map.TryGetValue(key, out frozen))
        {
            _map.Remove(key);
            return true;
        }
        return false;
    }

    public void DropBefore(long beat)
    {
        _removeBuffer.Clear();
        foreach (var k in _map.Keys)
            if (k.beat < beat) _removeBuffer.Add(k);

        foreach (var k in _removeBuffer)
            _map.Remove(k);
    }
    public void RemoveByActor(int actorId)
    {
        if (_map.Count == 0) return;

        _removeBuffer.Clear();
        foreach (var k in _map.Keys)
            if (k.actorId == actorId) _removeBuffer.Add(k);

        foreach (var k in _removeBuffer)
            _map.Remove(k);
    }

}
