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

    // 본인의 최근 공격 입력 시간을 기록 (로그용)
    public static long LastAttackInputServerTimeMs { get; private set; }

    [Header("Skill Slots")]
    [SerializeField] private string _normalAttackSkillId = "Attack";
    [SerializeField] private string[] _skillSlotIds = new string[4] { "Skill0", "Skill1", "Skill2", "Skill3" };

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

    // --- Prediction Tracking ---
    long _lastSkillPredictionBeat = long.MinValue;

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

            // [Change] Attack is now bound to Space and handles as a Skill
            _attackAction = new InputAction("Attack", type: InputActionType.Button, binding: "<Keyboard>/space");

            // [New] Skill keys HJKL
            _skillHAction = new InputAction("SkillH", type: InputActionType.Button, binding: "<Keyboard>/h");
            _skillJAction = new InputAction("SkillJ", type: InputActionType.Button, binding: "<Keyboard>/j");
            _skillKAction = new InputAction("SkillK", type: InputActionType.Button, binding: "<Keyboard>/k");
            _skillLAction = new InputAction("SkillL", type: InputActionType.Button, binding: "<Keyboard>/l");

            // [New] Utility keys
            _rotateLeftAction = new InputAction("RotateLeft", type: InputActionType.Button, binding: "<Keyboard>/q");
            _rotateRightAction = new InputAction("RotateRight", type: InputActionType.Button, binding: "<Keyboard>/e");
            _toggleAction = new InputAction("Toggle", type: InputActionType.Button, binding: "<Keyboard>/f1");

            _moveAction.started += OnMovePerformed;
            _attackAction.started += OnAttackPerformed;
            
            _skillHAction.started += (ctx) => OnSkillSlotPerformed(0);
            _skillJAction.started += (ctx) => OnSkillSlotPerformed(1);
            _skillKAction.started += (ctx) => OnSkillSlotPerformed(2);
            _skillLAction.started += (ctx) => OnSkillSlotPerformed(3);

            _rotateLeftAction.started += (ctx) => Rotate(-rotateAngle);
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
        // Space doesn't have direction, use look direction
        HandleSkillInputEvent(_normalAttackSkillId);
    }

    void OnSkillSlotPerformed(int slotIndex)
    {
        if (holdAutoInput) return;
        if (slotIndex < 0 || slotIndex >= _skillSlotIds.Length) return;
        HandleSkillInputEvent(_skillSlotIds[slotIndex]);
    }

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
        if (!GS.TryGetMyEntity(out var me)) return;

        var rdir = RotateDirByTarget(dir);
        int tx = me.X + rdir.x;
        int ty = me.Y + rdir.y;

        long serverNow = trueLocalNowMs + (long)TimeSync.OffsetMs;
        BeatDebugUI_TMP.Instance?.MarkHitNow();

        // --- Client-Side Prediction for Move ---
        if (BoardView.Instance != null && Rhythm != null)
        {
            long nearestBeat = Rhythm.GetNearestBeatIndex(serverNow);
            long judgeTime = Rhythm.GetBeatTimeMs(nearestBeat);
            long diff = System.Math.Abs(serverNow - judgeTime);

            if (diff <= Rhythm.judgeWindowMs)
            {
                BoardView.Instance.PlayMovePrediction(me.EntityId, tx, ty, BoardView.Instance.actionDurationRatio);
            }
        }

        SendActionRouted(ActionKind.Move, tx, ty, serverNow);
        _lastSendLocalMs = trueLocalNowMs;
    }

    void HandleSkillInputEvent(string skillId)
    {
        if (!IsReady() || IsInputBlocked) return;
        if (string.IsNullOrEmpty(skillId)) return;

        if (!GS.TryGetMyEntity(out var me)) return;

        // Use Current Looking Direction (Forward)
        Vector2Int dir = Vector2Int.up; 
        var rdir = RotateDirByTarget(dir);
        int tx = me.X + rdir.x;
        int ty = me.Y + rdir.y;

        long trueLocalNowMs = LocalNowMs(); 
        if (!PassCooldown(trueLocalNowMs)) return;

        long serverNow = trueLocalNowMs + (long)TimeSync.OffsetMs;
        BeatDebugUI_TMP.Instance?.MarkHitNow();

        // [Prediction] 로컬 즉시 재생 (판정 범위 내에 있을 때만)
        if (BoardView.Instance != null && Rhythm != null)
        {
            long nearestBeat = Rhythm.GetNearestBeatIndex(serverNow);
            long judgeTime = Rhythm.GetBeatTimeMs(nearestBeat);
            long diff = System.Math.Abs(serverNow - judgeTime);

            if (diff <= Rhythm.judgeWindowMs && _lastSkillPredictionBeat != nearestBeat)
            {
                _lastSkillPredictionBeat = nearestBeat;
                // SkillRunner는 틱 기반이므로 서버 동기화된 StartTick 필요
                long startTick = Rhythm.GetBeatTick(nearestBeat);
                BoardView.Instance.PlaySkillInstant(me.EntityId, skillId, startTick);
            }
        }

        if (TrySendCalib(serverNow))
        {
            _lastSendLocalMs = trueLocalNowMs;
            return;
        }

        SendActionRouted(ActionKind.Skill, tx, ty, serverNow, skillId);
        _lastSendLocalMs = trueLocalNowMs;
    }

    void Update()
    {
        if (!IsReady() || IsInputBlocked)
            return;

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
        }
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

    bool TryUpdateHoldState(out Vector2Int dir, out ActionKind kind)
    {
        dir = Vector2Int.zero;
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
    void SendActionRouted(ActionKind kind, int targetX, int targetY, long serverNowMs, string skillId = "")
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
                SendGameAction(kind, targetX, targetY, serverNowMs, skillId);
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

    void SendGameAction(ActionKind kind, int targetX, int targetY, long serverNowMs, string skillId = "")
    {
        CS_ActionRequest pkt = new CS_ActionRequest
        {
            ActorId = GS.MyActorId,
            ActionKind = (int)kind,
            SkillId = skillId ?? "", // [Fix] Ensure SkillId is not null to avoid crash in Write()
            TargetX = targetX,
            TargetY = targetY,
            ClientSendTimeMs = serverNowMs,
        };

        NetworkManager.Instance.Send(pkt.Write());

        // [User Request] 본인 공격/스킬의 100% 서버 동기화를 위해 클라 자체 선입력 재생(Prediction) 제외
        if (kind == ActionKind.Attack || kind == ActionKind.Skill)
        {
            LastAttackInputServerTimeMs = serverNowMs;
        }
    }
}
