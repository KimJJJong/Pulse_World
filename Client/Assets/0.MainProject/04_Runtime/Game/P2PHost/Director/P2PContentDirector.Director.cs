using System;
using System.Collections.Generic;
using System.Linq;
using GameServer.InGame.Director.Core;
using GameServer.InGame.Director.Data;
using StageEventType = GameServer.InGame.Director.Core.EventType;
using StageScenarioData = GameServer.InGame.Director.Data.StageScenario;
using UnityEngine;

public sealed partial class P2PContentDirector
{
    private void LoadStageContent()
    {
        if (string.IsNullOrWhiteSpace(_mapId) || _stageLoaded || string.Equals(_lastStageLoadAttemptMapId, _mapId, StringComparison.OrdinalIgnoreCase))
            return;

        _lastStageLoadAttemptMapId = _mapId;

        LoadEntityTemplateData();
        LoadPatternData();

        try
        {
            string jsonText = null;
            if (P2PServerContentResolver.TryLoadStageJson(_mapId, out var serverStageJson))
            {
                jsonText = serverStageJson;
                if (P2PDebugConfig.TraceContent)
                    Debug.Log($"[P2PContentDirector] Using server stage json: {_mapId}");
            }
            else
            {
                var textAsset = Resources.Load<TextAsset>($"Data/Stage/{_mapId}");
                if (textAsset == null)
                {
                    Debug.LogWarning($"[P2PContentDirector] Stage json not found: Data/Stage/{_mapId}");
                    return;
                }

                jsonText = textAsset.text;
            }

            _stage = JsonUtility.FromJson<StageScenarioData>(jsonText);
            if (_stage == null)
            {
                Debug.LogWarning($"[P2PContentDirector] Failed to parse stage json: {_mapId}");
                return;
            }

            BuildAppearanceTemplateIndex(_stage);
            BuildStageObjectIndex(_stage);
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

    private void ResetStageRuntime()
    {
        _stage = null;
        _stageLoaded = false;
        _lastStageLoadAttemptMapId = "";
        _templatesByAppearanceId.Clear();
        _templatesByAppearanceAndGroup.Clear();
        _templatesBySpawnSignature.Clear();
        _monsterStates.Clear();
        _stageObjectGroupByEntityId.Clear();
        _objectStatesByTargetId.Clear();
        _distanceFields.Clear();
        _moveReservationsByBeat.Clear();
        _expiredReservationBeats.Clear();
        _lastReservationCleanupBeat = long.MinValue;
        _stageEngine.Reset();
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

        bool loadedFromServer = false;
        foreach (var jsonText in P2PServerContentResolver.EnumeratePatternJsonTexts())
        {
            try
            {
                var set = JsonUtility.FromJson<MonsterPatternSet>(jsonText);
                if (set?.Monsters != null && set.Monsters.Count > 0)
                {
                    foreach (var pattern in set.Monsters)
                        CachePattern(pattern);
                    loadedFromServer = true;
                    continue;
                }

                var patternDef = JsonUtility.FromJson<MonsterPatternDef>(jsonText);
                CachePattern(patternDef);
                loadedFromServer = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[P2PContentDirector] Server pattern parse failed: {ex.Message}");
            }
        }

        if (!loadedFromServer)
        {
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
        _templatesByAppearanceAndGroup.Clear();
        _templatesBySpawnSignature.Clear();

        if (stage == null)
            return;

        foreach (var spawn in stage.InitialSpawns ?? new List<SpawnData>())
            RegisterTemplate(spawn.MonsterId, ResolveSpawnPattern(spawn), spawn.GroupId, spawn.X, ResolveMapY(spawn.Y, spawn.Z));
    }

    private void BuildStageObjectIndex(StageScenarioData stage)
    {
        _stageObjectGroupByEntityId.Clear();

        if (stage?.InitialObjects == null)
            return;

        foreach (var obj in stage.InitialObjects)
        {
            if (obj == null || obj.EntityId <= 0 || obj.GroupId <= 0)
                continue;

            _stageObjectGroupByEntityId[obj.EntityId] = obj.GroupId;
        }
    }

    private void RegisterTemplate(int appearanceId, string monsterType, int groupId, int x = int.MinValue, int y = int.MinValue)
    {
        if (appearanceId <= 0)
            return;

        if (!_entityMaxHpByTemplateId.TryGetValue(appearanceId, out var maxHp))
            maxHp = 50;

        var template = new StageMonsterTemplate
        {
            AppearanceId = appearanceId,
            MonsterType = string.IsNullOrWhiteSpace(monsterType) ? "Default" : monsterType,
            GroupId = groupId,
            MaxHp = Math.Max(1, maxHp)
        };

        if (groupId > 0)
            _templatesByAppearanceAndGroup[BuildTemplateKey(appearanceId, groupId)] = template;

        if (groupId > 0 && x != int.MinValue && y != int.MinValue)
            _templatesBySpawnSignature[BuildTemplateKey(appearanceId, groupId, x, y)] = template;

        if (!_templatesByAppearanceId.ContainsKey(appearanceId) || groupId <= 0)
            _templatesByAppearanceId[appearanceId] = template;
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
        if (!TryResolveMonsterTemplate(entity.AppearanceId, entity.GroupId, entity.X, entity.Y, out var template))
            return;

        if (!_monsterStates.TryGetValue(entity.EntityId, out var state))
        {
            state = new MonsterRuntimeState();
            _monsterStates[entity.EntityId] = state;
        }

        bool firstBind = state.GroupId <= 0 && state.MaxHp <= 0 && string.IsNullOrWhiteSpace(state.MonsterType);

        state.EntityId = entity.EntityId;
        state.AppearanceId = entity.AppearanceId;
        int groupId = entity.GroupId > 0 ? entity.GroupId : template.GroupId;
        if (groupId > 0)
            state.GroupId = groupId;

        if (!string.IsNullOrWhiteSpace(template.MonsterType))
            state.MonsterType = template.MonsterType;
        else if (string.IsNullOrWhiteSpace(state.MonsterType))
            state.MonsterType = "Default";

        if (state.MaxHp <= 0)
            state.MaxHp = template.MaxHp;

        if (firstBind && spawnBeat.HasValue)
            state.SpawnBeat = spawnBeat.Value;

        state.IsAlive = entity.Hp > 0;
        state.LastKnownHp = entity.Hp;
        state.Rotation = entity.Rotation;
    }

    public void PrepareHostBeat(long beat)
    {
        EnsureStageLoaded();
        if (!_stageLoaded || ClientGameState.Instance == null)
            return;

        if (_bindingsDirty)
        {
            SyncBindingsFromWorld();
            _bindingsDirty = false;
        }

        RunMonsterAI(beat);

        if (P2PDebugConfig.TraceContent)
            Debug.Log($"[P2PContentDirector] PrepareHostBeat beat={beat} monsters={_monsterStates.Count}");
    }

    public void FinalizeHostBeat(long beat)
    {
        EnsureStageLoaded();
        if (!_stageLoaded)
            return;

        long serverNow = TimeSync.ServerNowMs();
        _lastProcessedBeat = beat;
        _stageEngine.NotifyEvent(new GameEventContext(StageEventType.Beat, timeMs: serverNow), this);
        _stageEngine.NotifyEvent(new GameEventContext(StageEventType.TimeTick, timeMs: serverNow), this);

        if (P2PHostController.HasInstance && ShouldAutoSubmitClearOnMonsterWipe())
            P2PHostController.Instance.CheckAndSubmitGameResultIfCleared();

        if (P2PDebugConfig.TraceContent)
            Debug.Log($"[P2PContentDirector] FinalizeHostBeat beat={beat} timeMs={serverNow}");
    }

    public int GetDeadMonsterCount(int groupId) => CountDeadMonsters(groupId);

    public long GetElapsedTimeMs()
    {
        if (_stageLoadServerTimeMs <= 0)
            return 0;

        return Math.Max(0, TimeSync.ServerNowMs() - _stageLoadServerTimeMs);
    }

    public int GetObjectState(int targetId)
    {
        if (_objectStatesByTargetId.TryGetValue(targetId, out int state))
            return state;

        if (ClientGameState.Instance != null
            && ClientGameState.Instance.TryGetEntity(targetId, out var entity)
            && entity.EntityType == (int)EntityType.Object)
        {
            return entity.Hp;
        }

        return 0;
    }

    public void SetObjectState(int targetId, int state)
    {
        if (targetId <= 0)
            return;

        _objectStatesByTargetId[targetId] = state;

        int x = 0;
        int y = 0;
        float rotation = 0f;
        if (ClientGameState.Instance != null && ClientGameState.Instance.TryGetEntity(targetId, out var entity))
        {
            x = entity.X;
            y = entity.Y;
            rotation = entity.Rotation;
        }

        long beat = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : 0;
        var pkt = new SC_BeatActions
        {
            BeatIndex = beat,
            beatActionResults = new List<SC_BeatActions.BeatActionResult>
            {
                new SC_BeatActions.BeatActionResult
                {
                    ActorId = targetId,
                    ActionKind = (int)ActionKind.Interact,
                    FromX = x,
                    FromY = y,
                    ToX = x,
                    ToY = y,
                    Rotation = rotation,
                    Accepted = true,
                    hpUpdates = new List<SC_BeatActions.BeatActionResult.HpUpdate>
                    {
                        new SC_BeatActions.BeatActionResult.HpUpdate
                        {
                            EntityId = targetId,
                            NewHp = state
                        }
                    }
                }
            }
        };

        if (P2PHostController.HasInstance)
            P2PHostController.Instance.SendLocalAndRelay(pkt);
    }

    public void NotifyObjectInteracted(int actorId, ClientEntityInfo targetInfo)
    {
        EnsureStageLoaded();
        if (!_stageLoaded)
            return;

        int stageTargetId = ResolveStageObjectTargetId(targetInfo);
        _objectStatesByTargetId[stageTargetId] = GetObjectState(stageTargetId) + 1;

        _stageEngine.NotifyEvent(new GameEventContext(
            StageEventType.Interact,
            sourceActorId: actorId,
            targetId: stageTargetId,
            x: targetInfo.X,
            y: targetInfo.Y,
            timeMs: TimeSync.ServerNowMs()),
            this);

        if (P2PDebugConfig.TraceContent)
            Debug.Log($"[P2PContentDirector] StageInteract actor={actorId} targetEntity={targetInfo.EntityId} stageTarget={stageTargetId} pos=({targetInfo.X},{targetInfo.Y})");
    }

    private int ResolveStageObjectTargetId(ClientEntityInfo targetInfo)
    {
        if (targetInfo.GroupId > 0)
            return targetInfo.GroupId;

        if (_stageObjectGroupByEntityId.TryGetValue(targetInfo.EntityId, out int groupId) && groupId > 0)
            return groupId;

        return targetInfo.EntityId;
    }

    public void SpawnMonster(SpawnData data)
    {
        if (data == null)
            return;

        string monsterType = ResolveSpawnPattern(data);
        if (string.IsNullOrWhiteSpace(monsterType) || string.Equals(monsterType, "Default", StringComparison.OrdinalIgnoreCase))
            monsterType = ResolveMonsterType(data.MonsterId, data.GroupId);

        int maxHp = ResolveMaxHp(data.MonsterId);
        int entityId = GenerateSpawnEntityId();
        long beat = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : 0;
        float rotation = data.Rotation;

        RegisterRuntimeMonster(entityId, data.MonsterId, monsterType, data.GroupId, maxHp, beat, rotation);

        var pkt = new SC_EntitySpawn
        {
            BeatIndex = beat,
            EntityId = entityId,
            EntityType = (int)EntityType.Monster,
            X = data.X,
            Y = ResolveMapY(data.Y, data.Z),
            Hp = maxHp,
            AppearanceId = data.MonsterId,
            Rotation = rotation
        };

        if (P2PDebugConfig.TraceContent)
            Debug.Log($"[P2PContentDirector] SpawnMonster entity={entityId} appearance={data.MonsterId} type={monsterType} group={data.GroupId} pos=({data.X},{ResolveMapY(data.Y, data.Z)}) rot={rotation:F0} hp={maxHp}");

        P2PHostController.Instance.SendLocalAndRelay(pkt);
    }

    public void SpawnObject(SpawnObjectData data)
    {
        if (data == null)
            return;

        int entityId = GenerateSpawnEntityId();
        if (entityId > 0 && data.GroupId > 0)
            _stageObjectGroupByEntityId[entityId] = data.GroupId;

        long beat = RhythmClient.Instance != null ? RhythmClient.Instance.GetCurrentBeatIndex() : 0;
        var pkt = new SC_EntitySpawn
        {
            BeatIndex = beat,
            EntityId = entityId,
            EntityType = data.EntityType <= 0 ? (int)EntityType.Object : data.EntityType,
            X = data.X,
            Y = ResolveMapY(data.Y, data.Z),
            Hp = 1,
            AppearanceId = data.EntityId,
            Rotation = data.Rotation
        };

        if (P2PDebugConfig.TraceContent)
            Debug.Log($"[P2PContentDirector] SpawnObject entity={entityId} appearance={data.EntityId} type={pkt.EntityType} group={data.GroupId} pos=({data.X},{pkt.Y})");

        P2PHostController.Instance.SendLocalAndRelay(pkt);
    }

    public new void BroadcastMessage(string msg)
    {
        if (P2PDebugConfig.TraceContent)
            Debug.Log($"[P2PContentDirector] Broadcast: {msg}");

        ShowGuide(new StageGuideData
        {
            Body = msg ?? string.Empty,
            DurationMs = 3000
        });
    }

    public void ShowGuide(StageGuideData data)
    {
        data ??= new StageGuideData();

        if (P2PHostController.HasInstance)
        {
            P2PHostController.Instance.SendLocalAndRelay(new SC_Warn
            {
                code = StageSignalCodec.GuideWarnCode,
                msg = StageSignalCodec.EncodeGuide(data)
            });
            return;
        }

        StageGuideHud.Show(data);
    }

    public void ShowTutorialPanel(StageTutorialPanelData data)
    {
        SendTutorialPanel(data, visible: true);
    }

    public void HideTutorialPanel(StageTutorialPanelData data)
    {
        SendTutorialPanel(data, visible: false);
    }

    private void SendTutorialPanel(StageTutorialPanelData data, bool visible)
    {
        data ??= new StageTutorialPanelData();
        data.Visible = visible;

        if (P2PHostController.HasInstance)
        {
            P2PHostController.Instance.SendLocalAndRelay(new SC_Warn
            {
                code = StageSignalCodec.TutorialPanelWarnCode,
                msg = StageSignalCodec.EncodeTutorialPanel(data)
            });
            return;
        }

        if (visible)
            StageTutorialPanelHud.Show(data);
        else
            StageTutorialPanelHud.Hide(data);
    }

    public void PlayStageVfx(StageVfxData data)
    {
        data ??= new StageVfxData();

        if (P2PHostController.HasInstance)
        {
            P2PHostController.Instance.SendLocalAndRelay(new SC_Warn
            {
                code = StageSignalCodec.VfxWarnCode,
                msg = StageSignalCodec.EncodeVfx(data)
            });
            return;
        }

        StageGuideHud.PlayVfx(data);
    }

    public void ReturnToTown()
    {
        if (P2PHostController.HasInstance)
        {
            P2PHostController.Instance.SubmitStageClearResult("StageAction:ReturnToTown");
            return;
        }

        if (P2PDebugConfig.TraceContent)
            Debug.LogWarning("[P2PContentDirector] ReturnToTown requested without host controller instance.");
    }

    public void FinGame()
    {
        StageClearResultData data = BuildStageClearResultData();

        if (P2PHostController.HasInstance)
        {
            P2PHostController.Instance.SendLocalAndRelay(new SC_Warn
            {
                code = StageSignalCodec.StageClearWarnCode,
                msg = StageSignalCodec.EncodeStageClear(data)
            });
            return;
        }

        StageClearResultHud.Show(data);
    }

    public void OpenGate(int x, int y)
    {
        ClientGameState.Instance?.SetTile(x, y, (int)TileKind.Floor);
        if (P2PDebugConfig.TraceContent)
            Debug.Log($"[P2PContentDirector] OpenGate at ({x},{y})");
    }

    private StageClearResultData BuildStageClearResultData()
    {
        long startMs = 0;
        if (SessionContext.Instance != null && SessionContext.Instance.LastInitMap != null)
            startMs = SessionContext.Instance.LastInitMap.SongStartServerTime;

        if (startMs <= 0 && RhythmClient.Instance != null)
            startMs = RhythmClient.Instance.ServerSongStartMs;

        long clearTimeMs = startMs > 0 ? Math.Max(0, TimeSync.ServerNowMs() - startMs) : 0;

        return new StageClearResultData
        {
            MapId = _mapId ?? string.Empty,
            Title = "STAGE CLEAR",
            Subtitle = "Purification Complete - Deepwood Gate Stabilized",
            Rank = "S",
            ClearTimeMs = (int)Math.Min(int.MaxValue, clearTimeMs),
            RhythmSyncPercent = 98,
            MaxCombo = 842,
            Misses = 4,
            EchoNodesRestored = 4,
            EchoNodesTotal = 4,
            CorruptionResidue = "Low",
            GateStabilityPercent = 92,
            HiddenEchoFound = 1,
            FirstClearBonusActive = true,
            RouteUnlocked = "Deepwood Gate",
            NextArea = "Deepwood Gate",
            RecommendedLevel = 12,
            DangerRhythm = "Normal"
        };
    }

    private void RegisterRuntimeMonster(int entityId, int appearanceId, string monsterType, int groupId, int maxHp, long beat, float rotation = 0f)
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
        state.Rotation = rotation;

        var template = new StageMonsterTemplate
        {
            AppearanceId = appearanceId,
            MonsterType = state.MonsterType,
            GroupId = groupId,
            MaxHp = state.MaxHp
        };

        if (groupId > 0)
            _templatesByAppearanceAndGroup[BuildTemplateKey(appearanceId, groupId)] = template;

        _templatesByAppearanceId[appearanceId] = template;
    }

    private const int AiBucketCount = 3;  // [최적화] AI 버킷 분산 — Beat마다 1/N 몬스터만 처리

    private void RunMonsterAI(long beat)
    {
        CleanupMoveReservations(beat);

        int bucketIndex = (int)(beat % AiBucketCount);

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

            // [최적화] 버킷 분산 — 이번 Beat 담당 버킷이 아니면 스킵
            // 단, LockedUntilBeat가 이번 Beat에 만료된 경우는 반드시 처리
            bool isBucketMatch  = (state.EntityId % AiBucketCount) == bucketIndex;
            bool isJustUnlocked = (state.LockedUntilBeat == beat);
            if (!isBucketMatch && !isJustUnlocked)
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
                if (selector == null || IsSelectorOnCooldown(state, selector, beat) || !IsSelectorEligible(selector, state, beat))
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

            if (!string.Equals(transition.FromPhaseId, state.PhaseId, StringComparison.OrdinalIgnoreCase) || !IsTransitionMet(transition, state, beat))
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

        return transition.Type switch
        {
            PhaseTransitionType.HpPercentLE => state.MaxHp > 0 && entity.Hp * 100 <= state.MaxHp * transition.Value,
            PhaseTransitionType.TimeSinceSpawnBeatsGE => beat - state.SpawnBeat >= transition.Value,
            _ => false
        };
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
            if (condition != null && !IsConditionPassed(condition, self))
                return false;
        }

        return true;
    }

    private bool IsConditionPassed(ConditionDef condition, ClientEntityInfo self)
    {
        if (condition == null)
            return false;

        if (condition.Type != ConditionType.DistanceToClosestPlayerLE && condition.Type != ConditionType.DistanceToClosestPlayerGT)
            return true;

        if (!TryFindClosestPlayer(self, out var target, out int distance))
            return false;

        return condition.Type switch
        {
            ConditionType.DistanceToClosestPlayerLE => distance <= condition.Value,
            ConditionType.DistanceToClosestPlayerGT => distance > condition.Value,
            _ => true
        };
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

                        var nextPos = ResolvePathToward(
                            plannedPos,
                            new Vector2Int(target.Value.X, target.Value.Y),
                            Math.Max(1, action.MoveDistance),
                            state.EntityId,
                            executeBeat);

                        float rotation = CalculateRotation(plannedPos, nextPos, plannedRotation);
                        if (P2PDebugConfig.TraceContent)
                            Debug.Log($"[P2PContentDirector] AIScheduleMove actor={state.EntityId} beat={executeBeat} from=({plannedPos.x},{plannedPos.y}) to=({nextPos.x},{nextPos.y}) mode=MoveStepToward");
                        P2PHostController.Instance.EnqueueAiAction(state.EntityId, ActionKind.Move, nextPos.x, nextPos.y, rotation, "", executeBeat);
                        ReserveMoveTarget(executeBeat, state.EntityId, nextPos, plannedPos);

                        plannedPos = nextPos;
                        plannedRotation = rotation;
                        lastBeat = Math.Max(lastBeat, executeBeat + 1);
                        break;
                    }

                case ActionType.Move:
                    {
                        Vector2Int nextPos = action.MoveDirection != MoveDirection.None
                            ? ResolveMoveDirection(plannedPos, state, action, executeBeat)
                            : ResolveMoveStrategy(plannedPos, state, action, executeBeat);

                        if (nextPos != plannedPos && !CanEnterMoveTarget(state.EntityId, executeBeat, nextPos.x, nextPos.y))
                            nextPos = plannedPos;

                        float rotation = CalculateRotation(plannedPos, nextPos, plannedRotation);
                        if (P2PDebugConfig.TraceContent)
                            Debug.Log($"[P2PContentDirector] AIScheduleMove actor={state.EntityId} beat={executeBeat} from=({plannedPos.x},{plannedPos.y}) to=({nextPos.x},{nextPos.y}) mode=Move");
                        P2PHostController.Instance.EnqueueAiAction(state.EntityId, ActionKind.Move, nextPos.x, nextPos.y, rotation, "", executeBeat);
                        ReserveMoveTarget(executeBeat, state.EntityId, nextPos, plannedPos);

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
                        float rotation = action.LockRotation
                            ? plannedRotation
                            : CalculateRotation(plannedPos, targetPos, plannedRotation);

                        string skillId = string.IsNullOrWhiteSpace(action.SkillId) ? "Attack" : action.SkillId;
                        var skillDef = P2PCombatContentCache.GetSkillDefinition(skillId);
                        int skillDurationBeats = Math.Max(1, ((skillDef?.TotalDurationTicks ?? 480) + 479) / 480);

                        P2PHostController.Instance.EnqueueAiAction(
                            state.EntityId,
                            ActionKind.Skill,
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

    private Vector2Int ResolveMoveDirection(Vector2Int from, MonsterRuntimeState state, ActionDef action, long executeBeat)
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
                        next = target != null
                            ? ResolvePathToward(pos, new Vector2Int(target.Value.X, target.Value.Y), 1, state.EntityId, executeBeat)
                            : pos;
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

            if (next != pos && !CanEnterMoveTarget(state.EntityId, executeBeat, next.x, next.y))
                break;

            pos = next;
        }

        return pos;
    }

    private Vector2Int ResolveMoveStrategy(Vector2Int from, MonsterRuntimeState state, ActionDef action, long executeBeat)
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

                    candidates = candidates.Where(c => CanEnterMoveTarget(state.EntityId, executeBeat, c.x, c.y)).ToList();
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
                    Vector2Int next;
                    if (Math.Abs(dx) >= Math.Abs(dy))
                        next = new Vector2Int(from.x + Math.Sign(dx) * dist, from.y);
                    else
                        next = new Vector2Int(from.x, from.y + Math.Sign(dy) * dist);

                    return CanEnterMoveTarget(state.EntityId, executeBeat, next.x, next.y) ? next : from;
                }

