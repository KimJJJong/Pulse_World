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
    public long JudgedBeat {  get; set; }
    public int JudgeDiffMs { get; set; }


    /// <summary>행동의 주체가 되는 Actor ID (플레이어, 몬스터 등)</summary>
    public int ActorId { get; init; }

    /// <summary>행동 종류 (이동, 스킬, 대기 등)</summary>
    public ActionKind Kind { get; init; }

    /// <summary>
    /// Move/Skill 등의 타겟 위치 (그리드 좌표).
    /// Move면 이동하려는 칸, Skill이면 스킬 중심 좌표 등으로 해석.
    /// </summary>
    public GridPos TargetCell { get; init; }
    /// <summary>
    /// 
    /// </summary>
    public string SkillId { get; init; }


    /// <summary>
    /// 이 액션이 실행될 Beat 번호.
    /// - 클라가 직접 넣어도 되지만,
    ///   보통 서버가 판정 (TryRegisterAction) 후 세팅하는 값을 기준으로 사용.
    /// </summary>
    public long ExecuteBeat { get; set; }

    /// <summary>
    /// 클라이언트 기준 이 명령을 보낸 시각 (ms).
    /// RTT 계산, 판정 보정 등에 사용 가능.
    /// </summary>
    public long ClientSendTimeMs { get; init; }

    /// <summary>
    /// 서버 기준 이 명령을 수신한 시각 (ms).
    /// Beat 판정, ActionWindow 체크 등에 사용.
    /// </summary>
    public long ServerReceiveTimeMs { get; set; }

    public override string ToString()
        => $"Actor={ActorId}, Kind={Kind}, Target={TargetCell}, Beat={ExecuteBeat}";
}
