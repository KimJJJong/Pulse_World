using System.Collections.Generic;
using GameServer.InGame.Manager.Entity;

namespace GameServer.Content.Map.Interface;
public interface IGameWorld
{
    GridPos GetActorPosition(int actorId);
    bool TryGetActorPosition(int actorId, out GridPos pos);
    bool ContainsEntity(int actorId);
    bool TryGetEntity(int entityId, out MapEntity entity);
    IEnumerable<MapEntity> GetEntitiesAt(GridPos pos);

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

    // SkillRunner가 확정한 셀/데미지 결과만 월드에 적용한다.
    bool TryUseCustomSkill(int actorId, long currentTick, FrozenAttackRegistry.FrozenAttack frozen, List<HpUpdate> hpUpdates);
}
