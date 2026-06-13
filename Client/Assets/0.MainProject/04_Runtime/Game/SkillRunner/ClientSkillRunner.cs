using System;
using UnityEngine;
using System.Collections.Generic;
using Client.Data;
using GameShared.Data;

public class ClientSkillRunner : MonoBehaviour
{
    private static readonly bool VerboseSkillEventLogs = false;

    private NewSkillSO _skillDef;
    private long _startTick;
    private int _actorId;
    private bool _isMine;
    private EntityVisual _visual;
    private BoardView _boardView;
    private bool _playbackStarted;

    private float _casterRotation;

    private HashSet<int> _triggeredEvents = new HashSet<int>();
    private List<Vector2Int> _activeTelegraphs = new List<Vector2Int>();
    private readonly List<DueSkillEvent> _dueEvents = new List<DueSkillEvent>(16);

    // ── InputLock ─────────────────────────────────────────────────────────────
    // 내 캐릭터 전용. 스킬 시전 중 입력을 막고 OnDestroy에서 해제한다.
    private bool _isInputLocked = false;
    // InputLock 이벤트의 종료 Tick (DurationTicks 기준으로 계산)
    private long _inputLockEndTick = 0;

    public bool Initialize(BoardView boardView, int actorId, EntityVisual visual, string skillId, long startTick, bool isMine, float casterRotation = 0f)
    {
        ResetRuntimeState();

        _boardView = boardView;
        _actorId = actorId;
        _visual = visual;
        _startTick = startTick;
        _isMine = isMine;
        _casterRotation = casterRotation;

        // Load Skill Data
        _skillDef = P2PCombatContentCache.GetSkillAsset(skillId);
        if (_skillDef == null)
        {
            Debug.LogError($"[ClientSkillRunner] Skill {skillId} not found in Resources/Data/NewSkills/");

            bool fallbackPlayed = PlayFallbackSkillAnimation(skillId);
            Destroy(gameObject);
            return fallbackPlayed;
        }

        // [Fix] 스킬이 이미 만료된 상태로 도착했다면 (원격 RTT 지연) 즉시 폐기.
        // 기존 _firstUpdate 로직을 Initialize로 끌어올려 쓸모없는 SkillRunner 생성 자체를 차단.
        if (RhythmClient.Instance != null)
        {
            long currentTick = RhythmClient.Instance.GetCurrentServerTick();
            long relativeTick = currentTick - _startTick;
            if (relativeTick > _skillDef.Data.TotalDurationTicks)
            {
/*                Debug.LogWarning($"[ClientSkillRunner] Skill {skillId} already expired on arrival. " +
                                  $"RelativeTick={relativeTick} > TotalDuration={_skillDef.Data.TotalDurationTicks}. Discarding.");*/
                bool fallbackPlayed = PlayFallbackSkillAnimation(skillId);
                Destroy(gameObject);
                return fallbackPlayed;
            }
        }

        if (P2PDebugConfig.TraceCombat)
            Debug.Log($"[ClientSkillRunner] Started {skillId} for Actor {actorId} at Tick {startTick} Rotation={casterRotation}");

        if (RhythmClient.Instance != null)
        {
            long currentTick = RhythmClient.Instance.GetCurrentServerTick();
            TryStartPlayback(currentTick);
            ProcessDueEvents(currentTick - _startTick);
        }

        return true;
    }

    private bool PlayFallbackSkillAnimation(string skillId)
    {
        if (_visual == null || RhythmClient.Instance == null)
            return false;

        float fallbackDuration = (float)(RhythmClient.Instance.GetBeatDurationMs() / 1000.0);
        fallbackDuration = Mathf.Max(0.15f, fallbackDuration);
        string fallbackSkillId = string.IsNullOrWhiteSpace(skillId) ? "Attack" : skillId;
        _visual.PlaySkill(fallbackSkillId, fallbackDuration, _isMine);
        _playbackStarted = true;
        return true;
    }

    private void ResetRuntimeState()
    {
        ReleaseInputLock();
        _triggeredEvents.Clear();
        _activeTelegraphs.Clear();
        _skillDef = null;
        _startTick = 0;
        _actorId = 0;
        _isMine = false;
        _visual = null;
        _boardView = null;
        _casterRotation = 0f;
        _playbackStarted = false;
        _dueEvents.Clear();
    }

