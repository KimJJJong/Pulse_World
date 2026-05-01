using System;
using UnityEngine;
using UnityEngine.InputSystem;


public class RhythmInputController : MonoBehaviour
{
    private static RhythmInputController _instance;
    public static bool HasInstance => Instance != null;
    public static RhythmInputController Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindAnyObjectByType<RhythmInputController>();
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
    [SerializeField] float inputCooldownMs = 0f;
    [SerializeField] public bool holdAutoInput = false;
    [SerializeField] float rotateAngle = 90f;
    [SerializeField] public GameObject targetObject = null;

    public bool IsInputBlocked { get; set; } = false;
    public InputChannel CurrentChannel => channel;
    public bool HoldAutoInputEnabled => holdAutoInput;
    public string CurrentTargetName => targetObject != null ? targetObject.name : "<null>";

    long _lastSendLocalMs = 0;

    bool _holdActive = false;
    Vector2Int _holdDir = Vector2Int.zero;
    ActionKind _holdKind = ActionKind.Move;

    long _lastFiredBeatIndex = long.MinValue;
    long _lastAttackPredictionBeat = long.MinValue;

    public static long LastAttackInputServerTimeMs { get; private set; }
    private long _lastActionBeatIndex = -1;
    private double _nextGuardLogAt;
    private bool _hasLoggedFirstSend;

    [Header("Skill Slots")]
    [SerializeField] private string _normalAttackSkillId = "Attack";
    [SerializeField] private string[] _skillSlotIds = new string[4] { "Attack", "Attack", "Attack", "Attack" }; // [Fix] 기본값: 실제 스킬 ID 세팅 전까지 Attack 폴백

    public void SetSkillSlot(int slotIndex, string skillId)
    {
        if (slotIndex < 0 || slotIndex >= _skillSlotIds.Length) return;
        _skillSlotIds[slotIndex] = skillId;
        if (P2PDebugConfig.TraceInput)
            Debug.Log($"[RhythmInput] SkillSlot[{slotIndex}] set to {skillId}");
    }

    public void SetNormalAttackSkill(string skillId)
    {
        _normalAttackSkillId = skillId;
        if (P2PDebugConfig.TraceInput)
            Debug.Log($"[RhythmInput] NormalAttack set to {skillId}");
    }

    public string GetNormalAttackSkillId() => _normalAttackSkillId;

    public string GetSkillSlotId(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _skillSlotIds.Length)
            return "";

        return _skillSlotIds[slotIndex] ?? "";
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

    void Awake()
    {
        _instance = this;
        EnsureSceneConfiguration("Awake");
    }

    void Start()
    {
        EnsureSceneConfiguration("Start");
        Debug.Log($"[RhythmInput] Ready {GetDebugState()}");
    }

    public void ConfigureForScene(InputChannel inputChannel, bool enableHoldAutoInput)
    {
        channel = inputChannel;
        holdAutoInput = enableHoldAutoInput;
        IsInputBlocked = false;
        _holdActive = false;
        _holdDir = Vector2Int.zero;
        _holdKind = ActionKind.Move;
        _lastFiredBeatIndex = long.MinValue;
        _lastAttackPredictionBeat = long.MinValue;
        _lastActionBeatIndex = -1;
        _hasLoggedFirstSend = false;

        Debug.Log($"[RhythmInput] ConfigureForScene scene={GetSceneName()} channel={channel} hold={holdAutoInput} blocked={IsInputBlocked}");
    }

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
                    if (P2PDebugConfig.TraceInput)
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

        EnsureSceneConfiguration("OnEnable");
        Debug.Log($"[RhythmInput] Actions enabled {GetDebugState()}");
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
        if (_instance == this)
            _instance = null;

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
        if (!TryBeginInput("Move")) return;

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
        if (!GS.TryGetMyEntity(out var me))
        {
            LogGuardFailure("Move", $"MyEntity missing myActorId={GS.MyActorId} entityCount={GS.EntityCount} {GetDebugState()}");
            return;
        }

        var rdir = RotateDirByTarget(dir);
        int tx = me.X + rdir.x;
        int ty = me.Y + rdir.y;

        long serverNow = trueLocalNowMs + (long)TimeSync.OffsetMs;
        BeatDebugUI_TMP.Instance?.MarkHitNow();

