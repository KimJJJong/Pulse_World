using System.Collections.Generic;

namespace GameServer.Content.Map.Interface;
public interface IGameWorld
{
    GridPos GetActorPosition(int actorId);
    bool TryGetActorPosition(int actorId, out GridPos pos);
    bool ContainsEntity(int actorId);

    bool TryMove(int actorId, GridPos target);
    bool TryUseSkill(int actorId, string skillId ,int targetX, int targetY, List<HpUpdate> hpUpdate);
    bool TryUseSkillArea(int actorId, string skillId, IReadOnlyList<GridPos> cells, List<HpUpdate> hpUpdates);

    bool TryUseAttack(int actorId, /*나중에 직업별 일반공격 셋팅?*/int tartX, int tartY, List<HpUpdate> hpUpdates);

}