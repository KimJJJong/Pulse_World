using UnityEngine;

// using Contracts.Packet; // CS_ActionRequest

public class RhythmInputController : MonoBehaviour
{
    ClientGameState GS => ClientGameState.Instance;
    RhythmClient Rhythm => RhythmClient.Instance;

    [SerializeField] float inputCooldownMs = 80f; // 너무 자주 스팸 방지용

    long _lastSendLocalMs = 0;

    void Update()
    {
        if (!IsReady())
            return;

        long nowLocalMs = LocalNowMs();
        if (!PassCooldown(nowLocalMs))
            return;

        if (!TryGetInput(out var dir, out var kind))
            return;

        if (!GS.TryGetMyEntity(out var me))
            return;

        int targetX = me.X + dir.x;
        int targetY = me.Y + dir.y;

        long serverNowMs = Rhythm.GetCurrentServerTimeMs();

        BeatDebugUI_TMP.Instance?.MarkHitNow();

        // 캘리브 모드면 이동/공격 대신 캘리브 패킷 우선
        if (TrySendCalib(serverNowMs))
        {
            _lastSendLocalMs = nowLocalMs;
            return;
        }

        SendAction(kind, targetX, targetY, serverNowMs);

        _lastSendLocalMs = nowLocalMs;
    }

    bool IsReady()
    {
        if (GS == null || Rhythm == null)
        {
            Debug.LogWarning($"GS:{GS} Rhythm:{Rhythm}");
            return false;
        }

        // 필요하면 활성화
        // if (GS.MyActorId == 0) return false;

        return true;
    }

    static long LocalNowMs()
        => (long)(Time.realtimeSinceStartupAsDouble * 1000.0);

    bool PassCooldown(long nowLocalMs)
        => (nowLocalMs - _lastSendLocalMs) >= inputCooldownMs;

    /// <summary>
    /// 입력을 읽고, 방향(dir)과 액션 종류(kind)를 결정.
    /// - WASD: Move
    /// - Arrow: Attack
    /// </summary>
    bool TryGetInput(out Vector2Int dir, out ActionKind kind)
    {
        dir = Vector2Int.zero;
        kind = ActionKind.Move;

        // 우선순위: 입력이 동시에 들어오면 첫 매칭만 처리(기존과 동일)
        // WASD -> Move
        if (Input.GetKeyDown(KeyCode.W)) { dir = Vector2Int.up; kind = ActionKind.Move; return true; }
        if (Input.GetKeyDown(KeyCode.S)) { dir = Vector2Int.down; kind = ActionKind.Move; return true; }
        if (Input.GetKeyDown(KeyCode.A)) { dir = Vector2Int.left; kind = ActionKind.Move; return true; }
        if (Input.GetKeyDown(KeyCode.D)) { dir = Vector2Int.right; kind = ActionKind.Move; return true; }

        // Arrow -> Attack
        if (Input.GetKeyDown(KeyCode.UpArrow)) { dir = Vector2Int.up; kind = ActionKind.Attack; return true; }
        if (Input.GetKeyDown(KeyCode.DownArrow)) { dir = Vector2Int.down; kind = ActionKind.Attack; return true; }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) { dir = Vector2Int.left; kind = ActionKind.Attack; return true; }
        if (Input.GetKeyDown(KeyCode.RightArrow)) { dir = Vector2Int.right; kind = ActionKind.Attack; return true; }

        return false;
    }

    bool TrySendCalib(long serverNowMs)
    {
        var calib = AudioOffsetAutoCalibrator.Instance;
        if (calib == null || !calib.Enabled)
            return false;

        long beatIndex = Rhythm.GetCurrentBeatIndex();

        CS_CalibHit pkt = new CS_CalibHit
        {
            ClientSendTimeMs = serverNowMs,
            BeatIndex = beatIndex,
        };
        NetWorkManager.Instance.Send(pkt.Write());
        return true;
    }

    void SendAction(ActionKind kind, int targetX, int targetY, long serverNowMs)
    {
        CS_ActionRequest pkt = new CS_ActionRequest
        {
            ActorId = GS.MyActorId,
            ActionKind = (int)kind,
            TargetX = targetX,
            TargetY = targetY,
            ClientSendTimeMs = serverNowMs,
        };

        NetWorkManager.Instance.Send(pkt.Write());
    }
}
