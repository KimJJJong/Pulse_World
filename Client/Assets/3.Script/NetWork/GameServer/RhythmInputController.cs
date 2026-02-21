using UnityEngine;

// using Contracts.Packet; // CS_ActionRequest

public class RhythmInputController : MonoBehaviour
{
    private static RhythmInputController _instance;
    public static RhythmInputController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<RhythmInputController>();
                if (_instance == null)
                {
                    var go = new GameObject("RhythmInputController");
                    _instance = go.AddComponent<RhythmInputController>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    ClientGameState GS => ClientGameState.Instance;
    RhythmClient Rhythm => RhythmClient.Instance;

    public enum InputChannel
    {
        Town,
        Game
    }

    [Header("Route")]
    [SerializeField] InputChannel channel = InputChannel.Town; // 인스펙터에서 기본값
    [SerializeField] bool allowRuntimeToggle = true;
    [SerializeField] KeyCode toggleKey = KeyCode.F1;

    [Header("Input")]
    [SerializeField] float inputCooldownMs = 80f;
    [SerializeField] public bool holdAutoInput = false;
    [SerializeField] float rotateAngle = 90f;
    [SerializeField] public GameObject targetObject = null;

    // UI blocking flag
    public bool IsInputBlocked { get; set; } = false;

    long _lastSendLocalMs = 0;

    // --- Hold Auto 상태 ---
    bool _holdActive = false;
    Vector2Int _holdDir = Vector2Int.zero;
    ActionKind _holdKind = ActionKind.Move;

    // "이번 beatIndex에서 이미 발사했는가" 체크용
    long _lastFiredBeatIndex = long.MinValue;

    // Client-Side Prediction: 공격/스킬 비트당 1회 제한
    long _lastAttackPredictionBeat = long.MinValue;

    //private void Awake()
    //{
    //    Instance = this;
    //}

    void Update()
    {
        if (!IsReady())
            return;

        if (IsInputBlocked)
            return;

        if (allowRuntimeToggle && Input.GetKeyDown(toggleKey))
        {
            channel = (channel == InputChannel.Town) ? InputChannel.Game : InputChannel.Town;
            Debug.Log($"[RhythmInput] Channel switched => {channel}");
        }

        long nowLocalMs = LocalNowMs();

        if (Input.GetKeyDown(KeyCode.Q))
            Rotate(-rotateAngle);

        if (Input.GetKeyDown(KeyCode.E))
            Rotate(+rotateAngle);

        // 자동 입력 모드 (holdAutoInput)
        if (holdAutoInput)
        {
            if (!TryUpdateHoldState(out _holdDir, out _holdKind))
            {
                _holdActive = false;
                return;
            }

            _holdActive = true;

            if (!GS.TryGetMyEntity(out var me))
                return;

            var rdir = RotateDirByTarget(_holdDir);
            int targetX = me.X + rdir.x;
            int targetY = me.Y + rdir.y;

            long serverNowMs = Rhythm.GetCurrentServerTimeMs();

            // 캘리브 모드면 기존과 동일하게 캘리브 우선
            if (TrySendCalib(serverNowMs))
                return;

            // midpoint에서 1회 발사
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

        // ✅ 버그 수정: 회전 적용된 rdir를 써야 함
        var rdir2 = RotateDirByTarget(dir);
        int tx = me2.X + rdir2.x;
        int ty = me2.Y + rdir2.y;

        long serverNow = Rhythm.GetCurrentServerTimeMs();

        BeatDebugUI_TMP.Instance?.MarkHitNow();

        if (TrySendCalib(serverNow))
        {
            _lastSendLocalMs = nowLocalMs;
            return;
        }

        // [Debug] Input Sync Check
        long curBeat = Rhythm.GetCurrentBeatIndex();
        double progress = Rhythm.GetCurrentBeatProgress01();
        //Debug.Log($"[ClientInput] ServerTime={serverNow} Beat={curBeat} Progress={progress:F3} Kind={kind}");

        SendActionRouted(kind, tx, ty, serverNow);
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

        // world(x,z) -> grid(x,y)
        int rx = Mathf.RoundToInt(rw.x);
        int ry = Mathf.RoundToInt(rw.z);

        if (Mathf.Abs(rx) > Mathf.Abs(ry)) ry = 0;
        else rx = 0;

        rx = Mathf.Clamp(rx, -1, 1);
        ry = Mathf.Clamp(ry, -1, 1);

        return new Vector2Int(rx, ry);
    }

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

    bool TryUpdateHoldState(out Vector2Int dir, out ActionKind kind)
    {
        dir = Vector2Int.zero;
        kind = ActionKind.Move;

        if (Input.GetKey(KeyCode.W)) { dir = Vector2Int.up; kind = ActionKind.Move; return true; }
        if (Input.GetKey(KeyCode.S)) { dir = Vector2Int.down; kind = ActionKind.Move; return true; }
        if (Input.GetKey(KeyCode.A)) { dir = Vector2Int.left; kind = ActionKind.Move; return true; }
        if (Input.GetKey(KeyCode.D)) { dir = Vector2Int.right; kind = ActionKind.Move; return true; }

        // Arrow Hold 공격을 원하면 여기 주석 해제
        // if (Input.GetKey(KeyCode.UpArrow)) { dir = Vector2Int.up; kind = ActionKind.Attack; return true; }
        // if (Input.GetKey(KeyCode.DownArrow)) { dir = Vector2Int.down; kind = ActionKind.Attack; return true; }
        // if (Input.GetKey(KeyCode.LeftArrow)) { dir = Vector2Int.left; kind = ActionKind.Attack; return true; }
        // if (Input.GetKey(KeyCode.RightArrow)) { dir = Vector2Int.right; kind = ActionKind.Attack; return true; }

        return false;
    }

    void TryFireAtBeatMidpoint(ActionKind kind, int targetX, int targetY, long serverNowMs)
    {
        if (!_holdActive)
            return;

        long beat = Rhythm.GetCurrentBeatIndex();

        if (_lastFiredBeatIndex == beat)
            return;

        long t0 = Rhythm.GetBeatTimeMs(beat);
        //long t1 = Rhythm.GetBeatTimeMs(beat + 1);
        //long mid = (t0 + t1) / 2;

        if (serverNowMs >= t0)
        {
            BeatDebugUI_TMP.Instance?.MarkHitNow();

            SendActionRouted(kind, targetX, targetY, serverNowMs);
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

    // ---------------------------
    // 핵심: Town/Game 라우팅 분리
    // ---------------------------
    void SendActionRouted(ActionKind kind, int targetX, int targetY, long serverNowMs)
    {
        if (NetworkManager.Instance == null)
        {
            Debug.LogError("NetworkManager.Instance NULL");
            return;
        }

        switch (channel)
        {
            case InputChannel.Town:
                SendTownAction(kind, targetX, targetY, serverNowMs);
                break;

            case InputChannel.Game:
                SendGameAction(kind, targetX, targetY, serverNowMs);
                break;
        }
    }

    void SendTownAction(ActionKind kind, int targetX, int targetY, long serverNowMs)
    {
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

    void SendGameAction(ActionKind kind, int targetX, int targetY, long serverNowMs)
    {
        CS_ActionRequest pkt = new CS_ActionRequest
        {
            ActorId = GS.MyActorId,
            ActionKind = (int)kind,
            TargetX = targetX,
            TargetY = targetY,
            ClientSendTimeMs = serverNowMs,
        };

        NetworkManager.Instance.Send(pkt.Write());

        // ── Client-Side Prediction: 공격/스킬 모션 즉시 재생 ──
        // 조건: Judge Window 내 + 해당 비트에서 아직 미발동
        if (kind == ActionKind.Attack || kind == ActionKind.Skill)
        {
            TryPlayAttackPrediction(kind, serverNowMs);
        }
    }

    /// <summary>
    /// Judge Window 내이고 해당 비트에서 아직 공격 모션을 재생하지 않은 경우에만
    /// 내 캐릭터의 공격/스킬 모션을 즉시 재생 (Client Prediction)
    /// </summary>
    void TryPlayAttackPrediction(ActionKind kind, long serverNowMs)
    {
        // 1) Nearest Beat 기준 Judge Window 체크
        long nearestBeat = Rhythm.GetNearestBeatIndex(serverNowMs);
        long beatTimeMs = Rhythm.GetBeatTimeMs(nearestBeat);
        long diff = System.Math.Abs(serverNowMs - beatTimeMs);

        if (diff > (long)Rhythm.judgeWindowMs)
            return; // Window 밖 → 모션 스킵

        // 2) 해당 비트에서 이미 재생했으면 스킵 (1비트 1회)
        if (nearestBeat == _lastAttackPredictionBeat)
            return;

        _lastAttackPredictionBeat = nearestBeat;

        // 3) 모션 재생
        var bv = BoardView.Instance;
        if (bv == null) return;

        double beatMs = Rhythm.GetBeatDurationMs();
        float duration = (float)(beatMs / 1000.0) * bv.actionDurationRatio;

        bv.PlayAttackPrediction(GS.MyActorId, kind, duration);
    }
}
