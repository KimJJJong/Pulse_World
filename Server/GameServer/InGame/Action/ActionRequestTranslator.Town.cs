public static partial class ActionRequestTranslator
{
    public static bool TryBuildCmd(
        int actorId,
        CS_TownActionRequest req,
        long executeBeat,
        int judgeDiffMs,
        long serverReceiveMs,
        out PlayerActionCmd cmd,
        out string reason)
    {
        cmd = null!;
        reason = "";

        var kind = (ActionKind)req.ActionKind;

        switch (kind)
        {
            case ActionKind.Move:
                return TryBuildMove(actorId, req, executeBeat, judgeDiffMs, serverReceiveMs, out cmd, out reason);

            case ActionKind.Interact:
                return TryBuildInteract(actorId, req, executeBeat, judgeDiffMs, serverReceiveMs, out cmd, out reason);
            //case ActionKind.Attack:
            //    return TryBuildAttack(actorId, req, executeBeat, judgeDiffMs, serverReceiveMs, out cmd, out reason);

            //case ActionKind.Skill:
            //    return TryBuildSkill(actorId, req, executeBeat, judgeDiffMs, serverReceiveMs, out cmd, out reason);

            case ActionKind.Wait:
                cmd = new PlayerActionCmd
                {
                    ActorId = actorId,
                    Kind = ActionKind.Wait,
                    ExecuteBeat = executeBeat,
                    //JudgeDiffMs = judgeDiffMs,
                    ClientSendTimeMs = req.ClientSendTimeMs,
                    ServerReceiveTimeMs = serverReceiveMs,
                };
                return true;

            default:
                reason = $"unknown kind={req.ActionKind}";
                return false;
        }
    }

    static bool TryBuildMove(int actorId, CS_TownActionRequest req, long executeBeat, int judgeDiffMs, long serverReceiveMs,
        out PlayerActionCmd cmd, out string reason)
    {
        reason = "";
        cmd = new PlayerActionCmd
        {
            ActorId = actorId,
            Kind = ActionKind.Move,
            TargetCell = new GridPos(req.TargetX, req.TargetY),
            ExecuteBeat = executeBeat,
            Rotation = req.Rotation,
            //JudgeDiffMs = judgeDiffMs,
            ClientSendTimeMs = req.ClientSendTimeMs,
            ServerReceiveTimeMs = serverReceiveMs,
        };
        return true;
    }

    static bool TryBuildInteract(int actorId, CS_TownActionRequest req, long executeBeat, int judgeDiffMs, long serverReceiveMs,
        out PlayerActionCmd cmd, out string reason)
    {
        reason = "";



        cmd = new PlayerActionCmd
        {
            ActorId = actorId,
            Kind = ActionKind.Interact,
            TargetCell = new GridPos(req.TargetX, req.TargetY),
            ExecuteBeat = executeBeat,
            Rotation = req.Rotation,
            ClientSendTimeMs = req.ClientSendTimeMs,
            ServerReceiveTimeMs = serverReceiveMs,
        };
        return true;
    }

    //static bool TryBuildAttack(int actorId, CS_TownActionRequest req, long executeBeat, int judgeDiffMs, long serverReceiveMs,
    //    out PlayerActionCmd cmd, out string reason)
    //{
    //    reason = "";

    //    // 예시 정책: Attack은 TargetOid 필수
    //    //if (req.TargetOid <= 0)
    //    //{
    //    //    cmd = null!;
    //    //    reason = "Attack requires TargetOid";
    //    //    return false;
    //    //}

    //    cmd = new PlayerActionCmd
    //    {
    //        ActorId = actorId,
    //        Kind = ActionKind.Attack,
    //        TargetCell = new GridPos(req.TargetX, req.TargetY),
    //        //TargetOid = req.TargetOid,
    //        ExecuteBeat = executeBeat,
    //        //JudgeDiffMs = judgeDiffMs,
    //        ClientSendTimeMs = req.ClientSendTimeMs,
    //        ServerReceiveTimeMs = serverReceiveMs,
    //    };
    //    return true;
    //}

    //static bool TryBuildSkill(int actorId, CS_TownActionRequest req, long executeBeat, int judgeDiffMs, long serverReceiveMs,
    //    out PlayerActionCmd cmd, out string reason)
    //{
    //    reason = "";

    //    //if (req.SkillId <= 0)
    //    //{
    //    //    cmd = null!;
    //    //    reason = "Skill requires SkillId";
    //    //    return false;
    //    //}

    //    // 스킬은 타겟이 셀일 수도 있고, 오브젝트일 수도 있고, 둘 다 없을 수도 있음.
    //    // 여기선 둘 다 실어두고, 실제 해석은 SkillResolver에서.
    //    cmd = new PlayerActionCmd
    //    {
    //        ActorId = actorId,
    //        Kind = ActionKind.Skill,
    //        //SkillId = req.SkillId,
    //        TargetCell = new GridPos(req.TargetX, req.TargetY),
    //        //TargetOid = (req.TargetOid > 0) ? req.TargetOid : null,
    //        //Param0 = req.Param0,
    //        //Param1 = req.Param1,

    //        ExecuteBeat = executeBeat,
    //        //JudgeDiffMs = judgeDiffMs,
    //        ClientSendTimeMs = req.ClientSendTimeMs,
    //        ServerReceiveTimeMs = serverReceiveMs,
    //    };
    //    return true;
    //}
}
