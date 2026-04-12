using System.Collections.Generic;
using GameServer.InGame.Manager.Entity;

namespace GameServer.Content.Map.Interface;
public interface IGameWorld
{
    GridPos GetActorPosition(int actorId);
    bool TryGetActorPosition(int actorId, out GridPos pos);
    bool ContainsEntity(int actorId);
    bool TryGetEntity(int entityId, out MapEntity entity);

    bool TryMove(int actorId, GridPos target);
    bool TryUseSkill(int actorId, string skillId ,int targetX, int targetY, List<HpUpdate> hpUpdate);
    bool TryUseSkillArea(int actorId, string skillId, IReadOnlyList<GridPos> cells, List<HpUpdate> hpUpdates, bool? hitPlayers = null, bool? hitMonsters = null);

    bool TryUseAttack(int actorId, /*나중에 직업별 일반공격 셋팅?*/int tartX, int tartY, List<HpUpdate> hpUpdates);
    
    // For Pattern Custom Damage
    bool TryUseCustomSkill(int actorId, long currentTick, FrozenAttackRegistry.FrozenAttack frozen, List<HpUpdate> hpUpdates);
}