    void Update()
    {
        if (_skillDef == null || _skillDef.Data == null) return;
        if (RhythmClient.Instance == null) return;

        long currentTick = RhythmClient.Instance.GetCurrentServerTick();
        long relativeTick = currentTick - _startTick;

        TryStartPlayback(currentTick);

        // InputLock 만료 체크 — 종료 시점이 되면 입력 해제
        if (_isInputLocked && currentTick >= _inputLockEndTick)
        {
            ReleaseInputLock();
        }

        // [Fix] _firstUpdate 플래그 제거 — Initialize()에서 이미 만료 검증 완료
        if (relativeTick > _skillDef.Data.TotalDurationTicks)
        {
            Destroy(gameObject);
            return;
        }

        ProcessDueEvents(relativeTick);
    }

    private void ProcessDueEvents(long relativeTick)
    {
        if (_skillDef?.Data?.Tracks == null || relativeTick < 0)
            return;

        _dueEvents.Clear();

        for (int t = 0; t < _skillDef.Data.Tracks.Count; t++)
        {
            var track = _skillDef.Data.Tracks[t];
            if (track?.Events == null)
                continue;

            for (int e = 0; e < track.Events.Count; e++)
            {
                var ev = track.Events[e];
                int eventHash = (t << 16) | e;

                if (_triggeredEvents.Contains(eventHash)) continue;

                if (relativeTick >= ev.TriggerTick)
                {
                    _dueEvents.Add(new DueSkillEvent
                    {
                        TrackIndex = t,
                        EventIndex = e,
                        EventHash = eventHash,
                        Event = ev
                    });
                }
            }
        }

        if (_dueEvents.Count == 0)
            return;

        _dueEvents.Sort((a, b) =>
        {
            int cmp = a.Event.TriggerTick.CompareTo(b.Event.TriggerTick);
            if (cmp != 0)
                return cmp;

            cmp = GetActionPriority(a.Event.Action).CompareTo(GetActionPriority(b.Event.Action));
            if (cmp != 0)
                return cmp;

            cmp = a.TrackIndex.CompareTo(b.TrackIndex);
            if (cmp != 0)
                return cmp;

            return a.EventIndex.CompareTo(b.EventIndex);
        });

        foreach (var due in _dueEvents)
        {
            if (!_triggeredEvents.Add(due.EventHash))
                continue;

            if (VerboseSkillEventLogs)
            {
                Debug.Log($"[SkillEvent] actor={_actorId} track={due.TrackIndex} event={due.EventIndex} type={due.Event.Action?.Type} " +
                          $"triggerTick={due.Event.TriggerTick} relativeTick={relativeTick} " +
                          $"(lag={relativeTick - due.Event.TriggerTick} ticks late)");
            }

            ProcessEvent(due.Event, relativeTick);
        }
    }

    private void TryStartPlayback(long currentTick)
    {
        if (_playbackStarted || _visual == null || _skillDef?.Data == null)
            return;

        var rhythm = RhythmClient.Instance;
        if (rhythm == null)
            return;

        if (currentTick < _startTick)
            return;

        int totalDurationTicks = Mathf.Max(_skillDef.Data.TotalDurationTicks, 1);
        long relativeTick = System.Math.Max(0L, currentTick - _startTick);
        float normalizedStart = Mathf.Clamp01(relativeTick / (float)totalDurationTicks);
        float totalDurationSec = (totalDurationTicks / 480f) * (float)rhythm.GetBeatDurationMs() / 1000f;

        string skillId = _skillDef.Data != null ? _skillDef.Data.SkillId : "";
        _visual.PlaySkill(skillId, totalDurationSec, _isMine, normalizedStart);
        _playbackStarted = true;
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
                    if (P2PDebugConfig.TraceCombat)
                        Debug.Log($"[ClientSkillRunner] DamageAction fired for actor {_actorId} (HitMonsters=true)");
                }
                break;

