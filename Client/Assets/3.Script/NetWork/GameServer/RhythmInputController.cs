using UnityEngine;
using UnityEngine.InputSystem;


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
    [SerializeField] InputChannel channel = InputChannel.Town;
    [SerializeField] bool allowRuntimeToggle = true;
    [SerializeField] KeyCode toggleKey = KeyCode.F1;

    [Header("Input")]
    [SerializeField] float inputCooldownMs = 80f;
    [SerializeField] public bool holdAutoInput = false;
    [SerializeField] float rotateAngle = 90f;
    [SerializeField] public GameObject targetObject = null;

    public bool IsInputBlocked { get; set; } = false;

    long _lastSendLocalMs = 0;

    bool _holdActive = false;
    Vector2Int _holdDir = Vector2Int.zero;
    ActionKind _holdKind = ActionKind.Move;

    long _lastFiredBeatIndex = long.MinValue;
    long _lastAttackPredictionBeat = long.MinValue;

    public static long LastAttackInputServerTimeMs { get; private set; }
    private long _lastActionBeatIndex = -1;

    [Header("Skill Slots")]
    [SerializeField] private string _normalAttackSkillId = "Attack";
    [SerializeField] private string[] _skillSlotIds = new string[4] { "Attack", "Attack", "Attack", "Attack" }; // [Fix] 기본값: 실제 스킬 ID 세팅 전까지 Attack 폴백

    public void SetSkillSlot(int slotIndex, string skillId)
    {
        if (slotIndex < 0 || slotIndex >= _skillSlotIds.Length) return;
        _skillSlotIds[slotIndex] = skillId;
        Debug.Log($"[RhythmInput] SkillSlot[{slotIndex}] set to {skillId}");
    }

    public void SetNormalAttackSkill(string skillId)
    {
        _normalAttackSkillId = skillId;
        Debug.Log($"[RhythmInput] NormalAttack set to {skillId}");
    }



    InputAction _moveAction;
    InputAction _attackAction;
    InputAction _skillHAction;
    InputAction _skillJAction;
    InputAction _skillKAction;
    InputAction _skillLAction;

    InputAction _rotateLeftAction;
    InputAction _rotateRightAction;
    InputAction _toggleAction;

    void OnEnable()
    {
        if (_moveAction == null)
        {
            _moveAction = new InputAction("Move", type: InputActionType.Value);
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            _attackAction = new InputAction("Attack", type: InputActionType.Button, binding: "<Keyboard>/space");

            _skillHAction = new InputAction("SkillH", type: InputActionType.Button, binding: "<Keyboard>/h");
            _skillJAction = new InputAction("SkillJ", type: InputActionType.Button, binding: "<Keyboard>/j");
            _skillKAction = new InputAction("SkillK", type: InputActionType.Button, binding: "<Keyboard>/k");
            _skillLAction = new InputAction("SkillL", type: InputActionType.Button, binding: "<Keyboard>/l");

            _rotateLeftAction  = new InputAction("RotateLeft",  type: InputActionType.Button, binding: "<Keyboard>/q");
            _rotateRightAction = new InputAction("RotateRight", type: InputActionType.Button, binding: "<Keyboard>/e");
            _toggleAction      = new InputAction("Toggle",      type: InputActionType.Button, binding: "<Keyboard>/f1");

            _moveAction.started    += OnMovePerformed;
            _attackAction.started  += OnAttackPerformed;

            _skillHAction.started += (ctx) => OnSkillSlotPerformed(0);
            _skillJAction.started += (ctx) => OnSkillSlotPerformed(1);
            _skillKAction.started += (ctx) => OnSkillSlotPerformed(2);
            _skillLAction.started += (ctx) => OnSkillSlotPerformed(3);

            _rotateLeftAction.started  += (ctx) => Rotate(-rotateAngle);
            _rotateRightAction.started += (ctx) => Rotate(+rotateAngle);
            _toggleAction.started += (ctx) => {
                if (allowRuntimeToggle) {
                    channel = (channel == InputChannel.Town) ? InputChannel.Game : InputChannel.Town;
                    Debug.Log($"[RhythmInput] Channel switched => {channel}");
                }
            };
        }
        _moveAction.Enable();
        _attackAction.Enable();
        _skillHAction.Enable();
        _skillJAction.Enable();
        _skillKAction.Enable();
        _skillLAction.Enable();
        _rotateLeftAction.Enable();
        _rotateRightAction.Enable();
        _toggleAction.Enable();
    }

    void OnDisable()
    {
        _moveAction?.Disable();
        _attackAction?.Disable();
        _skillHAction?.Disable();
        _skillJAction?.Disable();
        _skillKAction?.Disable();
        _skillLAction?.Disable();
        _rotateLeftAction?.Disable();
        _rotateRightAction?.Disable();
        _toggleAction?.Disable();
    }

    void OnDestroy()
    {
        _moveAction?.Dispose();
        _attackAction?.Dispose();
        _skillHAction?.Dispose();
        _skillJAction?.Dispose();
        _skillKAction?.Dispose();
        _skillLAction?.Dispose();
        _rotateLeftAction?.Dispose();
        _rotateRightAction?.Dispose();
        _toggleAction?.Dispose();
    }

    void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        if (holdAutoInput) return;
        HandleMoveInputEvent(ctx);
    }

    void OnAttackPerformed(InputAction.CallbackContext ctx)
    {
        if (holdAutoInput) return;
        // Space = 일반공격. ActionKind.Attack으로 명시 전송 (SlotIndex -1 오해석 방지)
        HandleAttackInputEvent(_normalAttackSkillId);
    }

    void OnSkillSlotPerformed(int slotIndex)
    {
        if (holdAutoInput) return;
        if (slotIndex < 0 || slotIndex >= _skillSlotIds.Length) return;
        HandleSkillInputEvent(_skillSlotIds[slotIndex], slotIndex);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Input Handlers
    // ──────────────────────────────────────────────────────────────────────────

    void HandleMoveInputEvent(InputAction.CallbackContext ctx)
    {
        if (!IsReady() || IsInputBlocked) return;

        Vector2 val = ctx.ReadValue<Vector2>();
        Vector2Int dir = Vector2Int.zero;
        if (Mathf.Abs(val.x) > Mathf.Abs(val.y)) dir.x = val.x > 0 ? 1 : (val.x < 0 ? -1 : 0);
        else dir.y = val.y > 0 ? 1 : (val.y < 0 ? -1 : 0);

        if (dir == Vector2Int.zero) return;

        double unityTimeNow = Time.realtimeSinceStartupAsDouble;
        double ageSec = unityTimeNow - ctx.time;
        long trueLocalNowMs = LocalNowMs() - (long)(ageSec * 1000.0);

        if (!PassCooldown(trueLocalNowMs)) return;

        // [서버 권위] 현재 서버 확정 위치(me.X/Y)로만 목표 계산
        // Prediction 비주얼 선행 제거 — 이동 결과는 SC_BeatActions(Move) 수신 시 처리
        if (!GS.TryGetMyEntity(out var me)) return;

        var rdir = RotateDirByTarget(dir);
        int tx = me.X + rdir.x;
        int ty = me.Y + rdir.y;

        long serverNow = trueLocalNowMs + (long)TimeSync.OffsetMs;
        BeatDebugUI_TMP.Instance?.MarkHitNow();

        // Beat 중복 입력 차단 (judgeWindow 안일 때만 '확정 비트' 체크)
        long nearestBeat = -1;
        long diff = 0;
        if (Rhythm != null)
        {
            nearestBeat = Rhythm.GetNearestBeatIndex(serverNow);
            long judgeTime = Rhythm.GetBeatTimeMs(nearestBeat);
            diff = System.Math.Abs(serverNow - judgeTime);

            if (diff <= Rhythm.judgeWindowMs)
            {
                if (_lastActionBeatIndex == nearestBeat)
                {
                    Debug.Log($"[Input_Move] DUPLICATE beat={nearestBeat} diff={diff}ms BLOCKED");
                    return;
                }
                _lastActionBeatIndex = nearestBeat;
            }
        }

        // [Input_Move] 이동 입력 로그 — 비트 판정, 서버 시간, 타겟 좌표를 한 번에 확인
        //Debug.Log($"[Input_Move] from=({me.X},{me.Y}) to=({tx},{ty}) dir=({rdir.x},{rdir.y}) " +
        //          $"serverNow={serverNow} beat={nearestBeat} diff={diff}ms inWin={diff <= (Rhythm != null ? Rhythm.judgeWindowMs : 0)}");

        SendActionRouted(ActionKind.Move, tx, ty, serverNow, -1);
        _lastSendLocalMs = trueLocalNowMs;
    }

    /// <summary>
    /// 일반 공격 (Space키) - ActionKind.Skill + SlotIndex=-1 로 서버 전송.
    ///
    /// [Fix-Prediction-Always] 원격 환경에서 judgeWindow 밖이면 Prediction이 발동 안 되던 문제 해결.
    /// 이제 Prediction은 "중복 비트가 아닌 한" 무조건 발동한다.
    /// - 로컬 애니메이션 즉시 재생 → "공격이 밀려서 적용" 현상 근본 차단
    /// - 서버 판정 실패 시에도 애니메이션은 보이고 그 뒤에 HP 갱신이 안 들어올 뿐
    ///   (기존에는 애니메이션 자체가 안 떴음)
    /// - 중복 비트 차단은 _lastAttackPredictionBeat 로 별도 관리
    /// </summary>
    void HandleAttackInputEvent(string skillId)
    {
        if (!IsReady() || IsInputBlocked) return;
        if (string.IsNullOrEmpty(skillId)) return;
        if (!GS.TryGetMyEntity(out var me)) return;

        // 바라보는 방향 앞 칸을 타겟으로
        var rdir = RotateDirByTarget(Vector2Int.up);
        int tx = me.X + rdir.x;
        int ty = me.Y + rdir.y;

        long trueLocalNowMs = LocalNowMs();
        if (!PassCooldown(trueLocalNowMs)) return;

        long serverNow = trueLocalNowMs + (long)TimeSync.OffsetMs;
        BeatDebugUI_TMP.Instance?.MarkHitNow();

        // 클라이언트 선행 애니메이션 (Prediction) — 항상 발동
        long predictionBeat = -1;
        long diff = 0;
        bool inJudgeWin = false;
        if (BoardView.Instance != null && Rhythm != null)
        {
            predictionBeat = Rhythm.GetNearestBeatIndex(serverNow);
            long judgeTime = Rhythm.GetBeatTimeMs(predictionBeat);
            diff = System.Math.Abs(serverNow - judgeTime);
            inJudgeWin = diff <= Rhythm.judgeWindowMs;

            // 중복 비트 차단 (같은 비트에서 Prediction 반복 방지)
            if (_lastAttackPredictionBeat == predictionBeat)
            {
                Debug.Log($"[Input_Attack] DUPLICATE predictionBeat={predictionBeat} BLOCKED");
                return;
            }
            _lastAttackPredictionBeat = predictionBeat;

            if (inJudgeWin) _lastActionBeatIndex = predictionBeat;

            long startTick = Rhythm.GetBeatTick(predictionBeat);
            float rotation = targetObject != null ? targetObject.transform.eulerAngles.y : 0f;

            // [Input_Attack] 핵심 로그 — Prediction 실행 시점 및 비트 정렬 상태
            Debug.Log($"[Input_Attack] skill={skillId} actor={me.EntityId} pos=({me.X},{me.Y}) " +
                      $"target=({tx},{ty}) rot={rotation:F0} serverNow={serverNow} " +
                      $"beat={predictionBeat} startTick={startTick} diff={diff}ms inWin={inJudgeWin} " +
                      $"rtt={TimeSync.EstimatedRttMs:F0}ms offset={TimeSync.OffsetMs:F0}ms → PlaySkillInstant");

            BoardView.Instance.PlaySkillInstant(me.EntityId, skillId, rotation, startTick);
        }

        if (TrySendCalib(serverNow)) { _lastSendLocalMs = trueLocalNowMs; return; }

        // [Fix] ActionKind.Skill + SlotIndex=-1 로 통일 전송 (서버 ResolveSkillId에서 normalAttack으로 처리)
        SendActionRouted(ActionKind.Skill, tx, ty, serverNow, -1);
        _lastSendLocalMs = trueLocalNowMs;
    }

    /// <summary>
    /// [Fix-Prediction-Always] HandleAttackInputEvent와 동일한 원칙.
    /// Prediction은 중복 비트가 아닌 한 무조건 발동.
    /// </summary>
    void HandleSkillInputEvent(string skillId, int slotIndex)
    {
        if (!IsReady() || IsInputBlocked) return;

        // [Fix] 스킬이 바인드되지 않은 슬롯 입력 차단 + 사용자 경고
        if (string.IsNullOrEmpty(skillId))
        {
            Debug.LogWarning($"[Input_Skill] Slot {slotIndex}: 스킬이 바인드되지 않았습니다. 장비에 Skill을 연결하세요.");
            return;
        }
        if (!GS.TryGetMyEntity(out var me)) return;

        var rdir = RotateDirByTarget(Vector2Int.up);
        int tx = me.X + rdir.x;
        int ty = me.Y + rdir.y;

        long trueLocalNowMs = LocalNowMs();
        if (!PassCooldown(trueLocalNowMs)) return;

        long serverNow = trueLocalNowMs + (long)TimeSync.OffsetMs;
        BeatDebugUI_TMP.Instance?.MarkHitNow();

        long predictionBeat = -1;
        long diff = 0;
        bool inJudgeWin = false;
        if (BoardView.Instance != null && Rhythm != null)
        {
            predictionBeat = Rhythm.GetNearestBeatIndex(serverNow);
            long judgeTime = Rhythm.GetBeatTimeMs(predictionBeat);
            diff = System.Math.Abs(serverNow - judgeTime);
            inJudgeWin = diff <= Rhythm.judgeWindowMs;

            if (_lastAttackPredictionBeat == predictionBeat)
            {
                Debug.Log($"[Input_Skill] DUPLICATE predictionBeat={predictionBeat} slot={slotIndex} BLOCKED");
                return;
            }
            _lastAttackPredictionBeat = predictionBeat;

            if (inJudgeWin) _lastActionBeatIndex = predictionBeat;

            long startTick = Rhythm.GetBeatTick(predictionBeat);
            float rotation = targetObject != null ? targetObject.transform.eulerAngles.y : 0f;

            Debug.Log($"[Input_Skill] skill={skillId} slot={slotIndex} actor={me.EntityId} pos=({me.X},{me.Y}) " +
                      $"target=({tx},{ty}) rot={rotation:F0} serverNow={serverNow} " +
                      $"beat={predictionBeat} startTick={startTick} diff={diff}ms inWin={inJudgeWin} " +
                      $"rtt={TimeSync.EstimatedRttMs:F0}ms offset={TimeSync.OffsetMs:F0}ms → PlaySkillInstant");

            BoardView.Instance.PlaySkillInstant(me.EntityId, skillId, rotation, startTick);
        }

        if (TrySendCalib(serverNow)) { _lastSendLocalMs = trueLocalNowMs; return; }

        SendActionRouted(ActionKind.Skill, tx, ty, serverNow, slotIndex);
        _lastSendLocalMs = trueLocalNowMs;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Update (Hold Auto Input)
    // ──────────────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!IsReady() || IsInputBlocked)
            return;

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

            if (TrySendCalib(serverNowMs))
                return;

            TryFireAtBeatMidpoint(_holdKind, targetX, targetY, serverNowMs);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Utilities
    // ──────────────────────────────────────────────────────────────────────────

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
        => TimeSync.LocalNowMs();

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

        Vector3 w  = new Vector3(dir.x, 0f, dir.y);
        Vector3 rw = targetObject.transform.rotation * w;

        int rx = Mathf.RoundToInt(rw.x);
        int ry = Mathf.RoundToInt(rw.z);

        if (Mathf.Abs(rx) > Mathf.Abs(ry)) ry = 0;
        else rx = 0;

        rx = Mathf.Clamp(rx, -1, 1);
        ry = Mathf.Clamp(ry, -1, 1);

        return new Vector2Int(rx, ry);
    }

    bool TryUpdateHoldState(out Vector2Int dir, out ActionKind kind)
    {
        dir  = Vector2Int.zero;
        kind = ActionKind.Move;

        if (_moveAction == null) return false;
        Vector2 val = _moveAction.ReadValue<Vector2>();
        if (val == Vector2.zero) return false;

        if (Mathf.Abs(val.x) > Mathf.Abs(val.y)) dir.x = val.x > 0 ? 1 : (val.x < 0 ? -1 : 0);
        else dir.y = val.y > 0 ? 1 : (val.y < 0 ? -1 : 0);

        return dir != Vector2Int.zero;
    }

    void TryFireAtBeatMidpoint(ActionKind kind, int targetX, int targetY, long serverNowMs)
    {
        if (!_holdActive) return;

        long beat = Rhythm.GetCurrentBeatIndex();
        if (_lastFiredBeatIndex == beat) return;

        long t0 = Rhythm.GetBeatTimeMs(beat);
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

    // ──────────────────────────────────────────────────────────────────────────
    // Network Send (Town / Game 라우팅)
    // ──────────────────────────────────────────────────────────────────────────

    void SendActionRouted(ActionKind kind, int targetX, int targetY, long serverNowMs, int slotIndex = -1)
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
                SendGameAction(kind, targetX, targetY, serverNowMs, slotIndex);
                break;
        }
    }

    void SendTownAction(ActionKind kind, int targetX, int targetY, long serverNowMs)
    {
        CS_TownActionRequest pkt = new CS_TownActionRequest
        {
            ActorId          = GS.MyActorId,
            ActionKind       = (int)kind,
            TargetX          = targetX,
            TargetY          = targetY,
            Rotation         = targetObject != null ? targetObject.transform.eulerAngles.y : 0f,
            ClientSendTimeMs = serverNowMs,
        };
        NetworkManager.Instance.Send(pkt.Write());
    }

    void SendGameAction(ActionKind kind, int targetX, int targetY, long serverNowMs, int slotIndex = -1)
    {
        CS_ActionRequest pkt = new CS_ActionRequest
        {
            ActorId          = GS.MyActorId,
            ActionKind       = (int)kind,
            SlotIndex        = slotIndex,
            TargetX          = targetX,
            TargetY          = targetY,
            Rotation         = targetObject != null ? targetObject.transform.eulerAngles.y : 0f,
            ClientSendTimeMs = serverNowMs,
        };
        NetworkManager.Instance.Send(pkt.Write());

        if (kind == ActionKind.Attack || kind == ActionKind.Skill)
            LastAttackInputServerTimeMs = serverNowMs;
    }
}
