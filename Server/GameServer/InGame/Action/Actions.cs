/// <summary>
/// 서버가 내부적으로 관리하는 "입력 요청" 단위
/// </summary>
public sealed class PendingAction
{
    public int ActorId;
    public ActionKind Kind;
    public int TargetX;
    public int TargetY;
    public long ClientSendTimeMs;
    public long ServerReceiveTimeMs;
    public long BeatIndex; // 이 행동이 실행될 Beat (서버가 판정해서 셋팅)
}


/// <summary>
/// 한 플레이어(또는 AI)가 Beat 단위로 실행하고자 하는 액션 명령.
/// 서버 입장에서는 "이 Beat에 이 행동을 실행해라"라는 스케줄 목적.
/// </summary>
public sealed class PlayerActionCmd
{
    /// <summary>행동의 주체가 되는 Actor ID (플레이어, 몬스터 등)</summary>
    public int ActorId { get; init; }

    /// <summary>
    /// 행동 종류 (이동, 스킬, 대기 등).
    /// set 허용 - ResolveSkillId 등에서 Kind 재분류가 필요한 경우 사용.
    /// </summary>
    public ActionKind Kind { get; set; }

    /// <summary>
    /// Move/Skill 등의 타겟 위치 (그리드 좌표).
    /// Move면 이동하려는 칸, Skill이면 스킬 중심 좌표 등으로 해석.
    /// </summary>
    public GridPos TargetCell { get; init; }

    public string SkillId { get; set; }
    public int SlotIndex { get; init; }

    public long ExecuteBeat { get; set; }

    public long ClientSendTimeMs { get; init; }

    public float Rotation { get; init; }
    public long ServerReceiveTimeMs { get; set; }

    public override string ToString()
        => $"Actor={ActorId}, Kind={Kind}, Target={TargetCell}, Beat={ExecuteBeat}";
}
