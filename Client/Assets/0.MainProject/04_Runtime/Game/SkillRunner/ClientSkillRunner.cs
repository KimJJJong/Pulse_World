using UnityEngine;
using System.Collections.Generic;
using Client.Data;
using GameShared.Data;

public class ClientSkillRunner : MonoBehaviour
{
    private NewSkillSO _skillDef;
    private long _startTick;
    private int _actorId;
    private bool _isMine;
    private EntityVisual _visual;
    private BoardView _boardView;

    // 스킬 시전 시점의 시전자 방향 (서버 / 로컬 공통)
    // BoardView.PlaySkillInstant 에서 rotation 파라미터로 전달된다.
    private float _casterRotation;

    private HashSet<int> _triggeredEvents = new HashSet<int>();
    
    // For early CC cancellation cleanup
    private List<Vector2Int> _activeTelegraphs = new List<Vector2Int>();

    public void Initialize(BoardView boardView, int actorId, EntityVisual visual, string skillId, long startTick, bool isMine, float casterRotation = 0f)
    {
        _boardView = boardView;
        _actorId = actorId;
        _visual = visual;
        _startTick = startTick;
        _isMine = isMine;
        _casterRotation = casterRotation;

        // Load Skill Data
        _skillDef = Resources.Load<NewSkillSO>($"Data/NewSkills/{skillId}");
        if (_skillDef == null)
        {
            Debug.LogError($"[ClientSkillRunner] Skill {skillId} not found in Resources/Data/NewSkills/");
            
            // Fallback for Prediction/Missing JSONs
            if (skillId == "Attack" || skillId == "Skill")
            {
                if (RhythmClient.Instance != null && _visual != null) {
                    float fallbackDuration = (float)(RhythmClient.Instance.GetBeatDurationMs() / 1000.0);
                    if (skillId == "Attack") _visual.PlayAttack(fallbackDuration, _isMine);
                    else _visual.PlaySkill(fallbackDuration, _isMine);
                }
            }

            Destroy(gameObject);
            return;
        }

        Debug.Log($"[ClientSkillRunner] Started {skillId} for Actor {actorId} at Tick {startTick} Rotation={casterRotation}");

        // Base Animation & Sound (Temporary until AnimationAction is added to NewSkillDto)
        if (RhythmClient.Instance != null)
        {
            float totalDurationSec = (_skillDef.Data.TotalDurationTicks / 480f) * (float)RhythmClient.Instance.GetBeatDurationMs() / 1000f;
            
            if (_visual != null)
            {
                _visual.PlaySkill(totalDurationSec, _isMine); 
            }
        }
    }

    private bool _firstUpdate = true;

    void Update()
    {
        if (_skillDef == null || _skillDef.Data == null) return;
        if (RhythmClient.Instance == null) return;

        // 1. Get Current Client Computed Server Tick
        long currentTick = RhythmClient.Instance.GetCurrentServerTick();
        
        // 2. Relative tick since skill started
        long relativeTick = currentTick - _startTick;

        // [Fix] 패킷 지연 시에도 최소 1회는 이벤트를 처리하도록 보장
        if (relativeTick > _skillDef.Data.TotalDurationTicks && !_firstUpdate)
        {
            Destroy(gameObject);
            return;
        }
        _firstUpdate = false;

        // 3. Process Events
        for (int t = 0; t < _skillDef.Data.Tracks.Count; t++)
        {
            var track = _skillDef.Data.Tracks[t];
            for (int e = 0; e < track.Events.Count; e++)
            {
                var ev = track.Events[e];
                int eventHash = (t << 16) | e;
                
                if (_triggeredEvents.Contains(eventHash)) continue;

                if (relativeTick >= ev.TriggerTick)
                {
                    _triggeredEvents.Add(eventHash);
                    ProcessEvent(ev, relativeTick);
                }
            }
        }
    }

