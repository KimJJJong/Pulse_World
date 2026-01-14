using UnityEngine;

// using Contracts.Packet; // CS_ActionRequest

public class RhythmInputController : MonoBehaviour
{
    public static RhythmInputController Instance { get; private set; }
    ClientGameState GS => ClientGameState.Instance;
    RhythmClient Rhythm => RhythmClient.Instance;

    [SerializeField] float inputCooldownMs = 80f; // 기존 스팸 방지(기존 모드에서만 의미 있음)
    [SerializeField] bool holdAutoInput = false;  //  토글: true면 꾹누름 자동 입력(Beat midpoint 1회)
    [SerializeField] float rotateAngle = 90f; // 격자 기반이면 90 권장
    [SerializeField] public GameObject targetObject = null;
    long _lastSendLocalMs = 0;

    // --- Hold Auto 상태 ---
    bool _holdActive = false;
    Vector2Int _holdDir = Vector2Int.zero;
    ActionKind _holdKind = ActionKind.Move;

    // "이번 beatIndex에서 이미 발사했는가" 체크용
    long _lastFiredBeatIndex = long.MinValue;

    private void Awake()
    {

        Instance = this;
    }

    void Update()
    {
        if (!IsReady())
            return;

        long nowLocalMs = LocalNowMs();

        if (Input.GetKeyDown(KeyCode.Q))
            Rotate(-rotateAngle);

        if (Input.GetKeyDown(KeyCode.E))
            Rotate(+rotateAngle);

        //  자동 입력 모드
        if (holdAutoInput)
        {
            if (!TryUpdateHoldState(out _holdDir, out _holdKind))
            {
                // 아무 키도 안 누르는 상태면 hold 종료
                _holdActive = false;
                return;
            }

            _holdActive = true;

            if (!GS.TryGetMyEntity(out var me))
                return;

            int targetX = me.X + RotateDirByTarget(_holdDir).x;
            int targetY = me.Y + RotateDirByTarget(_holdDir).y;

            long serverNowMs = Rhythm.GetCurrentServerTimeMs();

            // 캘리브 모드면 기존과 동일하게 캘리브 우선
            if (TrySendCalib(serverNowMs))
                return;

            //  midpoint에서 1회 발사
            TryFireAtBeatMidpoint(_holdKind, targetX, targetY, serverNowMs);

            return;
        }

        // 기존 입력 모드(KeyDown 1회)
        if (!PassCooldown(nowLocalMs))
            return;

        if (!TryGetInput(out var dir, out var kind))
            return;

        if (!GS.TryGetMyEntity(out var me2))
            return;

        var rdir = RotateDirByTarget(dir);
        int tx = me2.X + dir.x;
        int ty = me2.Y + dir.y;

        long serverNow = Rhythm.GetCurrentServerTimeMs();

        BeatDebugUI_TMP.Instance?.MarkHitNow();

        if (TrySendCalib(serverNow))
        {
            _lastSendLocalMs = nowLocalMs;
            return;
        }

        SendAction(kind, tx, ty, serverNow);
        _lastSendLocalMs = nowLocalMs;
    }

    bool IsReady()
    {
        if (GS == null || Rhythm == null)
        {
            Debug.LogWarning($"GS:{GS} Rhythm:{Rhythm} Net:{NetworkManager.Instance}");
            return false;
        }
        return true;
    }

    static long LocalNowMs()
        => (long)(Time.realtimeSinceStartupAsDouble * 1000.0);

    bool PassCooldown(long nowLocalMs)
        => (nowLocalMs - _lastSendLocalMs) >= inputCooldownMs;

    void Rotate(float angle)
    {
        if (targetObject == null) return;

        targetObject.transform.Rotate(Vector3.up, angle);

        // 격자(90도)면 오차 누적 방지용 스냅 추천
        var e = targetObject.transform.eulerAngles;
        e.y = Mathf.Round(e.y / rotateAngle) * rotateAngle;
        targetObject.transform.eulerAngles = e;
    }
    Vector2Int RotateDirByTarget(Vector2Int dir)
    {
        if (targetObject == null)
            return dir;

        // grid(x,y) -> world(x,z)
        Vector3 w = new Vector3(dir.x, 0f, dir.y);

        // 타겟 회전 적용
        Vector3 rw = targetObject.transform.rotation * w;

        // world(x,z) -> grid(x,y) (격자이므로 반올림)
        int rx = Mathf.RoundToInt(rw.x);
        int ry = Mathf.RoundToInt(rw.z);

        // 혹시 대각이 나오는 경우 방지(안전장치)
        if (Mathf.Abs(rx) > Mathf.Abs(ry)) ry = 0;
        else rx = 0;

        // 값 정규화 ([-1,0,1])
        rx = Mathf.Clamp(rx, -1, 1);
        ry = Mathf.Clamp(ry, -1, 1);

        return new Vector2Int(rx, ry);
    }