            case SkillActionType.Warning:
                if (ev.Action is WarningAction warning && RhythmClient.Instance != null)
                {
                    // [Fix] Warning 만료 tick을 _startTick 기준 절대값으로 계산.
                    //
                    // 기존 코드: GetCurrentServerTick() + remainingTicks
                    //   → 원격 서버에서 RTT 지연으로 패킷이 늦게 오면 relativeTick이 이미 크게 진행됨
                    //   → remainingTicks = ev.DurationTicks - elapsedSinceTrigger 가 작아짐
                    //   → expireBeat가 너무 이른 시점으로 계산되어 Warning이 즉시 또는 짧게만 표시됨
                    //
                    // 수정 코드: _startTick + ev.TriggerTick + ev.DurationTicks
                    //   → 서버가 정의한 절대 만료 tick → RTT와 무관하게 항상 동일한 만료 시점 보장
                    long absoluteExpireTick = _startTick + ev.TriggerTick + ev.DurationTicks;
                    long expireBeat = (absoluteExpireTick + 479) / 480;

                    long currentBeat = RhythmClient.Instance.GetCurrentBeatIndex();
                    if (expireBeat <= currentBeat)
                    {
                        // 만료된 Warning은 표시하지 않음 (RTT로 인한 과거 Warning 표시 방지)
                        Debug.LogWarning($"[ClientSkillRunner] Warning already expired: expireBeat={expireBeat} <= currentBeat={currentBeat} actor={_actorId}. Skipping.");
                        break;
                    }

                    ShowWarningCells(warning.Shape, expireBeat);
                    //Debug.Log($"[ClientSkillRunner] Warning shown: expireBeat={expireBeat} currentBeat={currentBeat} actor={_actorId}");
                }
                break;

            case SkillActionType.InputLock:
                // 내 캐릭터만 입력 잠금
                if (_isMine)
                {
                    long lockStartTick = _startTick + ev.TriggerTick;
                    long rawLockEndTick = lockStartTick + ev.DurationTicks;
                    long lockEndTick = AdjustInputLockEndTickForAcceptWindow(lockStartTick, rawLockEndTick);
                    ApplyInputLock(lockEndTick);
                    if (P2PDebugConfig.TraceCombat)
                        Debug.Log($"[ClientSkillRunner] InputLock applied for actor {_actorId}: EndTick={lockEndTick} RawEndTick={rawLockEndTick}");
                }
                break;

            case SkillActionType.Sound:
                if (ev.Action is SoundAction sound)
                {
                    bool isMine = _isMine && sound.UseOwnerPerspective;
                    float startOffsetMs = CalculateLateEventOffsetMs(ev, relativeTick);
                    if (!string.IsNullOrEmpty(sound.FmodEventPath))
                        FMODActionSoundPlayer.Instance?.PlayByEventPath(sound.FmodEventPath, sound.Volume, startOffsetMs);
                    else
                        FMODActionSoundPlayer.Instance?.PlayAttackSound(isMine, startOffsetMs);

                    if (_isMine)
                        CombatImpactFeedback.Instance.PlayLocalAttackImpact();
                }
                break;
        }
    }

    private static int GetActionPriority(BaseAction action)
    {
        if (action == null)
            return int.MaxValue;

        return action.GetSkillActionType() switch
        {
            SkillActionType.Move => 0,
            SkillActionType.Warning => 1,
            SkillActionType.InputLock => 2,
            SkillActionType.Damage => 3,
            SkillActionType.Sound => 4,
            SkillActionType.Wait => 5,
            _ => 6
        };
    }

    private static float CalculateLateEventOffsetMs(SkillEvent ev, long relativeTick)
    {
        if (ev == null || relativeTick <= ev.TriggerTick || RhythmClient.Instance == null)
            return 0f;

        double beatMs = RhythmClient.Instance.GetBeatDurationMs();
        if (beatMs <= 0d)
            return 0f;

        double lateTicks = relativeTick - ev.TriggerTick;
        return (float)(lateTicks * beatMs / 480.0d);
    }

    private struct DueSkillEvent
    {
        public int TrackIndex;
        public int EventIndex;
        public int EventHash;
        public SkillEvent Event;
    }

    // ── InputLock 헬퍼 ────────────────────────────────────────────────────────

    private long AdjustInputLockEndTickForAcceptWindow(long lockStartTick, long lockEndTick)
    {
        var rhythm = RhythmClient.Instance;
        if (rhythm == null || rhythm.judgeWindowMs <= 0f)
            return lockEndTick;

        double beatDurationMs = rhythm.GetBeatDurationMs();
        if (beatDurationMs <= 0d)
            return lockEndTick;

        long acceptWindowTicks = (long)System.Math.Ceiling(rhythm.judgeWindowMs * 480.0d / beatDurationMs);
        return System.Math.Max(lockStartTick, lockEndTick - acceptWindowTicks);
    }

    private void ApplyInputLock(long endTick)
    {
        var rhythm = RhythmClient.Instance;
        if (rhythm != null)
        {
            long currentTick = rhythm.GetCurrentServerTick();
            if (currentTick >= endTick)
            {
                if (_isInputLocked && currentTick >= _inputLockEndTick)
                    ReleaseInputLock();
                return;
            }
        }

        // 더 늦게 끝나는 Lock이 들어오면 갱신
        if (endTick > _inputLockEndTick)
            _inputLockEndTick = endTick;

        if (RhythmInputController.Instance != null)
            RhythmInputController.Instance.ApplyTimedInputLock(_inputLockEndTick);

        if (!_isInputLocked)
        {
            _isInputLocked = true;
        }
    }

    private void ReleaseInputLock()
    {
        if (!_isInputLocked) return;
        _isInputLocked = false;
        if (RhythmInputController.Instance != null)
            RhythmInputController.Instance.ReleaseTimedInputLock(_inputLockEndTick);
        if (P2PDebugConfig.TraceCombat)
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
        Color color = _boardView.GetTelegraphColorForActor(_actorId);
        foreach (var cell in cells)
        {
            _boardView.SetTelegraphWithExpire(cell.x, cell.y, expireBeat, color);
            _activeTelegraphs.Add(cell);
        }
    }

    private void ShowDamageCells(IShapeDef shape)
    {
        if (shape == null) return;
        var cells = ShapeToWorldCells(shape);
        if (P2PDebugConfig.TraceCombat)
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
        _boardView?.NotifySkillRunnerStopped(_actorId, this);

        if (_activeTelegraphs != null)
            _activeTelegraphs.Clear();
    }
}