    private void ProcessEvent(SkillEvent ev, long relativeTick)
    {
        if (ev.Action == null) return;

        switch (ev.Action.Type)
        {
            case SkillActionType.Damage:
                // 실제 데미지 계산은 서버 권위 (SC_BeatActions hpUpdates).
                // 클라이언트는 히트 범위 표시 + 시각/사운드 피드백만 담당.
                if (ev.Action is DamageAction damage && damage.HitMonsters)
                {
                    // 히트 범위 타일을 잠깐 하이라이트 (선택적 피드백)
                    ShowDamageCells(damage.Shape);
                    // 공격 사운드가 별도 Sound 이벤트로 없는 경우 폴백
                    if (_isMine)
                        FMODActionSoundPlayer.Instance?.PlayAttackSound(true);
                    Debug.Log($"[ClientSkillRunner] DamageAction fired for actor {_actorId} (HitMonsters=true)");
                }
                break;

            case SkillActionType.Warning:
                if (ev.Action is WarningAction warning)
                {
                    if (RhythmClient.Instance != null)
                    {
                        long elapsedSinceTrigger = relativeTick - ev.TriggerTick;
                        long remainingTicks = ev.DurationTicks - elapsedSinceTrigger;

                        if (remainingTicks > 0)
                        {
                            long expireBeat = (RhythmClient.Instance.GetCurrentServerTick() + remainingTicks + 479) / 480;
                            ShowWarningCells(warning.Shape, expireBeat);
                        }
                    }
                }
                break;

            case SkillActionType.Sound:
                if (ev.Action is SoundAction sound)
                {
                    bool isMine = _isMine && sound.UseOwnerPerspective;
                    if (!string.IsNullOrEmpty(sound.FmodEventPath))
                        FMODActionSoundPlayer.Instance?.PlayByEventPath(sound.FmodEventPath, sound.Volume);
                    else
                        FMODActionSoundPlayer.Instance?.PlayAttackSound(isMine);
                }
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Shape → World Cells 변환 공통 헬퍼
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 시전자의 현재 위치 + casterRotation 기준으로 Shape를 월드 셀 목록으로 변환한다.
    /// casterRotation은 Initialize 시 전달된 값(= 입력 시점의 targetObject.eulerAngles.y)을 우선 사용하고,
    /// 그것이 0이면 ClientGameState의 Rotation으로 폴백.
    /// </summary>
    private List<Vector2Int> ShapeToWorldCells(IShapeDef shape)
    {
        var result = new List<Vector2Int>();
        if (shape == null) return result;

        int sx = 0, sy = 0;
        float rotation = _casterRotation;

        if (ClientGameState.Instance != null && ClientGameState.Instance.TryGetEntity(_actorId, out var info))
        {
            sx = info.X;
            sy = info.Y;
            // 0도는 정북(Up) 방향으로 유효한 값이므로 덮어쓰지 않는다.
            // sentinel(-1)로 표시된 경우에만 entity 값으로 폴백한다.
            if (_casterRotation < 0f)
                rotation = info.Rotation;
        }
        else return result;

        List<GridPoint> offsets = new List<GridPoint>();

        if (shape is CustomCellsShape customShape) offsets = customShape.Cells;
        else if (shape is RectShape rect)
        {
            int halfW = rect.Width / 2;
            int halfH = rect.Height / 2;
            for (int x = -halfW; x <= halfW; x++)
                for (int y = -halfH; y <= halfH; y++)
                    offsets.Add(new GridPoint(x, y));
        }
        else if (shape is DiamondShape diamond)
        {
            int r = diamond.Radius;
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                    if (System.Math.Abs(x) + System.Math.Abs(y) <= r)
                        offsets.Add(new GridPoint(x, y));
        }

        foreach (var p in offsets)
        {
            var pt = RotateGridPoint(p.X, p.Y, shape.RotateWithCaster ? rotation : 0);
            result.Add(new Vector2Int(sx + pt.X, sy + pt.Y));
        }
        return result;
    }

    private void ShowWarningCells(IShapeDef shape, long expireBeat)
    {
        if (_boardView == null || shape == null) return;
        var cells = ShapeToWorldCells(shape);
        foreach (var cell in cells)
        {
            _boardView.SetTelegraphWithExpire(cell.x, cell.y, expireBeat);
            _activeTelegraphs.Add(cell);
        }
    }

    private void ShowDamageCells(IShapeDef shape)
    {
        // DamageAction 발동 시 잠깐 타일을 하이라이트 (선택적, 원하지 않으면 비워도 됨)
        // 현재는 별도 색상 없이 로그만 남김.
        if (shape == null) return;
        var cells = ShapeToWorldCells(shape);
        Debug.Log($"[ClientSkillRunner] DamageCells count={cells.Count} rotation={_casterRotation}");
        // TODO: 히트 VFX나 짧은 플래시 효과를 원하면 여기서 추가
    }

    private GridPoint RotateGridPoint(int x, int y, float rotation)
    {
        // [Rollback] Restoring legacy +180 offset and original formulas
        float corrected = rotation + 180f;
        int deg = (int)((corrected + 45) / 90) * 90;
        deg = (deg % 360 + 360) % 360;

        if (deg == 90)  return new GridPoint( y, -x); // Right (Now mapping East)
        if (deg == 180) return new GridPoint(-x, -y); // Down
        if (deg == 270) return new GridPoint(-y,  x); // Left
        return new GridPoint(x, y);                   // Up (0)
    }

    private void OnDestroy()
    {
        if (_activeTelegraphs != null)
            _activeTelegraphs.Clear();
    }
}