        // 이동은 가장 가까운 비트로 양자화되므로 judge window 밖이어도
        // 같은 비트로 중복 입력되는 요청은 클라이언트에서 먼저 막는다.
        long nearestBeat = -1;
        long diff = 0;
        if (Rhythm != null)
        {
            nearestBeat = Rhythm.GetNearestBeatIndex(serverNow);
            long judgeTime = Rhythm.GetBeatTimeMs(nearestBeat);
            diff = System.Math.Abs(serverNow - judgeTime);

            if (_lastActionBeatIndex == nearestBeat)
            {
                if (P2PDebugConfig.TraceInput)
                    Debug.Log($"[Input_Move] DUPLICATE beat={nearestBeat} diff={diff}ms BLOCKED");
                return;
            }

            _lastActionBeatIndex = nearestBeat;
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
    /// judge window 밖 입력은 클라이언트에서 즉시 차단한다.
    /// - 잘못된 타이밍의 입력은 애니메이션/전송 모두 하지 않음
    /// - 중복 비트 차단은 _lastAttackPredictionBeat 로 별도 관리
    /// </summary>
    void HandleAttackInputEvent(string skillId)
    {
        if (!TryBeginInput("Attack")) return;
        if (string.IsNullOrEmpty(skillId))
        {
            LogGuardFailure("Attack", $"Normal attack skill is empty {GetDebugState()}");
            return;
        }
        if (!GS.TryGetMyEntity(out var me))
        {
            LogGuardFailure("Attack", $"MyEntity missing myActorId={GS.MyActorId} entityCount={GS.EntityCount} {GetDebugState()}");
            return;
        }

        // 바라보는 방향 앞 칸을 타겟으로
        var rdir = RotateDirByTarget(Vector2Int.up);
        int tx = me.X + rdir.x;
        int ty = me.Y + rdir.y;

        long trueLocalNowMs = LocalNowMs();
        if (!PassCooldown(trueLocalNowMs)) return;

        long serverNow = trueLocalNowMs + (long)TimeSync.OffsetMs;
        if (!TryGetJudgeWindowInfo(serverNow, out long predictionBeat, out long diff, out bool inJudgeWin))
        {
            if (P2PDebugConfig.TraceInput)
                Debug.Log($"[Input_Attack] OUT_OF_WINDOW skill={skillId} actor={me.EntityId} pos=({me.X},{me.Y}) " +
                          $"serverNow={serverNow} beat={predictionBeat} diff={diff}ms " +
                          $"rtt={TimeSync.EstimatedRttMs:F0}ms offset={TimeSync.OffsetMs:F0}ms → blocked");
            return;
        }

        BeatDebugUI_TMP.Instance?.MarkHitNow();

        // 클라이언트 선행 애니메이션 (Prediction) — 유효 window 안에서만 발동
        if (_lastAttackPredictionBeat == predictionBeat)
        {
            if (P2PDebugConfig.TraceInput)
                Debug.Log($"[Input_Attack] DUPLICATE predictionBeat={predictionBeat} BLOCKED");
            return;
        }
        _lastAttackPredictionBeat = predictionBeat;

        if (inJudgeWin) _lastActionBeatIndex = predictionBeat;

        long startTick = Rhythm.GetBeatTick(predictionBeat);
        float rotation = targetObject != null ? targetObject.transform.eulerAngles.y : 0f;

        bool deferHostLocalPlayback = ShouldDeferHostLocalPlayback();

        // [Input_Attack] 핵심 로그 — Prediction 실행 시점 및 비트 정렬 상태
        if (P2PDebugConfig.TraceInput)
        {
            Debug.Log($"[Input_Attack] skill={skillId} actor={me.EntityId} pos=({me.X},{me.Y}) " +
                      $"target=({tx},{ty}) rot={rotation:F0} serverNow={serverNow} " +
                      $"beat={predictionBeat} startTick={startTick} diff={diff}ms inWin={inJudgeWin} " +
                      $"rtt={TimeSync.EstimatedRttMs:F0}ms offset={TimeSync.OffsetMs:F0}ms → " +
                      (deferHostLocalPlayback ? "RelayHostDeferred" : "PlaySkillInstant"));
        }

        if (!deferHostLocalPlayback && BoardView.Instance != null)
            BoardView.Instance.PlaySkillInstant(me.EntityId, skillId, rotation, startTick);

        if (TrySendCalib(serverNow)) { _lastSendLocalMs = trueLocalNowMs; return; }

        // [Fix] ActionKind.Skill + SlotIndex=-1 로 통일 전송 (서버 ResolveSkillId에서 normalAttack으로 처리)
        SendActionRouted(ActionKind.Skill, tx, ty, serverNow, -1);
        _lastSendLocalMs = trueLocalNowMs;
    }

    /// <summary>
    /// HandleAttackInputEvent와 동일한 원칙.
    /// judge window 밖 입력은 클라이언트에서 즉시 차단한다.
    /// </summary>
    void HandleSkillInputEvent(string skillId, int slotIndex)
    {
        if (!TryBeginInput($"Skill[{slotIndex}]")) return;

        // [Fix] 스킬이 바인드되지 않은 슬롯 입력 차단 + 사용자 경고
        if (string.IsNullOrEmpty(skillId))
        {
            if (P2PDebugConfig.TraceInput)
                Debug.LogWarning($"[Input_Skill] Slot {slotIndex}: 스킬이 바인드되지 않았습니다. 장비에 Skill을 연결하세요.");
            return;
        }
        if (!GS.TryGetMyEntity(out var me))
        {
            LogGuardFailure($"Skill[{slotIndex}]", $"MyEntity missing myActorId={GS.MyActorId} entityCount={GS.EntityCount} {GetDebugState()}");
            return;
        }

        var rdir = RotateDirByTarget(Vector2Int.up);
        int tx = me.X + rdir.x;
        int ty = me.Y + rdir.y;

        long trueLocalNowMs = LocalNowMs();
        if (!PassCooldown(trueLocalNowMs)) return;

        long serverNow = trueLocalNowMs + (long)TimeSync.OffsetMs;
        if (!TryGetJudgeWindowInfo(serverNow, out long predictionBeat, out long diff, out bool inJudgeWin))
        {
            if (P2PDebugConfig.TraceInput)
                Debug.Log($"[Input_Skill] OUT_OF_WINDOW skill={skillId} slot={slotIndex} actor={me.EntityId} " +
                          $"pos=({me.X},{me.Y}) serverNow={serverNow} beat={predictionBeat} diff={diff}ms " +
                          $"rtt={TimeSync.EstimatedRttMs:F0}ms offset={TimeSync.OffsetMs:F0}ms → blocked");
            return;
        }

        BeatDebugUI_TMP.Instance?.MarkHitNow();

        if (_lastAttackPredictionBeat == predictionBeat)
        {
            if (P2PDebugConfig.TraceInput)
                Debug.Log($"[Input_Skill] DUPLICATE predictionBeat={predictionBeat} slot={slotIndex} BLOCKED");
            return;
        }
        _lastAttackPredictionBeat = predictionBeat;

        if (inJudgeWin) _lastActionBeatIndex = predictionBeat;

        long startTick = Rhythm.GetBeatTick(predictionBeat);
        float rotation = targetObject != null ? targetObject.transform.eulerAngles.y : 0f;

        bool deferHostLocalPlayback = ShouldDeferHostLocalPlayback();

        if (P2PDebugConfig.TraceInput)
        {
            Debug.Log($"[Input_Skill] skill={skillId} slot={slotIndex} actor={me.EntityId} pos=({me.X},{me.Y}) " +
                      $"target=({tx},{ty}) rot={rotation:F0} serverNow={serverNow} " +
                      $"beat={predictionBeat} startTick={startTick} diff={diff}ms inWin={inJudgeWin} " +
                      $"rtt={TimeSync.EstimatedRttMs:F0}ms offset={TimeSync.OffsetMs:F0}ms → " +
                      (deferHostLocalPlayback ? "RelayHostDeferred" : "PlaySkillInstant"));
        }

        if (!deferHostLocalPlayback && BoardView.Instance != null)
            BoardView.Instance.PlaySkillInstant(me.EntityId, skillId, rotation, startTick);

        if (TrySendCalib(serverNow)) { _lastSendLocalMs = trueLocalNowMs; return; }

        SendActionRouted(ActionKind.Skill, tx, ty, serverNow, slotIndex);
        _lastSendLocalMs = trueLocalNowMs;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Update (Hold Auto Input)
    // ──────────────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!TryBeginInput("Hold"))
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
            {
                LogGuardFailure("Hold", $"MyEntity missing myActorId={GS.MyActorId} entityCount={GS.EntityCount} {GetDebugState()}");
                return;
            }

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
        return GS != null && Rhythm != null;
    }

    static long LocalNowMs()
        => TimeSync.LocalNowMs();

    bool PassCooldown(long nowLocalMs)
    {
        if (inputCooldownMs <= 0f)
            return true;

        return (nowLocalMs - _lastSendLocalMs) >= inputCooldownMs;
    }

    bool TryGetJudgeWindowInfo(long serverNowMs, out long predictionBeat, out long diffMs, out bool inJudgeWindow)
    {
        predictionBeat = -1;
        diffMs = 0;
        inJudgeWindow = false;

        if (Rhythm == null)
            return false;

        predictionBeat = Rhythm.GetNearestBeatIndex(serverNowMs);
        long judgeTime = Rhythm.GetBeatTimeMs(predictionBeat);
        diffMs = System.Math.Abs(serverNowMs - judgeTime);
        inJudgeWindow = diffMs <= Rhythm.judgeWindowMs;
        return inJudgeWindow;
    }

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
        if (P2PRelayClientBridge.HasInstance && P2PRelayClientBridge.Instance.IsRelayMode)
            return false;

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
        EnsureSceneConfiguration("SendActionRouted");

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
        LogFirstSuccessfulSend(kind, targetX, targetY, serverNowMs, -1);

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
        LogFirstSuccessfulSend(kind, targetX, targetY, serverNowMs, slotIndex);

        // Input hot path: unconditional warning logs cause large editor spikes for local host input.
        if (P2PDebugConfig.TraceInput)
            Debug.Log($"[P2P_DEBUG_FLOW] SendGameAction: kind={kind}, target=({targetX},{targetY}), slot={slotIndex}, serverNow={serverNowMs}");

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

        var bridge = P2PRelayClientBridge.Instance;
        if (bridge.IsRelayMode)
        {
            if (bridge.IsHostLocal)
            {
                P2PHostController.Instance.EnqueueLocalActionRequest(pkt);
            }
            else
            {
                bridge.SendWrappedPacket(pkt);
            }
        }
        else
        {
            NetworkManager.Instance.Send(pkt.Write());
        }

        if (kind == ActionKind.Attack || kind == ActionKind.Skill)
            LastAttackInputServerTimeMs = serverNowMs;
    }