public static class P2PCombatContentCache
{
    private static readonly object _lock = new();
    private static readonly Dictionary<string, NewSkillSO> _skillAssets = new(StringComparer.Ordinal);
    private static bool _skillAssetsLoaded;

    public static void WarmUpSkills()
    {
        EnsureSkillAssetsLoaded();
    }

    public static NewSkillSO GetSkillAsset(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            return null;

        EnsureSkillAssetsLoaded();

        lock (_lock)
        {
            if (_skillAssets.TryGetValue(skillId, out var cached) && cached != null)
                return cached;
        }

        // 캐시에 없으면 개별 경로를 마지막으로 시도하고, 성공하면 다시 캐시에 올린다.
        var loaded = Resources.Load<NewSkillSO>($"Data/NewSkills/{skillId}");
        if (loaded != null)
        {
            lock (_lock)
            {
                CacheSkillAsset(loaded);
            }
        }

        return loaded;
    }

    public static NewSkillDef GetSkillDefinition(string skillId)
    {
        return GetSkillAsset(skillId)?.Data;
    }

    private static void EnsureSkillAssetsLoaded()
    {
        if (_skillAssetsLoaded)
            return;

        lock (_lock)
        {
            if (_skillAssetsLoaded)
                return;

            var assets = Resources.LoadAll<NewSkillSO>("Data/NewSkills");
            int loadedCount = 0;

            foreach (var asset in assets)
            {
                if (asset == null)
                    continue;

                if (CacheSkillAsset(asset))
                    loadedCount++;
            }

            _skillAssetsLoaded = true;
            Debug.Log($"[P2PCombatContentCache] Warmed skill assets: {loadedCount}");
        }
    }

    private static bool CacheSkillAsset(NewSkillSO asset)
    {
        if (asset == null)
            return false;

        string primaryId = asset.Data != null && !string.IsNullOrWhiteSpace(asset.Data.SkillId)
            ? asset.Data.SkillId
            : asset.name;

        if (string.IsNullOrWhiteSpace(primaryId))
            return false;

        _skillAssets[primaryId] = asset;

        if (asset.Data != null && !string.IsNullOrWhiteSpace(asset.Data.SkillId))
            _skillAssets[asset.Data.SkillId] = asset;

        return true;
    }
}
