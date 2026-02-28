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

    InputAction _moveAction;
    InputAction _attackAction;

    void OnEnable()
    {
        if (_moveAction == null)
        {
            _moveAction = new InputAction("Move", type: InputActionType.Value, binding: "2DVector");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            _attackAction = new InputAction("Attack", type: InputActionType.Value, binding: "2DVector");
            _attackAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            // discrete movement/attack usually feels more responsive on 'started' than 'performed' or 'canceled'
            _moveAction.started += OnMovePerformed;
            _attackAction.started += OnAttackPerformed;
        }
        _moveAction.Enable();
        _attackAction.Enable();
    }

    void OnDisable()
    {
        _moveAction?.Disable();
        _attackAction?.Disable();
    }

    void OnDestroy()
    {
        _moveAction?.Dispose();
        _attackAction?.Dispose();
    }

    void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        if (holdAutoInput) return;
        HandleInputEvent(ctx, ActionKind.Move);
    }

    void OnAttackPerformed(InputAction.CallbackContext ctx)
    {
        if (holdAutoInput) return;
        HandleInputEvent(ctx, ActionKind.Attack);
    }

    void HandleInputEvent(InputAction.CallbackContext ctx, ActionKind kind)
    {
        if (!IsReady() || IsInputBlocked) 
        {
            Debug.Log($"[RhythmInput] Ignored. Ready: {IsReady()}, Blocked: {IsInputBlocked}");
            return;
        }
        
        Vector2 val = ctx.ReadValue<Vector2>();
        
        // Strict orthogonal casting
        Vector2Int dir = Vector2Int.zero;
        if (Mathf.Abs(val.x) > Mathf.Abs(val.y))
        {
            dir.x = val.x > 0 ? 1 : (val.x < 0 ? -1 : 0);
        }
        else
        {
            dir.y = val.y > 0 ? 1 : (val.y < 0 ? -1 : 0);
        }

        Debug.Log($"[RhythmInput] Raw Vector2: {val} => Dir: {dir}");
        if (dir == Vector2Int.zero) return;

        // [Extreme Optimization] OS 레벨 하드웨어 인터럽트 시간(ctx.time)을 
        // Stopwatch 단조 시간 체계로 변환하여 0 Frame 지연 타임스탬프를 획득.
        double unityTimeNow = Time.realtimeSinceStartupAsDouble;
        double ageSec = unityTimeNow - ctx.time;
        long currentStopwatchMs = LocalNowMs();
        long trueLocalNowMs = currentStopwatchMs - (long)(ageSec * 1000.0);

        if (!PassCooldown(trueLocalNowMs)) 
        {
            Debug.Log($"[RhythmInput] Blocked by Cooldown.");
            return;
        }

        if (!GS.TryGetMyEntity(out var me)) 
        {
            Debug.Log($"[RhythmInput] Cannot find MyEntity.");
            return;
        }

        var rdir = RotateDirByTarget(dir);
        int tx = me.X + rdir.x;
        int ty = me.Y + rdir.y;
        
        Debug.Log($"[RhythmInput] Firing {kind} to ({tx}, {ty}) with 0-frame delay.");

        long serverNow = trueLocalNowMs + (long)TimeSync.OffsetMs;
        BeatDebugUI_TMP.Instance?.MarkHitNow();

        if (kind == ActionKind.Attack || kind == ActionKind.Skill)
        {
            LastAttackInputServerTimeMs = serverNow;

            // [Client-Side Prediction] 로컬에서 즉시 0ms 딜레이로 애니메이션/SFX 재생
            if (BoardView.Instance != null && Rhythm != null)
            {
                long nearestBeat = Rhythm.GetNearestBeatIndex(serverNow);
                if (_lastAttackPredictionBeat != nearestBeat)
                {
                    _lastAttackPredictionBeat = nearestBeat;
                    double beatMs = Rhythm.GetBeatDurationMs();
                    float duration = (float)(beatMs / 1000.0) * BoardView.Instance.actionDurationRatio;
                    BoardView.Instance.PlayInstantActionBroadcast(me.EntityId, kind, duration);
                }
                else
                {
                    Debug.Log($"[RhythmInput] Attack Prediction ignored. Already predicted for Beat: {nearestBeat}");
                }
            }
        }

        if (TrySendCalib(serverNow))
        {
            _lastSendLocalMs = trueLocalNowMs;
            return;
        }

        SendActionRouted(kind, tx, ty, serverNow);
        _lastSendLocalMs = trueLocalNowMs;
    }

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

        // 수동 입력(단발 탭)은 Update 대신 InputAction의 Event Callback(HandleInputEvent)에서 즉시 처리됩니다.
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

        Debug.Log($"[RhythmInput] SendActionRouted: Channel={channel}, ActorId={GS.MyActorId}, Kind={kind}, Target=({targetX},{targetY})");

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

        // [User Request] 본인 공격/스킬의 100% 서버 동기화를 위해 클라 자체 선입력 재생(Prediction) 제외
        if (kind == ActionKind.Attack || kind == ActionKind.Skill)
        {
            LastAttackInputServerTimeMs = serverNowMs;
        }
    }
}
