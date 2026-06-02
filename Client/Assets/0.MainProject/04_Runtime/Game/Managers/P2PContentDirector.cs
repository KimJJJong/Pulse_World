using System;
using System.Collections.Generic;
using System.Linq;
using GameServer.InGame.Director.Core;
using GameServer.InGame.Director.Data;
using StageEventType = GameServer.InGame.Director.Core.EventType;
using StageActionData = GameServer.InGame.Director.Data.ActionData;
using StageConditionData = GameServer.InGame.Director.Data.ConditionData;
using StageEventData = GameServer.InGame.Director.Data.EventData;
using StageRectData = GameServer.InGame.Director.Data.RectData;
using StageRhythmSettingsData = GameServer.InGame.Director.Data.RhythmSettingsData;
using StageScenarioData = GameServer.InGame.Director.Data.StageScenario;
using StageSpawnData = GameServer.InGame.Director.Data.SpawnData;
using StageSpawnObjectData = GameServer.InGame.Director.Data.SpawnObjectData;
using UnityEngine;

[DefaultExecutionOrder(-800)]
public sealed partial class P2PContentDirector : MonoBehaviour, IStageActionHost
{
    private const int MaxCatchUpBeatsPerUpdate = 2;

    public static P2PContentDirector Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject(nameof(P2PContentDirector));
                _instance = go.AddComponent<P2PContentDirector>();
                DontDestroyOnLoad(go);
            }

            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;

    private static P2PContentDirector _instance;

    private readonly System.Random _rng = new();
    private readonly Dictionary<int, StageMonsterTemplate> _templatesByAppearanceId = new();
    private readonly Dictionary<string, MonsterPatternDef> _patterns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, MonsterRuntimeState> _monsterStates = new();
    private readonly Dictionary<int, int> _entityMaxHpByTemplateId = new();
    private readonly Dictionary<int, int> _stageObjectGroupByEntityId = new();
    private readonly Dictionary<int, int> _objectStatesByTargetId = new();
    private readonly StageRuntimeEngine _stageEngine = new();

    private string _mapId = "";
    private StageScenarioData _stage;
    private bool _stageLoaded;
    private string _lastStageLoadAttemptMapId = "";
    private long _lastProcessedBeat = long.MinValue;
    private long _stageLoadServerTimeMs;
    private int _nextSpawnEntityId = 10_000_000;
    private bool _bindingsDirty = true;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!ShouldDriveHostContent())
            return;

        EnsureStageLoaded();
        if (!_stageLoaded || ClientGameState.Instance == null)
            return;

        if (_bindingsDirty)
        {
            SyncBindingsFromWorld();
            _bindingsDirty = false;
        }
    }

    public void ConfigureStage(string mapId)
    {
        _mapId = mapId ?? "";
        _stageLoadServerTimeMs = TimeSync.ServerNowMs();
        _lastProcessedBeat = long.MinValue;
        _bindingsDirty = true;
        ResetStageRuntime();
        LoadStageContent();
        if (_stageLoaded)
        {
            SyncBindingsFromWorld();
            _bindingsDirty = false;
            _stageEngine.NotifyEvent(new GameEventContext(StageEventType.GameStart, timeMs: _stageLoadServerTimeMs), this);
        }
        if (P2PDebugConfig.TraceContent)
            Debug.Log($"[P2PContentDirector] ConfigureStage map={_mapId} loaded={_stageLoaded}");
    }

    public void OnHostLocalChanged(bool isHostLocal)
    {
        if (!isHostLocal)
            return;

        if (RhythmClient.Instance != null)
            _lastProcessedBeat = RhythmClient.Instance.GetCurrentBeatIndex() - 1;

        SyncBindingsFromWorld();
        _bindingsDirty = false;
        if (P2PDebugConfig.TraceContent)
            Debug.Log("[P2PContentDirector] Host local enabled");
    }

    public void ResetMatchState()
    {
        _mapId = "";
        _lastProcessedBeat = long.MinValue;
        _stageLoadServerTimeMs = 0;
        _nextSpawnEntityId = 10_000_000;
        _bindingsDirty = true;
        ResetStageRuntime();
        _patterns.Clear();

        if (P2PDebugConfig.TraceContent)
            Debug.Log("[P2PContentDirector] Match state reset");
    }

    public void OnEntitySpawned(ClientEntityInfo info)
    {
        if (!ShouldDriveHostContent() || info.EntityType != (int)EntityType.Monster)
            return;

        EnsureStageLoaded();
        long currentBeat = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : 0;
        BindMonsterEntity(info, currentBeat);
        MarkWorldDirty();
    }

    public void OnEntityDespawned(int entityId)
    {
        if (_monsterStates.TryGetValue(entityId, out var state))
            state.IsAlive = false;

        MarkWorldDirty();
    }

    public void MarkWorldDirty()
    {
        _bindingsDirty = true;
    }

    public bool ShouldAutoSubmitClearOnMonsterWipe()
    {
        EnsureStageLoaded();
        return !HasStageAction("ReturnToTown");
    }

    public void NotifyPlayerMoved(int actorId, int x, int y)
    {
        if (!ShouldDriveHostContent())
            return;

        EnsureStageLoaded();
        if (!_stageLoaded)
            return;

        _stageEngine.NotifyEvent(new GameEventContext(
            StageEventType.Move,
            sourceActorId: actorId,
            x: x,
            y: y,
            timeMs: TimeSync.ServerNowMs()),
            this);

        if (P2PDebugConfig.TraceContent)
            Debug.Log($"[P2PContentDirector] StageMove actor={actorId} pos=({x},{y})");
    }

    private void EnsureStageLoaded()
    {
        if (_stageLoaded)
            return;

        LoadStageContent();
        if (_stageLoaded)
            SyncBindingsFromWorld();
    }

    private bool ShouldDriveHostContent()
    {
        var bridge = P2PRelayClientBridge.Instance;
        return bridge != null && bridge.IsRelayMode && bridge.IsHostLocal && !bridge.IsTownRelayMode;
    }

    private bool HasStageAction(string actionType)
    {
        if (string.IsNullOrWhiteSpace(actionType) || _stage?.Events == null)
            return false;

        foreach (var evt in _stage.Events)
        {
            if (evt?.Actions == null)
                continue;

            foreach (var action in evt.Actions)
            {
                if (action != null
                    && string.Equals(action.Type, actionType, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
