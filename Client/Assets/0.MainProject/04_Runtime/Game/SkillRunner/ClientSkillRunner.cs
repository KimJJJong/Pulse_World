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

    private float _casterRotation;

    private HashSet<int> _triggeredEvents = new HashSet<int>();
    private List<Vector2Int> _activeTelegraphs = new List<Vector2Int>();

    // ── InputLock ─────────────────────────────────────────────────────────────
    // 내 캐릭터 전용. 스킬 시전 중 입력을 막고 OnDestroy에서 해제한다.
    private bool _isInputLocked = false;
    // InputLock 이벤트의 종료 Tick (DurationTicks 기준으로 계산)
    private long _inputLockEndTick = 0;

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
                if (RhythmClient.Instance != null && _visual != null)
                {
                    float fallbackDuration = (float)(RhythmClient.Instance.GetBeatDurationMs() / 1000.0);
                    _visual.PlaySkill(fallbackDuration, _isMine);
                }
            }

            Destroy(gameObject);
            return;
        }

        Debug.Log($"[ClientSkillRunner] Started {skillId} for Actor {actorId} at Tick {startTick} Rotation={casterRotation}");

        if (RhythmClient.Instance != null)
        {
            float totalDurationSec = (_skillDef.Data.TotalDurationTicks / 480f) * (float)RhythmClient.Instance.GetBeatDurationMs() / 1000f;
            if (_visual != null)
                _visual.PlaySkill(totalDurationSec, _isMine);
        }
    }

    private bool _firstUpdate = true;

    void Update()
    {
        if (_skillDef == null || _skillDef.Data == null) return;
        if (RhythmClient.Instance == null) return;

        long currentTick = RhythmClient.Instance.GetCurrentServerTick();
        long relativeTick = currentTick - _startTick;

        // InputLock 만료 체크 — 종료 시점이 되면 입력 해제
        if (_isInputLocked && currentTick >= _inputLockEndTick)
        {
            ReleaseInputLock();
        }

        if (relativeTick > _skillDef.Data.TotalDurationTicks && !_firstUpdate)
        {
            Destroy(gameObject);
            return;
        }
        _firstUpdate = false;

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
                if (ev.Action is DamageAction damage && damage.HitMonsters)
                {
                    ShowDamageCells(damage.Shape);
                    Debug.Log($"[ClientSkillRunner] DamageAction fired for actor {_actorId} (HitMonsters=true)");
                }
                break;

            case SkillActionType.Warning:
                if (ev.Action is WarningAction warning && RhythmClient.Instance != null)
                {
                    long elapsedSinceTrigger = relativeTick - ev.TriggerTick;
                    long remainingTicks = ev.DurationTicks - elapsedSinceTrigger;
                    if (remainingTicks > 0)
                    {
                        long expireBeat = (RhythmClient.Instance.GetCurrentServerTick() + remainingTicks + 479) / 480;
                        ShowWarningCells(warning.Shape, expireBeat);
                    }
                }
                break;

            case SkillActionType.InputLock:
                // 내 캐릭터만 입력 잠금
                if (_isMine)
                {
                    long lockEndTick = _startTick + ev.TriggerTick + ev.DurationTicks;
                    ApplyInputLock(lockEndTick);
                    Debug.Log($"[ClientSkillRunner] InputLock applied for actor {_actorId}: EndTick={lockEndTick}");
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

    // ── InputLock 헬퍼 ────────────────────────────────────────────────────────

    private void ApplyInputLock(long endTick)
    {
        // 더 늦게 끝나는 Lock이 들어오면 갱신
        if (endTick > _inputLockEndTick)
            _inputLockEndTick = endTick;

        if (!_isInputLocked)
        {
            _isInputLocked = true;
            if (RhythmInputController.Instance != null)
                RhythmInputController.Instance.IsInputBlocked = true;
        }
    }

    private void ReleaseInputLock()
    {
        if (!_isInputLocked) return;
        _isInputLocked = false;
        if (RhythmInputController.Instance != null)
            RhythmInputController.Instance.IsInputBlocked = false;
        Debug.Log($"[ClientSkillRunner] InputLock released for actor {_actorId}");
    }

    // ── Shape → World Cells ───────────────────────────────────────────────────

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
        if (shape == null) return;
        var cells = ShapeToWorldCells(shape);
        Debug.Log($"[ClientSkillRunner] DamageCells count={cells.Count} rotation={_casterRotation}");
    }

    private GridPoint RotateGridPoint(int x, int y, float rotation)
    {
        float corrected = rotation + 180f;
        int deg = (int)((corrected + 45) / 90) * 90;
        deg = (deg % 360 + 360) % 360;

        if (deg == 90)  return new GridPoint( y, -x);
        if (deg == 180) return new GridPoint(-x, -y);
        if (deg == 270) return new GridPoint(-y,  x);
        return new GridPoint(x, y);
    }

    private void OnDestroy()
    {
        // 스킬이 도중에 파괴되어도 InputLock 반드시 해제
        ReleaseInputLock();

        if (_activeTelegraphs != null)
            _activeTelegraphs.Clear();
    }
}