    /// <summary>
    /// (기존) KeyDown 기반 1회 입력
    /// </summary>
    bool TryGetInput(out Vector2Int dir, out ActionKind kind)
    {
        dir = Vector2Int.zero;
        kind = ActionKind.Move;

        if (Input.GetKeyDown(KeyCode.W)) { dir = Vector2Int.up; kind = ActionKind.Move; return true; }
        if (Input.GetKeyDown(KeyCode.S)) { dir = Vector2Int.down; kind = ActionKind.Move; return true; }
        if (Input.GetKeyDown(KeyCode.A)) { dir = Vector2Int.left; kind = ActionKind.Move; return true; }
        if (Input.GetKeyDown(KeyCode.D)) { dir = Vector2Int.right; kind = ActionKind.Move; return true; }

        if (Input.GetKeyDown(KeyCode.UpArrow)) { dir = Vector2Int.up; kind = ActionKind.Attack; return true; }
        if (Input.GetKeyDown(KeyCode.DownArrow)) { dir = Vector2Int.down; kind = ActionKind.Attack; return true; }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) { dir = Vector2Int.left; kind = ActionKind.Attack; return true; }
        if (Input.GetKeyDown(KeyCode.RightArrow)) { dir = Vector2Int.right; kind = ActionKind.Attack; return true; }

        return false;
    }

    /// <summary>
    /// ✅ (새) Hold 상태 업데이트: GetKey 기반
    /// - WASD: Move
    /// - Arrow: Attack
    /// - 동시에 눌리면 "기존과 동일하게" 위에서 먼저 매칭되는 1개만
    /// </summary>
    bool TryUpdateHoldState(out Vector2Int dir, out ActionKind kind)
    {
        dir = Vector2Int.zero;
        kind = ActionKind.Move;

        // WASD -> Move (우선)
        if (Input.GetKey(KeyCode.W)) { dir = Vector2Int.up; kind = ActionKind.Move; return true; }
        if (Input.GetKey(KeyCode.S)) { dir = Vector2Int.down; kind = ActionKind.Move; return true; }
        if (Input.GetKey(KeyCode.A)) { dir = Vector2Int.left; kind = ActionKind.Move; return true; }
        if (Input.GetKey(KeyCode.D)) { dir = Vector2Int.right; kind = ActionKind.Move; return true; }

        // Arrow -> Attack
        if (Input.GetKey(KeyCode.UpArrow)) { dir = Vector2Int.up; kind = ActionKind.Attack; return true; }
        if (Input.GetKey(KeyCode.DownArrow)) { dir = Vector2Int.down; kind = ActionKind.Attack; return true; }
        if (Input.GetKey(KeyCode.LeftArrow)) { dir = Vector2Int.left; kind = ActionKind.Attack; return true; }
        if (Input.GetKey(KeyCode.RightArrow)) { dir = Vector2Int.right; kind = ActionKind.Attack; return true; }

        return false;
    }

    /// <summary>
    ///  "한 beatIndex당 1회" + "index와 index 사이 midpoint에서 1번" 발사
    /// </summary>
    void TryFireAtBeatMidpoint(ActionKind kind, int targetX, int targetY, long serverNowMs)
    {
        if (!_holdActive)
            return;

        long beat = Rhythm.GetCurrentBeatIndex();

        // 이미 이번 beat에서 발사했으면 종료
        if (_lastFiredBeatIndex == beat)
            return;

        // ---- midpoint 계산 ----
        // 아래 함수명이 너 프로젝트에 없으면 "beatIndex -> beatTimeMs" 반환 함수로 교체해줘.
        long t0 = Rhythm.GetBeatTimeMs(beat);
        long t1 = Rhythm.GetBeatTimeMs(beat + 1);

        long mid = (t0 + t1) / 2;

        // midpoint를 지난 "그 프레임"에 1번만 발사
        if (serverNowMs >= mid)
        {
            BeatDebugUI_TMP.Instance?.MarkHitNow();

            SendAction(kind, targetX, targetY, serverNowMs);
            _lastFiredBeatIndex = beat;
        }
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
        NetworkManager.Instance.Send(pkt.Write());
        return true;
    }

    void SendAction(ActionKind kind, int targetX, int targetY, long serverNowMs)
    {
        if (NetworkManager.Instance == null) { Debug.LogError("NetWorkManager.Instance NULL"); return; }

        CS_TownActionRequest pkt = new CS_TownActionRequest
        {
            ActorId = GS.MyActorId,
            ActionKind = (int)kind,
            TargetX = targetX,
            TargetY = targetY,
            ClientSendTimeMs = serverNowMs,
        };

        NetworkManager.Instance.Send(pkt.Write());
    }
}