    private static bool ShouldDeferHostLocalPlayback()
    {
        var bridge = P2PRelayClientBridge.Instance;
        return bridge != null && bridge.IsRelayMode && bridge.IsHostLocal;
    }

    public string GetDebugState()
    {
        return $"scene={GetSceneName()} channel={channel} hold={holdAutoInput} blocked={IsInputBlocked} target={CurrentTargetName} active={isActiveAndEnabled}";
    }

    private bool TryBeginInput(string actionTag)
    {
        EnsureSceneConfiguration(actionTag);

        if (!IsReady())
        {
            LogGuardFailure(actionTag, BuildNotReadyReason());
            return false;
        }

        if (IsInputBlocked)
        {
            LogGuardFailure(actionTag, $"IsInputBlocked=true {GetDebugState()}");
            return false;
        }

        return true;
    }

    private void EnsureSceneConfiguration(string reason)
    {
        if (!TryGetScenePreset(out var expectedChannel, out var expectedHold))
            return;

        if (channel == expectedChannel && holdAutoInput == expectedHold)
            return;

        var beforeChannel = channel;
        var beforeHold = holdAutoInput;
        ConfigureForScene(expectedChannel, expectedHold);
        Debug.LogWarning($"[RhythmInput] Auto-corrected config reason={reason} scene={GetSceneName()} channel={beforeChannel}->{channel} hold={beforeHold}->{holdAutoInput}");
    }

