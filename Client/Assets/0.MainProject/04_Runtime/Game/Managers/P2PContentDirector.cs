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
public sealed class P2PContentDirector : MonoBehaviour, IStageActionHost
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
    private readonly StageRuntimeEngine _stageEngine = new();

    private string _mapId = "";
    private StageScenarioData _stage;
    private bool _stageLoaded;
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
        if (!_stageLoaded || RhythmClient.Instance == null || ClientGameState.Instance == null)
            return;

        long currentBeat = RhythmClient.Instance.GetCurrentBeatIndex();
        if (currentBeat < 0)
            return;

        if (_lastProcessedBeat == currentBeat && !_bindingsDirty)
            return;

        if (_lastProcessedBeat == long.MinValue)
            _lastProcessedBeat = currentBeat - 1;

        if (_bindingsDirty && _lastProcessedBeat >= currentBeat)
        {
            ProcessDirtyBeat(currentBeat);
            return;
        }

        int processedBeats = 0;
        while (_lastProcessedBeat < currentBeat && processedBeats < MaxCatchUpBeatsPerUpdate)
        {
            _lastProcessedBeat++;
            ProcessBeat(_lastProcessedBeat);
            processedBeats++;
        }
    }

    public void ConfigureStage(string mapId)
    {
        _mapId = mapId ?? "";
        _stageLoadServerTimeMs = TimeSync.ServerNowMs();
        _lastProcessedBeat = long.MinValue;
        _bindingsDirty = true;
        LoadStageContent();
        SyncBindingsFromWorld();
        _bindingsDirty = false;
        _stageEngine.NotifyEvent(new GameEventContext(StageEventType.GameStart, timeMs: _stageLoadServerTimeMs), this);
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
        _stage = null;
        _stageLoaded = false;
        _lastProcessedBeat = long.MinValue;
        _stageLoadServerTimeMs = 0;
        _nextSpawnEntityId = 10_000_000;
        _bindingsDirty = true;

        _templatesByAppearanceId.Clear();
        _patterns.Clear();
        _monsterStates.Clear();
        _stageEngine.Reset();

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

    private GameEventContext BuildStageContext(StageEventType type, long beat)
    {
        int x = 0;
        int y = 0;

        if (ClientGameState.Instance != null && ClientGameState.Instance.TryGetMyEntity(out var my))
        {
            x = my.X;
            y = my.Y;
        }

        int actorId = 0;
        if (ClientGameState.Instance != null)
            actorId = ClientGameState.Instance.MyActorId;

        return new GameEventContext(type, actorId, actorId, x, y, TimeSync.ServerNowMs());
    }

    private void EnsureStageLoaded()
    {
        if (_stageLoaded)
            return;

        LoadStageContent();
        SyncBindingsFromWorld();
    }

    private bool ShouldDriveHostContent()
    {
        var bridge = P2PRelayClientBridge.Instance;
        return bridge != null && bridge.IsRelayMode && bridge.IsHostLocal;
    }

    private void LoadStageContent()
    {
        if (string.IsNullOrWhiteSpace(_mapId))
            return;

        if (_stageLoaded)
            return;

        LoadEntityTemplateData();
        LoadPatternData();

        var textAsset = Resources.Load<TextAsset>($"Data/Stage/{_mapId}");
        if (textAsset == null)
        {
            Debug.LogWarning($"[P2PContentDirector] Stage json not found: Data/Stage/{_mapId}");
            return;
        }

        try
        {
            _stage = JsonUtility.FromJson<StageScenarioData>(textAsset.text);
            if (_stage == null)
            {
                Debug.LogWarning($"[P2PContentDirector] Failed to parse stage json: {_mapId}");
                return;
            }

            BuildAppearanceTemplateIndex(_stage);
            _stageEngine.LoadScenario(_stage);
            _stageLoaded = true;
            if (P2PDebugConfig.TraceContent)
                Debug.Log($"[P2PContentDirector] Loaded stage={_stage.MapId} events={_stage.Events?.Count ?? 0}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[P2PContentDirector] Stage load failed: {ex.Message}");
        }
    }

    private void LoadEntityTemplateData()
    {
        if (_entityMaxHpByTemplateId.Count > 0)
            return;

        var textAsset = Resources.Load<TextAsset>("Data/EntityData");
        if (textAsset == null)
        {
            Debug.LogWarning("[P2PContentDirector] EntityData.json not found. Monster HP fallback will be used.");
            return;
        }

        try
        {
            var root = JsonUtility.FromJson<EntityDataRoot>(textAsset.text);
            if (root?.Entities == null)
                return;

            foreach (var entity in root.Entities)
            {
                if (entity == null || entity.EntityId <= 0)
                    continue;

                _entityMaxHpByTemplateId[entity.EntityId] = Math.Max(1, entity.MaxHp);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[P2PContentDirector] EntityData load failed: {ex.Message}");
        }
    }

    private void LoadPatternData()
    {
        if (_patterns.Count > 0)
            return;

        var assets = Resources.LoadAll<TextAsset>("Data/Patterns");
        foreach (var asset in assets)
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                continue;

            try
            {
                var set = JsonUtility.FromJson<MonsterPatternSet>(asset.text);
                if (set?.Monsters != null && set.Monsters.Count > 0)
                {
                    foreach (var pattern in set.Monsters)
                        CachePattern(pattern);
                    continue;
                }

                var patternDef = JsonUtility.FromJson<MonsterPatternDef>(asset.text);
                CachePattern(patternDef);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[P2PContentDirector] Pattern parse failed '{asset.name}': {ex.Message}");
            }
        }

        if (P2PDebugConfig.TraceContent)
            Debug.Log($"[P2PContentDirector] Loaded patterns={_patterns.Count}");
    }

    private void CachePattern(MonsterPatternDef pattern)
    {
        if (pattern == null || string.IsNullOrWhiteSpace(pattern.MonsterType))
            return;

        _patterns[pattern.MonsterType] = pattern;
    }

    private void BuildAppearanceTemplateIndex(StageScenarioData stage)
    {
        _templatesByAppearanceId.Clear();

        if (stage == null)
            return;

        // Initial wave only.
        // Later waves may reuse the same MonsterId/AppearanceId but belong to a different
        // logical group, so event spawns are tracked through runtime state instead of here.
        foreach (var spawn in stage.InitialSpawns ?? new List<StageSpawnData>())
            RegisterTemplate(spawn.MonsterId, spawn.AI, spawn.GroupId);
    }

    private void RegisterTemplate(int appearanceId, string monsterType, int groupId)
    {
        if (appearanceId <= 0)
            return;

        if (!_entityMaxHpByTemplateId.TryGetValue(appearanceId, out var maxHp))
            maxHp = 50;

        _templatesByAppearanceId[appearanceId] = new StageMonsterTemplate
        {
            AppearanceId = appearanceId,
            MonsterType = string.IsNullOrWhiteSpace(monsterType) ? "Default" : monsterType,
            GroupId = groupId,
            MaxHp = Math.Max(1, maxHp)
        };
    }

    private void SyncBindingsFromWorld()
    {
        if (ClientGameState.Instance == null || !_stageLoaded)
            return;

        var seen = new HashSet<int>();
        long currentBeat = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : 0;

        foreach (var entity in ClientGameState.Instance.EnumerateEntities())
        {
            if (entity.EntityType != (int)EntityType.Monster)
                continue;

            seen.Add(entity.EntityId);
            BindMonsterEntity(entity, currentBeat);
        }

        foreach (var kv in _monsterStates)
        {
            if (seen.Contains(kv.Key))
                continue;

            kv.Value.IsAlive = false;
        }
    }

    private void BindMonsterEntity(ClientEntityInfo entity, long? spawnBeat = null)
    {
        if (!_templatesByAppearanceId.TryGetValue(entity.AppearanceId, out var template))
            return;

        if (!_monsterStates.TryGetValue(entity.EntityId, out var state))
        {
            state = new MonsterRuntimeState();
            _monsterStates[entity.EntityId] = state;
        }

        bool firstBind = state.GroupId <= 0 && state.MaxHp <= 0 && string.IsNullOrWhiteSpace(state.MonsterType);

        state.EntityId = entity.EntityId;
        state.AppearanceId = entity.AppearanceId;
        if (string.IsNullOrWhiteSpace(state.MonsterType))
            state.MonsterType = template.MonsterType;

        if (state.GroupId <= 0)
            state.GroupId = template.GroupId;

        if (state.MaxHp <= 0)
            state.MaxHp = template.MaxHp;

        if (firstBind && spawnBeat.HasValue)
            state.SpawnBeat = spawnBeat.Value;

        state.IsAlive = entity.Hp > 0;
        state.LastKnownHp = entity.Hp;
        state.Rotation = entity.Rotation;
    }

    private void ProcessDirtyBeat(long beat)
    {
        SyncBindingsFromWorld();
        _bindingsDirty = false;
        _stageEngine.NotifyEvent(BuildStageContext(StageEventType.Beat, beat), this);

        if (P2PHostController.HasInstance)
            P2PHostController.Instance.CheckAndSubmitGameResultIfCleared();
    }

    private void ProcessBeat(long beat)
    {
        if (_bindingsDirty)
        {
            SyncBindingsFromWorld();
            _bindingsDirty = false;
        }

        _stageEngine.NotifyEvent(BuildStageContext(StageEventType.Beat, beat), this);
        RunMonsterAI(beat);
        P2PHostController.Instance.CheckAndSubmitGameResultIfCleared();
    }

    private int CountDeadMonsters(int groupId)
    {
        int deadCount = 0;

        foreach (var state in _monsterStates.Values)
        {
            if (state.GroupId != groupId)
                continue;

            if (!state.IsAlive)
            {
                deadCount++;
                continue;
            }

            if (ClientGameState.Instance != null &&
                ClientGameState.Instance.TryGetEntity(state.EntityId, out var entity) &&
                entity.Hp <= 0)
            {
                deadCount++;
                state.IsAlive = false;
            }
        }

        return deadCount;
    }

    public int GetDeadMonsterCount(int groupId) => CountDeadMonsters(groupId);

    public long GetElapsedTimeMs()
    {
        if (_stageLoadServerTimeMs <= 0)
            return 0;

        return Math.Max(0, TimeSync.ServerNowMs() - _stageLoadServerTimeMs);
    }

    public void SpawnMonster(SpawnData data)
    {
        if (data == null)
            return;

        string monsterType = string.IsNullOrWhiteSpace(data.AI)
            ? ResolveMonsterType(data.MonsterId)
            : data.AI;

        int maxHp = ResolveMaxHp(data.MonsterId);
        int entityId = GenerateSpawnEntityId();
        long beat = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : 0;

        RegisterRuntimeMonster(entityId, data.MonsterId, monsterType, data.GroupId, maxHp, beat);

        var pkt = new SC_EntitySpawn
        {
            BeatIndex = beat,
            EntityId = entityId,
            EntityType = (int)EntityType.Monster,
            X = data.X,
            Y = ResolveMapY(data.Y, data.Z),
            Hp = maxHp,
            AppearanceId = data.MonsterId
        };

        if (P2PDebugConfig.TraceContent)
            Debug.Log($"[P2PContentDirector] SpawnMonster entity={entityId} appearance={data.MonsterId} type={monsterType} group={data.GroupId} pos=({data.X},{ResolveMapY(data.Y, data.Z)}) hp={maxHp}");

        P2PHostController.Instance.SendLocalAndRelay(pkt);
    }

    public void SpawnObject(SpawnObjectData data)
    {
        if (data == null)
            return;

        long beat = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : 0;
        var pkt = new SC_EntitySpawn
        {
            BeatIndex = beat,
            EntityId = data.EntityId,
            EntityType = data.EntityType <= 0 ? (int)EntityType.Object : data.EntityType,
            X = data.X,
            Y = ResolveMapY(data.Y, data.Z),
            Hp = 1,
            AppearanceId = data.EntityId
        };

        if (P2PDebugConfig.TraceContent)
            Debug.Log($"[P2PContentDirector] SpawnObject entity={data.EntityId} type={pkt.EntityType} pos=({data.X},{pkt.Y})");

        P2PHostController.Instance.SendLocalAndRelay(pkt);
    }

    public new void BroadcastMessage(string msg)
    {
        if (P2PDebugConfig.TraceContent)
            Debug.Log($"[P2PContentDirector] Broadcast: {msg}");
    }

    public void ReturnToTown()
    {
        P2PHostController.Instance.SendLocalAndRelay(new SC_ReturnToTown());
    }

    private void RegisterRuntimeMonster(int entityId, int appearanceId, string monsterType, int groupId, int maxHp, long beat)
    {
        if (!_monsterStates.TryGetValue(entityId, out var state))
        {
            state = new MonsterRuntimeState();
            _monsterStates[entityId] = state;
        }

        state.EntityId = entityId;
        state.AppearanceId = appearanceId;
        state.MonsterType = string.IsNullOrWhiteSpace(monsterType) ? ResolveMonsterType(appearanceId) : monsterType;
        state.GroupId = groupId;
        state.MaxHp = Math.Max(1, maxHp);
        state.SpawnBeat = beat;
        state.LockedUntilBeat = -1;
        state.PhaseId = "P1";
        state.IsAlive = true;
        state.LastKnownHp = maxHp;
        state.Rotation = 0f;

        _templatesByAppearanceId[appearanceId] = new StageMonsterTemplate
        {
            AppearanceId = appearanceId,
            MonsterType = state.MonsterType,
            GroupId = groupId,
            MaxHp = state.MaxHp
        };
    }

    public void OpenGate(int x, int y)
    {
        ClientGameState.Instance?.SetTile(x, y, (int)TileKind.Floor);
        if (P2PDebugConfig.TraceContent)
            Debug.Log($"[P2PContentDirector] OpenGate at ({x},{y})");
    }

    private void RunMonsterAI(long beat)
    {
        foreach (var state in _monsterStates.Values)
        {
            if (!state.IsAlive)
                continue;

            if (ClientGameState.Instance != null &&
                (!ClientGameState.Instance.TryGetEntity(state.EntityId, out var entity) || entity.Hp <= 0))
            {
                state.IsAlive = false;
                continue;
            }

            if (beat < state.LockedUntilBeat)
                continue;

            var pattern = ResolvePattern(state.MonsterType);
            if (pattern == null)
                continue;

            ApplyPhaseTransitions(pattern, state, beat);

            var phase = pattern.GetPhase(state.PhaseId) ?? pattern.GetPhase(pattern.DefaultPhase);
            if (phase == null || phase.Selectors == null || phase.Selectors.Count == 0)
                continue;

            var candidates = new List<SelectorDef>();
            foreach (var selector in phase.Selectors)
            {
                if (selector == null)
                    continue;

                if (IsSelectorOnCooldown(state, selector, beat))
                    continue;

                if (!IsSelectorEligible(selector, state, beat))
                    continue;

                candidates.Add(selector);
            }

            if (candidates.Count == 0)
                continue;

            var picked = WeightedPick(candidates);
            if (picked == null)
                continue;

            long lockedUntil = ScheduleSelectorTimeline(state, picked, beat);
            state.LockedUntilBeat = Math.Max(state.LockedUntilBeat, lockedUntil);

            if (picked.CooldownBeats > 0)
                state.Cooldowns[picked.Id] = beat + picked.CooldownBeats;
        }
    }

    private MonsterPatternDef ResolvePattern(string monsterType)
    {
        if (string.IsNullOrWhiteSpace(monsterType))
            return null;

        if (_patterns.TryGetValue(monsterType, out var pattern))
            return pattern;

        _patterns.TryGetValue("Default", out pattern);
        return pattern;
    }

    private void ApplyPhaseTransitions(MonsterPatternDef pattern, MonsterRuntimeState state, long beat)
    {
        if (pattern?.Transitions == null || state == null)
            return;

        foreach (var transition in pattern.Transitions)
        {
            if (transition == null)
                continue;

            if (!string.Equals(transition.FromPhaseId, state.PhaseId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!IsTransitionMet(transition, state, beat))
                continue;

            state.PhaseId = transition.ToPhaseId;
            state.LockedUntilBeat = -1;
            state.Cooldowns.Clear();
            if (P2PDebugConfig.TraceContent)
                Debug.Log($"[P2PContentDirector] Monster {state.EntityId} phase -> {state.PhaseId}");
            break;
        }
    }

    private bool IsTransitionMet(PhaseTransitionDef transition, MonsterRuntimeState state, long beat)
    {
        if (transition == null)
            return false;

        if (ClientGameState.Instance == null || !ClientGameState.Instance.TryGetEntity(state.EntityId, out var entity))
            return false;

        switch (transition.Type)
        {
            case PhaseTransitionType.HpPercentLE:
                if (state.MaxHp <= 0)
                    return false;
                return entity.Hp * 100 <= state.MaxHp * transition.Value;

            case PhaseTransitionType.TimeSinceSpawnBeatsGE:
                return beat - state.SpawnBeat >= transition.Value;
        }

        return false;
    }

    private bool IsSelectorOnCooldown(MonsterRuntimeState state, SelectorDef selector, long beat)
    {
        if (state == null || selector == null || string.IsNullOrWhiteSpace(selector.Id))
            return false;

        if (!state.Cooldowns.TryGetValue(selector.Id, out var readyBeat))
            return false;

        return beat < readyBeat;
    }

    private bool IsSelectorEligible(SelectorDef selector, MonsterRuntimeState state, long beat)
    {
        if (selector?.When?.All == null || selector.When.All.Count == 0)
            return true;

        if (ClientGameState.Instance == null || !ClientGameState.Instance.TryGetEntity(state.EntityId, out var self))
            return false;

        foreach (var condition in selector.When.All)
        {
            if (condition == null)
                continue;

            if (!IsConditionPassed(condition, self))
                return false;
        }

        return true;
    }

    private bool IsConditionPassed(ConditionDef condition, ClientEntityInfo self)
    {
        if (condition == null)
            return false;

        if (condition.Type == ConditionType.DistanceToClosestPlayerLE || condition.Type == ConditionType.DistanceToClosestPlayerGT)
        {
            if (TryFindClosestPlayer(self, out var target, out int distance))
            {
                return condition.Type switch
                {
                    ConditionType.DistanceToClosestPlayerLE => distance <= condition.Value,
                    ConditionType.DistanceToClosestPlayerGT => distance > condition.Value,
                    _ => true
                };
            }

            return false;
        }

        return true;
    }

    private SelectorDef WeightedPick(List<SelectorDef> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        int totalWeight = 0;
        foreach (var candidate in candidates)
            totalWeight += Math.Max(1, candidate?.Weight ?? 1);

        if (totalWeight <= 0)
            return candidates[0];

        int roll = _rng.Next(totalWeight);
        foreach (var candidate in candidates)
        {
            roll -= Math.Max(1, candidate?.Weight ?? 1);
            if (roll < 0)
                return candidate;
        }

        return candidates[0];
    }

    private long ScheduleSelectorTimeline(MonsterRuntimeState state, SelectorDef selector, long baseBeat)
    {
        long lastBeat = baseBeat;
        Vector2Int plannedPos = GetMonsterPosition(state.EntityId);
        float plannedRotation = GetMonsterRotation(state.EntityId);

        foreach (var action in selector.Timeline ?? new List<ActionDef>())
        {
            if (action == null)
                continue;

            long executeBeat = baseBeat + action.AtBeatOffset;
            lastBeat = Math.Max(lastBeat, executeBeat);

            switch (action.Type)
            {
                case ActionType.Wait:
                    lastBeat = Math.Max(lastBeat, executeBeat + 1);
                    break;

                case ActionType.MoveStepToward:
                    {
                        var target = FindTargetEntity(state, action.Target, out _);
                        if (target == null)
                        {
                            lastBeat = Math.Max(lastBeat, executeBeat + 1);
                            break;
                        }

                        var nextPos = StepTowards(plannedPos, new Vector2Int(target.Value.X, target.Value.Y), Math.Max(1, action.MoveDistance), state.EntityId);
                        if (!IsWalkable(nextPos.x, nextPos.y, state.EntityId))
                            nextPos = plannedPos;

                        float rotation = CalculateRotation(plannedPos, nextPos, plannedRotation);
                        P2PHostController.Instance.EnqueueAiAction(state.EntityId, ActionKind.Move, nextPos.x, nextPos.y, rotation, "", executeBeat);

                        plannedPos = nextPos;
                        plannedRotation = rotation;
                        lastBeat = Math.Max(lastBeat, executeBeat + 1);
                        break;
                    }

                case ActionType.Move:
                    {
                        Vector2Int nextPos = action.MoveDirection != MoveDirection.None
                            ? ResolveMoveDirection(plannedPos, state, action)
                            : ResolveMoveStrategy(plannedPos, state, action);

                        if (!IsWalkable(nextPos.x, nextPos.y, state.EntityId))
                            nextPos = plannedPos;

                        float rotation = CalculateRotation(plannedPos, nextPos, plannedRotation);
                        P2PHostController.Instance.EnqueueAiAction(state.EntityId, ActionKind.Move, nextPos.x, nextPos.y, rotation, "", executeBeat);

                        plannedPos = nextPos;
                        plannedRotation = rotation;
                        lastBeat = Math.Max(lastBeat, executeBeat + 1);
                        break;
                    }

                case ActionType.Attack:
                case ActionType.CastSkill:
                    {
                        var target = FindTargetEntity(state, action.Target, out _);
                        var targetPos = target != null ? new Vector2Int(target.Value.X, target.Value.Y) : plannedPos;
                        float rotation = target != null
                            ? CalculateRotation(plannedPos, targetPos, plannedRotation)
                            : plannedRotation;

                        string skillId = string.IsNullOrWhiteSpace(action.SkillId) ? "Attack" : action.SkillId;
                        var skillDef = P2PCombatContentCache.GetSkillDefinition(skillId);
                        int skillDurationBeats = Math.Max(1, ((skillDef?.TotalDurationTicks ?? 480) + 479) / 480);

                        P2PHostController.Instance.EnqueueAiAction(
                            state.EntityId,
                            action.Type == ActionType.Attack ? ActionKind.Attack : ActionKind.Skill,
                            targetPos.x,
                            targetPos.y,
                            rotation,
                            skillId,
                            executeBeat);

                        plannedRotation = rotation;
                        lastBeat = Math.Max(lastBeat, executeBeat + skillDurationBeats);
                        break;
                    }
            }
        }

        return lastBeat;
    }

    private bool IsWalkable(int x, int y, int ignoreEntityId)
    {
        if (ClientGameState.Instance == null)
            return false;

        return ClientGameState.Instance.IsWalkable(x, y) && !ClientGameState.Instance.IsOccupied(x, y, ignoreEntityId);
    }

    private Vector2Int ResolveMoveDirection(Vector2Int from, MonsterRuntimeState state, ActionDef action)
    {
        Vector2Int pos = from;
        int steps = Math.Max(1, action.MoveDistance);

        for (int step = 0; step < steps; step++)
        {
            Vector2Int next;
            switch (action.MoveDirection)
            {
                case MoveDirection.Up:
                    next = new Vector2Int(pos.x, pos.y + 1);
                    break;
                case MoveDirection.Down:
                    next = new Vector2Int(pos.x, pos.y - 1);
                    break;
                case MoveDirection.Left:
                    next = new Vector2Int(pos.x - 1, pos.y);
                    break;
                case MoveDirection.Right:
                    next = new Vector2Int(pos.x + 1, pos.y);
                    break;
                case MoveDirection.TowardTarget:
                    {
                        var target = FindTargetEntity(state, action.Target, out _);
                        next = target != null ? StepTowards(pos, new Vector2Int(target.Value.X, target.Value.Y), 1) : pos;
                        break;
                    }
                case MoveDirection.AwayFromTarget:
                    {
                        var target = FindTargetEntity(state, action.Target, out _);
                        next = target != null ? StepAway(pos, new Vector2Int(target.Value.X, target.Value.Y)) : pos;
                        break;
                    }
                default:
                    next = pos;
                    break;
            }

            if (!IsWalkable(next.x, next.y, state.EntityId))
                break;

            pos = next;
        }

        return pos;
    }

    private Vector2Int ResolveMoveStrategy(Vector2Int from, MonsterRuntimeState state, ActionDef action)
    {
        int dist = Math.Max(1, action.MoveDistance);

        switch (action.MoveStrategy)
        {
            case MoveStrategy.Random:
                {
                    var candidates = new List<Vector2Int>
                    {
                        new Vector2Int(from.x, from.y + dist),
                        new Vector2Int(from.x, from.y - dist),
                        new Vector2Int(from.x + dist, from.y),
                        new Vector2Int(from.x - dist, from.y)
                    };

                    candidates = candidates.Where(c => IsWalkable(c.x, c.y, state.EntityId)).ToList();
                    if (candidates.Count == 0)
                        return from;

                    return candidates[_rng.Next(candidates.Count)];
                }

            case MoveStrategy.Flee:
                {
                    var target = FindTargetEntity(state, action.Target, out _);
                    if (target == null)
                        return from;

                    int dx = from.x - target.Value.X;
                    int dy = from.y - target.Value.Y;
                    if (Math.Abs(dx) >= Math.Abs(dy))
                        return new Vector2Int(from.x + Math.Sign(dx) * dist, from.y);

                    return new Vector2Int(from.x, from.y + Math.Sign(dy) * dist);
                }

            case MoveStrategy.Forward:
                {
                    var target = FindClosestPlayerEntity(state, out _);
                    if (target == null)
                        return from;

                    return StepTowards(from, new Vector2Int(target.Value.X, target.Value.Y), dist, state.EntityId);
                }

            case MoveStrategy.Backward:
                {
                    var target = FindClosestPlayerEntity(state, out _);
                    if (target == null)
                        return from;

                    int dx = from.x - target.Value.X;
                    int dy = from.y - target.Value.Y;
                    int sx = 0;
                    int sy = 0;

                    if (Math.Abs(dx) >= Math.Abs(dy))
                        sx = Math.Sign(dx);
                    else
                        sy = Math.Sign(dy);

                    if (sx == 0 && sy == 0)
                        sx = -1;

                    return new Vector2Int(from.x + sx * dist, from.y + sy * dist);
                }
        }

        return from;
    }

    private Vector2Int StepTowards(Vector2Int from, Vector2Int target, int distance, int ignoreEntityId = -1)
    {
        Vector2Int pos = from;
        int steps = Math.Max(1, distance);

        for (int i = 0; i < steps; i++)
        {
            int dx = target.x - pos.x;
            int dy = target.y - pos.y;

            if (dx == 0 && dy == 0)
                break;

            Vector2Int next;
            if (Math.Abs(dx) >= Math.Abs(dy))
                next = new Vector2Int(pos.x + Math.Sign(dx), pos.y);
            else
                next = new Vector2Int(pos.x, pos.y + Math.Sign(dy));

            if (!IsWalkable(next.x, next.y, ignoreEntityId))
                break;

            pos = next;
        }

        return pos;
    }

    private Vector2Int StepAway(Vector2Int from, Vector2Int threat)
    {
        int dx = from.x - threat.x;
        int dy = from.y - threat.y;

        if (dx == 0 && dy == 0)
            return new Vector2Int(from.x + 1, from.y);

        if (Math.Abs(dx) >= Math.Abs(dy))
            return new Vector2Int(from.x + Math.Sign(dx), from.y);

        return new Vector2Int(from.x, from.y + Math.Sign(dy));
    }

    private bool TryFindClosestPlayer(ClientEntityInfo self, out ClientEntityInfo? best, out int distance)
    {
        best = null;
        distance = int.MaxValue;

        if (ClientGameState.Instance == null)
            return false;

        foreach (var entity in ClientGameState.Instance.EnumerateEntities())
        {
            if (entity.EntityType != (int)EntityType.Player || entity.Hp <= 0)
                continue;

            int dist = Math.Abs(entity.X - self.X) + Math.Abs(entity.Y - self.Y);
            if (dist >= distance)
                continue;

            best = entity;
            distance = dist;
        }

        return best.HasValue;
    }

    private ClientEntityInfo? FindClosestPlayerEntity(MonsterRuntimeState state, out int distance)
    {
        distance = int.MaxValue;

        if (ClientGameState.Instance == null || !ClientGameState.Instance.TryGetEntity(state.EntityId, out var self))
            return null;

        ClientEntityInfo? best = null;
        foreach (var entity in ClientGameState.Instance.EnumerateEntities())
        {
            if (entity.EntityType != (int)EntityType.Player || entity.Hp <= 0)
                continue;

            int dist = Math.Abs(entity.X - self.X) + Math.Abs(entity.Y - self.Y);
            if (dist >= distance)
                continue;

            best = entity;
            distance = dist;
        }

        return best;
    }

    private ClientEntityInfo? FindTargetEntity(MonsterRuntimeState state, TargetDef target, out int distance)
    {
        distance = int.MaxValue;

        if (ClientGameState.Instance == null || !ClientGameState.Instance.TryGetEntity(state.EntityId, out var self))
            return null;

        var candidates = new List<ClientEntityInfo>();
        foreach (var entity in ClientGameState.Instance.EnumerateEntities())
        {
            if (entity.EntityId == state.EntityId)
                continue;

            if (target != null && target.RequireAlive && entity.Hp <= 0)
                continue;

            bool isPlayer = entity.EntityType == (int)EntityType.Player;
            bool isMonster = entity.EntityType == (int)EntityType.Monster || entity.EntityType == (int)EntityType.Object;
            if (!isPlayer && !isMonster)
                continue;

            int dist = Math.Abs(entity.X - self.X) + Math.Abs(entity.Y - self.Y);
            if (target != null && dist > target.MaxRange)
                continue;

            candidates.Add(entity);
        }

        if (candidates.Count == 0)
            return null;

        var playerCandidates = candidates.Where(e => e.EntityType == (int)EntityType.Player).ToList();
        if (playerCandidates.Count == 0)
            return null;

        if (target == null)
        {
            distance = int.MaxValue;
            return playerCandidates[0];
        }

        switch (target.Type)
        {
            case TargetType.LowestHpPlayer:
                {
                    ClientEntityInfo? best = null;
                    int minHp = int.MaxValue;
                    foreach (var entity in playerCandidates)
                    {
                        if (entity.Hp < minHp)
                        {
                            minHp = entity.Hp;
                            best = entity;
                        }
                    }

                    if (best.HasValue)
                    {
                        distance = Math.Abs(best.Value.X - self.X) + Math.Abs(best.Value.Y - self.Y);
                        return best;
                    }

                    break;
                }

            case TargetType.RandomPlayer:
                {
                    var pick = playerCandidates[_rng.Next(playerCandidates.Count)];
                    distance = Math.Abs(pick.X - self.X) + Math.Abs(pick.Y - self.Y);
                    return pick;
                }
        }

        var closest = playerCandidates
            .OrderBy(e => Math.Abs(e.X - self.X) + Math.Abs(e.Y - self.Y))
            .First();
        distance = Math.Abs(closest.X - self.X) + Math.Abs(closest.Y - self.Y);
        return closest;
    }

    private float GetMonsterRotation(int entityId)
    {
        if (ClientGameState.Instance != null && ClientGameState.Instance.TryGetEntity(entityId, out var entity))
            return entity.Rotation;

        return 0f;
    }

    private Vector2Int GetMonsterPosition(int entityId)
    {
        if (ClientGameState.Instance != null && ClientGameState.Instance.TryGetEntity(entityId, out var entity))
            return new Vector2Int(entity.X, entity.Y);

        return Vector2Int.zero;
    }

    private int ResolveMaxHp(int appearanceId)
    {
        if (_entityMaxHpByTemplateId.TryGetValue(appearanceId, out var hp))
            return Math.Max(1, hp);

        return 50;
    }

    private string ResolveMonsterType(int appearanceId)
    {
        if (_templatesByAppearanceId.TryGetValue(appearanceId, out var template) && !string.IsNullOrWhiteSpace(template.MonsterType))
            return template.MonsterType;

        return "Default";
    }

    private int GenerateSpawnEntityId()
        => _nextSpawnEntityId++;

    private static int ResolveMapY(int legacyY, int unityZ)
    {
        if (unityZ != 0)
            return unityZ;

        return legacyY;
    }

    private static float CalculateRotation(Vector2Int from, Vector2Int to, float current)
    {
        int dx = to.x - from.x;
        int dy = to.y - from.y;

        if (dx == 0 && dy == 0)
            return current;

        if (dy > 0) return 0f;
        if (dy < 0) return 180f;
        if (dx > 0) return 90f;
        if (dx < 0) return 270f;

        return current;
    }

    private struct StageMonsterTemplate
    {
        public int AppearanceId;
        public string MonsterType;
        public int GroupId;
        public int MaxHp;
    }

    [Serializable]
    private sealed class EntityDataRoot
    {
        public List<EntityDataRecord> Entities = new();
    }

    [Serializable]
    private sealed class EntityDataRecord
    {
        public int EntityId;
        public string Name;
        public int EntityType;
        public int MaxHp;
        public string ResourcePath;
    }

    private sealed class MonsterRuntimeState
    {
        public int EntityId;
        public int AppearanceId;
        public string MonsterType = "";
        public int GroupId;
        public int MaxHp = 1;
        public long SpawnBeat;
        public string PhaseId = "P1";
        public long LockedUntilBeat = -1;
        public bool IsAlive = true;
        public int LastKnownHp;
        public float Rotation;
        public readonly Dictionary<string, long> Cooldowns = new(StringComparer.OrdinalIgnoreCase);
    }
}
