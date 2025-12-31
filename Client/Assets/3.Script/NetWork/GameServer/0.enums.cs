public enum ActionKind : byte
{
    None = 0,

    Move = 1,  // 칸 이동 (TargetCell 필요)
    Attack = 2,  // 기본 공격 (Target: Cell or Unit)
    Skill = 3,  // 스킬 (SkillId + TargetType)
    Wait = 4,  // 아무 것도 안 함(리듬 입력만 소비)

    Interact = 5, // 오브젝트 상호작용
    Emote = 6, // 디버그/감정표현 등 (게임로직 영향 없게)
}

public enum EntityType : byte
{
    Player = 1,
    Monster = 2,
    Object = 3,
}