            case MoveStrategy.Forward:
                {
                    var target = FindClosestPlayerEntity(state, out _);
                    if (target == null)
                        return from;

                    return ResolvePathToward(from, new Vector2Int(target.Value.X, target.Value.Y), dist, state.EntityId, executeBeat);
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

                    var next = new Vector2Int(from.x + sx * dist, from.y + sy * dist);
                    return CanEnterMoveTarget(state.EntityId, executeBeat, next.x, next.y) ? next : from;
                }
        }

        return from;
    }

    private Vector2Int ResolvePathToward(Vector2Int from, Vector2Int target, int distance, int actorId, long executeBeat)
    {
        if (from == target)
            return from;

        var field = GetDistanceField(target);
        if (field.GetDistance(from.x, from.y) == GridDistanceField.Unreachable)
            return from;

        return field.TryStepToward(
            new GridPos(from.x, from.y),
            Math.Max(1, distance),
            (x, y) => CanEnterMoveTarget(actorId, executeBeat, x, y),
            out var next)
            ? new Vector2Int(next.X, next.Y)
            : from;
    }

    private GridDistanceField GetDistanceField(Vector2Int target)
    {
        var key = (target.x, target.y);
        if (_distanceFields.TryGetValue(key, out var field))
            return field;

        var state = ClientGameState.Instance;
        int width = state != null ? state.MapWidth : 0;
        int height = state != null ? state.MapHeight : 0;

        field = new GridDistanceField(width, height);
        field.Build(new[] { new GridPos(target.x, target.y) }, (x, y) => state != null && state.IsWalkable(x, y));
        _distanceFields[key] = field;
        return field;
    }

    private bool CanEnterMoveTarget(int actorId, long executeBeat, int x, int y)
    {
        var state = ClientGameState.Instance;
        if (state == null || !state.IsWalkable(x, y))
            return false;

        if (IsMoveTargetReservedByOther(executeBeat, actorId, x, y))
            return false;

        return !state.IsOccupied(x, y, actorId);
    }

    private bool IsMoveTargetReservedByOther(long executeBeat, int actorId, int x, int y)
    {
        return _moveReservationsByBeat.TryGetValue(executeBeat, out var reservations)
               && reservations.TryGetValue((x, y), out int reservedBy)
               && reservedBy != actorId;
    }

    private void ReserveMoveTarget(long executeBeat, int actorId, Vector2Int target, Vector2Int from)
    {
        if (target == from)
            return;

        if (!_moveReservationsByBeat.TryGetValue(executeBeat, out var reservations))
        {
            reservations = new Dictionary<(int x, int y), int>();
            _moveReservationsByBeat[executeBeat] = reservations;
        }

        reservations[(target.x, target.y)] = actorId;
    }

    private void CleanupMoveReservations(long beat)
    {
        if (_lastReservationCleanupBeat == beat)
            return;

        _lastReservationCleanupBeat = beat;
        _expiredReservationBeats.Clear();

        foreach (var reservedBeat in _moveReservationsByBeat.Keys)
        {
            if (reservedBeat < beat)
                _expiredReservationBeats.Add(reservedBeat);
        }

        foreach (var reservedBeat in _expiredReservationBeats)
            _moveReservationsByBeat.Remove(reservedBeat);
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
                        if (entity.Hp >= minHp)
                            continue;

                        minHp = entity.Hp;
                        best = entity;
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

        // [최적화] LINQ OrderBy 제거 → 수동 최솟값 탐색 (GC 할당 없음)
        ClientEntityInfo? closest = null;
        int minDist = int.MaxValue;
        foreach (var e in playerCandidates)
        {
            int d = Math.Abs(e.X - self.X) + Math.Abs(e.Y - self.Y);
            if (d >= minDist) continue;
            minDist = d;
            closest = e;
        }
        if (closest == null) return null;
        distance = minDist;
        return closest;
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

    private string ResolveMonsterType(int appearanceId, int groupId = 0)
    {
        if (TryResolveMonsterTemplate(appearanceId, groupId, out var template)
            && !string.IsNullOrWhiteSpace(template.MonsterType))
        {
            return template.MonsterType;
        }

        return "Default";
    }

    private bool TryResolveMonsterTemplate(int appearanceId, int groupId, out StageMonsterTemplate template)
        => TryResolveMonsterTemplate(appearanceId, groupId, int.MinValue, int.MinValue, out template);

    private bool TryResolveMonsterTemplate(int appearanceId, int groupId, int x, int y, out StageMonsterTemplate template)
    {
        if (appearanceId > 0 && groupId > 0 && x != int.MinValue && y != int.MinValue &&
            _templatesBySpawnSignature.TryGetValue(BuildTemplateKey(appearanceId, groupId, x, y), out template))
        {
            return true;
        }

        if (appearanceId > 0 && groupId > 0 &&
            _templatesByAppearanceAndGroup.TryGetValue(BuildTemplateKey(appearanceId, groupId), out template))
        {
            return true;
        }

        if (_templatesByAppearanceId.TryGetValue(appearanceId, out template) && !string.IsNullOrWhiteSpace(template.MonsterType))
        {
            return true;
        }

        template = default;
        return false;
    }

    private static string BuildTemplateKey(int appearanceId, int groupId)
        => $"{appearanceId}:{groupId}";

    private static string BuildTemplateKey(int appearanceId, int groupId, int x, int y)
        => $"{appearanceId}:{groupId}:{x}:{y}";

    private static string ResolveSpawnPattern(SpawnData data)
    {
        if (data == null)
            return "Default";

        string pattern = data.ResolvePatternKey();
        if (!string.IsNullOrWhiteSpace(pattern))
            return pattern;

        if (!string.IsNullOrWhiteSpace(data.AI))
            return data.AI;

        if (!string.IsNullOrWhiteSpace(data.Pattern))
            return data.Pattern;

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

        if (dy > 0)
            return 0f;
        if (dy < 0)
            return 180f;
        if (dx > 0)
            return 90f;
        if (dx < 0)
            return 270f;

        return current;
    }
}