    private bool TryGetScenePreset(out InputChannel expectedChannel, out bool enableHoldAutoInput)
    {
        expectedChannel = channel;
        enableHoldAutoInput = holdAutoInput;

        string sceneName = GetSceneName();
        if (sceneName.StartsWith("Game", StringComparison.OrdinalIgnoreCase))
        {
            expectedChannel = InputChannel.Game;
            enableHoldAutoInput = false;
            return true;
        }

        if (string.Equals(sceneName, "TownMap", StringComparison.OrdinalIgnoreCase))
        {
            expectedChannel = InputChannel.Town;
            enableHoldAutoInput = true;
            return true;
        }

        return false;
    }

    private string GetSceneName()
    {
        return gameObject.scene.IsValid() ? gameObject.scene.name : "<invalid-scene>";
    }

    private string BuildNotReadyReason()
    {
        return $"deps missing gs={(GS != null)} rhythm={(Rhythm != null)} net={(NetworkManager.Instance != null)} {GetDebugState()}";
    }

    private void LogGuardFailure(string actionTag, string reason)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < _nextGuardLogAt)
            return;

        _nextGuardLogAt = now + 1.0d;
        Debug.LogWarning($"[RhythmInput] BLOCK {actionTag}: {reason}");
    }

    private void LogFirstSuccessfulSend(ActionKind kind, int targetX, int targetY, long serverNowMs, int slotIndex)
    {
        if (_hasLoggedFirstSend)
            return;

        _hasLoggedFirstSend = true;

        bool isRelayMode = P2PRelayClientBridge.HasInstance && P2PRelayClientBridge.Instance.IsRelayMode;
        Debug.Log($"[RhythmInput] FirstSend kind={kind} scene={GetSceneName()} channel={channel} actor={GS?.MyActorId ?? 0} target=({targetX},{targetY}) slot={slotIndex} relay={isRelayMode} serverNow={serverNowMs} {GetDebugState()}");
    }
}
