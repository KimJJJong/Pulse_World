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

    private HashSet<int> _triggeredEvents = new HashSet<int>();
    
    // For early CC cancellation cleanup
    private List<Vector2Int> _activeTelegraphs = new List<Vector2Int>();

    public void Initialize(BoardView boardView, int actorId, EntityVisual visual, string skillId, long startTick, bool isMine)
    {
        _boardView = boardView;
        _actorId = actorId;
        _visual = visual;
        _startTick = startTick;
        _isMine = isMine;

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

        Debug.Log($"[ClientSkillRunner] Started {skillId} for Actor {actorId} at Tick {startTick}");

        // Base Animation & Sound (Temporary until AnimationAction is added to NewSkillDto)
        if (RhythmClient.Instance != null)
        {
            float totalDurationSec = (_skillDef.Data.TotalDurationTicks / 480f) * (float)RhythmClient.Instance.GetBeatDurationMs() / 1000f;
            
            if (_visual != null)
            {
                _visual.PlaySkill(totalDurationSec, _isMine); 
            }

            if (skillId.Contains("Attack"))
            {
                FMODActionSoundPlayer.Instance?.PlayAttackSound(_isMine);
            }
        }
    }

    void Update()
    {
        if (_skillDef == null || _skillDef.Data == null) return;
        if (RhythmClient.Instance == null) return;

        // 1. Get Current Client Computed Server Tick
        long currentTick = RhythmClient.Instance.GetCurrentServerTick();
        
        // 2. Relative tick since skill started
        long relativeTick = currentTick - _startTick;

        // If skill is over
        if (relativeTick > _skillDef.Data.TotalDurationTicks)
        {
            Destroy(gameObject);
            return;
        }

        // 3. Process Events
        for (int t = 0; t < _skillDef.Data.Tracks.Count; t++)
        {
            var track = _skillDef.Data.Tracks[t];
            for (int e = 0; e < track.Events.Count; e++)
            {
                var ev = track.Events[e];
                // Unique ID for event per track & event index
                int eventHash = (t << 16) | e;
                
                if (_triggeredEvents.Contains(eventHash)) continue;

                if (relativeTick >= ev.TriggerTick)
                {
                    _triggeredEvents.Add(eventHash);
                    ProcessEvent(ev);
                }
            }
        }
    }

    private void ProcessEvent(SkillEvent ev)
    {
        if (ev.Action == null) return;

        switch (ev.Action.Type)
        {
            case SkillActionType.Damage:
                // Damage is Server authoritative via BeatActions, client logic ignored here or used for prediction VFX.
                break;

            case SkillActionType.Warning:
                if (ev.Action is WarningAction warning)
                {
                    if (RhythmClient.Instance != null)
                    {
                        float warningDurationSec = (ev.DurationTicks / 480f) * (float)RhythmClient.Instance.GetBeatDurationMs() / 1000f;
                        ShowWarningCells(warning.Shape, warningDurationSec);
                    }
                }
                break;

            case SkillActionType.Sound:
                if (ev.Action is SoundAction sound)
                {
                    bool isMine = _isMine && sound.UseOwnerPerspective;
                    if (!string.IsNullOrEmpty(sound.FmodEventPath))
                    {
                        // FmodEventPath가 있으면 범용 재생
                        FMODActionSoundPlayer.Instance?.PlayByEventPath(sound.FmodEventPath, sound.Volume);
                    }
                    else
                    {
                        // Path 미지정 시 기존 공격 사운드로 폴백
                        FMODActionSoundPlayer.Instance?.PlayAttackSound(isMine);
                    }
                }
                break;
        }
    }

    private void ShowWarningCells(IShapeDef shape, float durationSec)
    {
        if (_visual == null || _boardView == null || shape == null) return;

        int sx = 0;
        int sy = 0;

        // Fetch grid pos from entity info
        if (ClientGameState.Instance != null && ClientGameState.Instance.TryGetEntity(_actorId, out var info))
        {
            sx = info.X;
            sy = info.Y;
        }
        else
        {
            // fallback world to grid mapping or ignore
            return;
        }

        int dir = 0; // Default look direction up

        // Collect offsets
        List<GridPoint> offsets = new List<GridPoint>();

        if (shape is CustomCellsShape customShape)
        {
            offsets = customShape.Cells;
        }
        else if (shape is RectShape rect)
        {
            // Simple logic for rect directly in front
            for (int x = -rect.Width/2; x <= rect.Width/2; x++)
            {
                for (int y = 1; y <= rect.Height; y++)
                {
                    offsets.Add(new GridPoint(x, y));
                }
            }
        }
        // ... (can add Diamond logic here)

        foreach (var p in offsets)
        {
            int rx = p.X;
            int ry = p.Y;

            // Rotate based on dir (0:Up, 1:Right, 2:Down, 3:Left) default looking up.
            if (dir == 1)      { rx = p.Y; ry = -p.X; }
            else if (dir == 2) { rx = -p.X; ry = -p.Y; }
            else if (dir == 3) { rx = -p.Y; ry = p.X; }

            int tx = sx + rx;
            int ty = sy + ry;

            // Track for early cancellation
            _activeTelegraphs.Add(new Vector2Int(tx, ty));

            _boardView.SetTelegraphOverlay(tx, ty, true);
            StartCoroutine(ClearTelegraphOverlay(tx, ty, durationSec));
        }
    }

    private System.Collections.IEnumerator ClearTelegraphOverlay(int x, int y, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_boardView != null)
        {
            _activeTelegraphs.Remove(new Vector2Int(x, y));
            _boardView.SetTelegraphOverlay(x, y, false);
        }
    }

    private void OnDestroy()
    {
        // Gracefully clean up any active telegraphs when destroyed (e.g. CC cancellation)
        if (_boardView != null)
        {
            foreach (var pos in _activeTelegraphs)
            {
                _boardView.SetTelegraphOverlay(pos.x, pos.y, false);
            }
            _activeTelegraphs.Clear();
        }
        
        // Could also add Logic here to interrupt _visual.PlayAttack if we had an animation reference
    }
}
