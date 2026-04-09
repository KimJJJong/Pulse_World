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
        // 스킬이 이미 끝났더라도, Warning 기간이 남아있다면 출력해야 함
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
                // Unique ID for event per track & event index
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
                // Damage is Server authoritative via BeatActions, client logic ignored here or used for prediction VFX.
                break;

            case SkillActionType.Warning:
                if (ev.Action is WarningAction warning)
                {
                    if (RhythmClient.Instance != null)
                    {
                        // [Fix] 지연된 시간만큼 Duration에서 차감하여 정확한 종료 시점에 사라지도록 함
                        long elapsedSinceTrigger = relativeTick - ev.TriggerTick;
                        long remainingTicks = ev.DurationTicks - elapsedSinceTrigger;

                        if (remainingTicks > 0)
                        {
                            // [Change] 만료 비트를 계산하여 중앙 집중식 시스템에 위임 (올림 처리 적용)
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

    private void ShowWarningCells(IShapeDef shape, long expireBeat)
    {
        if (_visual == null || _boardView == null || shape == null) return;

        int sx = 0;
        int sy = 0;

        if (ClientGameState.Instance != null && ClientGameState.Instance.TryGetEntity(_actorId, out var info))
        {
            sx = info.X;
            sy = info.Y;
        }
        else return;

        int dir = 0; // TODO: Look Direction support
        List<GridPoint> offsets = new List<GridPoint>();

        if (shape is CustomCellsShape customShape) offsets = customShape.Cells;
        else if (shape is RectShape rect)
        {
            for (int x = -rect.Width/2; x <= rect.Width/2; x++)
                for (int y = 1; y <= rect.Height; y++)
                    offsets.Add(new GridPoint(x, y));
        }

        foreach (var p in offsets)
        {
            int rx = p.X;
            int ry = p.Y;

            if (dir == 1)      { rx = p.Y; ry = -p.X; }
            else if (dir == 2) { rx = -p.X; ry = -p.Y; }
            else if (dir == 3) { rx = -p.Y; ry = p.X; }

            int tx = sx + rx;
            int ty = sy + ry;

            // [Change] 중앙 집중식 관리 시스템 사용 (만료 시간 전달)
            _boardView.SetTelegraphWithExpire(tx, ty, expireBeat);
            _activeTelegraphs.Add(new Vector2Int(tx, ty));
        }
    }

    // [REMOVED] Coroutine clearing is now handled by BoardView.Update loop
    /*
    private System.Collections.IEnumerator ClearTelegraphOverlay(int x, int y, float delay) ...
    */

    private void OnDestroy()
    {
        // [Change] 강제 삭제 루프 제거.
        // 이제 BoardView가 만료 시간을 관리하므로, 스킬러너가 일찍 파괴되어도 경고 상태가 유지됩니다.
        if (_activeTelegraphs != null)
        {
            _activeTelegraphs.Clear();
        }
    }
}
