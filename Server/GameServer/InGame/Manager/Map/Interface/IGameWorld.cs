using System.Collections.Generic;

namespace GameServer.InGame.Manager.Map.Interface;
public interface IGameWorld
{
    GridPos GetActorPosition(int actorId);
    bool TryGetActorPosition(int actorId, out GridPos pos);
    bool ContainsEntity(int actorId);

    bool TryMove(int actorId, GridPos target);
    bool TryUseSkill(int actorId, string skillId ,int targetX, int targetY);
    bool TryUseSkillArea(int actorId, string skillId, IReadOnlyList<GridPos> cells);

}