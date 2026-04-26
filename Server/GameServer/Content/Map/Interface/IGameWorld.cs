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

    /// <summary>Dash: Distance 칸 이동, 도중 벽이면 바로 앞까지만.</summary>
    bool TryDash(int actorId, int dirX, int dirY, int distance, out GridPos landedPos);

    /// <summary>
    /// Dash의 비파괴 미리보기. 실제 엔티티 이동 없이 시작 위치 기준 착지 지점을 계산한다.
    /// </summary>
    bool TryPreviewDash(GridPos from, int dirX, int dirY, int distance, out GridPos landedPos);

    /// <summary>Blink: 목표지점이 Walkable이면 순간이동, 아니면 실패.</summary>
    bool TryBlink(int actorId, int dirX, int dirY, int distance, out GridPos landedPos);

    /// <summary>
    /// Blink의 비파괴 미리보기. 실제 엔티티 이동 없이 시작 위치 기준 목표 지점을 계산한다.
    /// </summary>
    bool TryPreviewBlink(GridPos from, int dirX, int dirY, int distance, out GridPos landedPos);
    bool TryUseSkill(int actorId, string skillId ,int targetX, int targetY, List<HpUpdate> hpUpdate);
    bool TryUseSkillArea(int actorId, string skillId, IReadOnlyList<GridPos> cells, List<HpUpdate> hpUpdates, bool? hitPlayers = null, bool? hitMonsters = null);

    bool TryUseAttack(int actorId, /*나중에 직업별 일반공격 셋팅?*/int tartX, int tartY, List<HpUpdate> hpUpdates);
    
    // For Pattern Custom Damage
    bool TryUseCustomSkill(int actorId, long currentTick, FrozenAttackRegistry.FrozenAttack frozen, List<HpUpdate> hpUpdates);
}
