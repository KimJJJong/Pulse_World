using System.Collections.Generic;
using GameServer.InGame.Director.Data;
using RhythmRPG.Game.Stage;
using RhythmRPG.Game.Visual.SceneEffects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmRPG.Editor.StageBuilder
{
    public static class ForestFirstStepStageSetup
    {
        private const string ScenePath = "Assets/0.MainProject/Scenes/Game/Game_Forest_First_Step.unity";
        private const string StageDataPath = "Assets/Resources/Data/StageAssets/Game_Forest_First_Step.asset";
        private const string ObeliskPrefabPath = "Assets/Resources/Prefabs/Interaction/Runic_Obelisk.prefab";
        private const string TowerEntityAssetPath = "Assets/Resources/Data/Entity_502_Runic_Obelisk.asset";
        private const string TowerEntityKey = "RunicTower";
        private const int TowerEntityId = 502;
        private const string TowerTargetKey = "RunicTower";
        private const string TowerCrystalTargetKey = "RunicTowerCrystal";
        private const int TowerPhaseStateId = 100;

        private static readonly Vector3 ObeliskRotation = new(270f, 0f, 0f);
        private static readonly Vector3 ObeliskScale = new(200f, 200f, 200f);
        private static readonly Vector3 CenterCrystalPosition = new(69.66f, 1.15f, 39.44f);

        private static readonly TowerSpec[] Towers =
        {
            new("North", "Runic_Obelisk_North", new Vector3(69.66f, 0.83f, 50.40f), new Vector2Int(70, 50), "RunicTowerLink_North", 31, 101),
            new("East", "Runic_Obelisk_East", new Vector3(80.00f, 0.83f, 39.44f), new Vector2Int(80, 39), "RunicTowerLink_East", 32, 102),
            new("South", "Runic_Obelisk_South", new Vector3(69.66f, 0.83f, 28.50f), new Vector2Int(70, 29), "RunicTowerLink_South", 33, 103),
            new("West", "Runic_Obelisk_West", new Vector3(59.30f, 0.83f, 39.44f), new Vector2Int(59, 39), "RunicTowerLink_West", 34, 104)
        };

        [MenuItem("Tools/RhythmRPG/Stage/Setup Forest First Step Towers")]
        public static void Setup()
        {
            EnsureSceneLoaded();

            Transform centerCrystal = GameObject.Find("Runic_Circle_Platform/Crystal")?.transform;
            if (centerCrystal == null)
            {
                Debug.LogError("[ForestFirstStepStageSetup] Center crystal not found.");
                return;
            }

            ConfigureCenterCrystal(centerCrystal);
            EntityDefinitionSO towerEntity = EnsureRunicTowerEntityDefinition();
            ConfigureTowerPrefab();

            foreach (TowerSpec tower in Towers)
            {
                GameObject towerRoot = EnsureTowerRoot(tower);
                if (towerRoot == null)
                    continue;

                ConfigureTowerRoot(towerRoot, tower);
                ConfigureTowerCrystal(towerRoot, tower, centerCrystal);
                EditorUtility.SetDirty(towerRoot);
            }

            RemoveSceneTowerInstances();
            ConfigureStageData(towerEntity);
            EntityExporter.Export();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureSceneLoaded()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static GameObject EnsureTowerRoot(TowerSpec tower)
        {
            GameObject existing = GameObject.Find(tower.ObjectName);
            if (existing != null)
                return existing;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ObeliskPrefabPath);
            if (prefab != null)
            {
                var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance != null)
                {
                    instance.name = tower.ObjectName;
                    return instance;
                }
            }

            GameObject template = GameObject.Find(Towers[0].ObjectName) ?? GameObject.Find("Runic_Obelisk");
            if (template == null)
            {
                Debug.LogError($"[ForestFirstStepStageSetup] Cannot create {tower.ObjectName}; prefab/template missing.");
                return null;
            }

            GameObject clone = Object.Instantiate(template);
            clone.name = tower.ObjectName;
            return clone;
        }

        private static void ConfigureCenterCrystal(Transform centerCrystal)
        {
            centerCrystal.position = CenterCrystalPosition;

            var target = EnsureComponent<StageSceneObjectTarget>(centerCrystal.gameObject);
            target.TargetKey = "Crystal";
            target.GroupId = 1;
            target.DefaultDurationMs = 1400;
            target.HiddenScale = 0.88f;
            target.HiddenYOffset = -0.35f;
            target.DisableCollidersWhenHidden = false;
            target.UseWorldUpMotion = true;
            target.StartHidden = true;
            target.CurrentPoseIsHidden = false;
            target.EnableRiseFromGround = true;
            target.ReplayShowAnimationWhenAlreadyVisible = false;
            target.RiseHiddenYOffset = -2.15f;
            target.RiseOvershootHeight = 0.14f;
            target.EnableIdleFloat = true;
            target.FloatAmplitude = 0.10f;
            target.FloatPeriodSeconds = 2.6f;
            target.FloatBlendInSeconds = 0.35f;
            target.UseParabolicFloat = true;
            target.MotionRoots = System.Array.Empty<Transform>();

            EditorUtility.SetDirty(centerCrystal.gameObject);
            EditorUtility.SetDirty(target);
        }

        private static EntityDefinitionSO EnsureRunicTowerEntityDefinition()
        {
            var entity = AssetDatabase.LoadAssetAtPath<EntityDefinitionSO>(TowerEntityAssetPath);
            if (entity == null)
            {
                entity = ScriptableObject.CreateInstance<EntityDefinitionSO>();
                AssetDatabase.CreateAsset(entity, TowerEntityAssetPath);
            }

            entity.EntityId = TowerEntityId;
            entity.EntityName = "Entity_502_Runic_Obelisk";
            entity.Type = EntityType.Object;
            entity.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ObeliskPrefabPath);
            entity.AnimatorController = null;
            entity.MaxHp = 999999;
            EditorUtility.SetDirty(entity);
            return entity;
        }

        private static void ConfigureTowerPrefab()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ObeliskPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[ForestFirstStepStageSetup] Prefab missing: {ObeliskPrefabPath}");
                return;
            }

            ConfigureTowerRoot(prefabRoot, Towers[0], applyTransform: false);
            ConfigureTowerCrystal(prefabRoot, Towers[0], null);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, ObeliskPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        private static void RemoveSceneTowerInstances()
        {
            foreach (TowerSpec tower in Towers)
            {
                GameObject existing = GameObject.Find(tower.ObjectName);
                if (existing != null)
                    Object.DestroyImmediate(existing);
            }

            GameObject legacy = GameObject.Find("Runic_Obelisk");
            if (legacy != null)
                Object.DestroyImmediate(legacy);
        }

        private static void ConfigureTowerRoot(GameObject towerRoot, TowerSpec tower, bool applyTransform = true)
        {
            if (applyTransform)
                towerRoot.transform.SetPositionAndRotation(tower.Position, Quaternion.Euler(ObeliskRotation));
            else
                towerRoot.transform.localRotation = Quaternion.Euler(ObeliskRotation);

            towerRoot.transform.localScale = ObeliskScale;

            var pulse = towerRoot.GetComponent<ForestBeatLightPulse>();
            if (pulse != null)
                pulse.enabled = false;

            var target = EnsureComponent<StageSceneObjectTarget>(towerRoot);
            target.TargetKey = TowerTargetKey;
            target.GroupId = 0;
            target.BindRuntimeGroup = false;
            target.DefaultDurationMs = 1300;
            target.HiddenScale = 1f;
            target.HiddenYOffset = -0.35f;
            target.DisableCollidersWhenHidden = false;
            target.UseWorldUpMotion = true;
            target.StartHidden = true;
            target.CurrentPoseIsHidden = false;
            target.EnableRiseFromGround = true;
            target.ReplayShowAnimationWhenAlreadyVisible = false;
            target.RiseHiddenYOffset = -1.8f;
            target.RiseOvershootHeight = 0.10f;
            target.EnableIdleFloat = false;
            target.MotionRoots = System.Array.Empty<Transform>();

            var autoReveal = EnsureComponent<StageSceneObjectAutoReveal>(towerRoot);
            autoReveal.Target = target;
            autoReveal.DelayMs = 0;
            autoReveal.DurationMs = target.DefaultDurationMs;
            autoReveal.ShakeCameraOnReveal = true;
            autoReveal.ShakeDelaySeconds = 0.08f;
            autoReveal.CameraShakeDuration = 0.55f;
            autoReveal.CameraShakeStrength = 0.085f;
            autoReveal.CameraShakeFrequency = 24f;

            EditorUtility.SetDirty(autoReveal);
            EditorUtility.SetDirty(target);
        }

        private static void ConfigureTowerCrystal(GameObject towerRoot, TowerSpec tower, Transform centerCrystal)
        {
            Transform crystal = towerRoot.transform.Find("Crystal");
            if (crystal == null)
            {
                Debug.LogWarning($"[ForestFirstStepStageSetup] Crystal child missing on {towerRoot.name}.");
                return;
            }

            var crystalTarget = EnsureComponent<StageSceneObjectTarget>(crystal.gameObject);
            crystalTarget.TargetKey = TowerCrystalTargetKey;
            crystalTarget.GroupId = 0;
            crystalTarget.BindRuntimeGroup = false;
            crystalTarget.DefaultDurationMs = 950;
            crystalTarget.HiddenScale = 0.12f;
            crystalTarget.HiddenYOffset = -0.35f;
            crystalTarget.DisableCollidersWhenHidden = false;
            crystalTarget.UseWorldUpMotion = true;
            crystalTarget.StartHidden = true;
            crystalTarget.CurrentPoseIsHidden = false;
            crystalTarget.EnableRiseFromGround = true;
            crystalTarget.ReplayShowAnimationWhenAlreadyVisible = false;
            crystalTarget.RiseHiddenYOffset = -1.2f;
            crystalTarget.RiseOvershootHeight = 0.12f;
            crystalTarget.EnableIdleFloat = true;
            crystalTarget.FloatAmplitude = 0.08f;
            crystalTarget.FloatPeriodSeconds = 2.2f;
            crystalTarget.FloatBlendInSeconds = 0.35f;
            crystalTarget.UseParabolicFloat = true;
            crystalTarget.MotionRoots = System.Array.Empty<Transform>();

            var autoReveal = EnsureComponent<StageSceneObjectAutoReveal>(crystal.gameObject);
            autoReveal.Target = crystalTarget;
            autoReveal.DelayMs = 900;
            autoReveal.DurationMs = crystalTarget.DefaultDurationMs;
            autoReveal.ShakeCameraOnReveal = false;

            EditorUtility.SetDirty(autoReveal);
            EditorUtility.SetDirty(crystalTarget);

            ConfigureLinkEffect(crystal, tower, centerCrystal);
        }

        private static void ConfigureLinkEffect(Transform crystal, TowerSpec tower, Transform centerCrystal)
        {
            Transform link = crystal.Find("CrystalLinkEffect");
            if (link == null)
            {
                var linkGo = new GameObject("CrystalLinkEffect");
                link = linkGo.transform;
                link.SetParent(crystal, false);
            }

            link.gameObject.SetActive(true);
            GameObject[] linkedFxObjects = CollectCrystalFxObjects(crystal);
            foreach (GameObject linkedFxObject in linkedFxObjects)
                linkedFxObject.SetActive(false);

            Transform lightTransform = link.Find("LinkPointLight");
            if (lightTransform == null)
            {
                var lightGo = new GameObject("LinkPointLight");
                lightTransform = lightGo.transform;
                lightTransform.SetParent(link, false);
            }

            lightTransform.localPosition = Vector3.zero;
            var linkLight = EnsureComponent<Light>(lightTransform.gameObject);
            linkLight.type = LightType.Point;
            linkLight.color = new Color(0.18f, 0.92f, 0.72f, 1f);
            linkLight.range = 7.5f;
            linkLight.intensity = 1.8f;

            var line = EnsureComponent<LineRenderer>(link.gameObject);
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.08f;
            line.endWidth = 0.11f;
            line.startColor = new Color(0.18f, 0.92f, 0.72f, 0.88f);
            line.endColor = new Color(0.18f, 0.75f, 1f, 0.68f);

            var beam = EnsureComponent<StageCrystalLinkBeam>(link.gameObject);
            beam.Source = crystal;
            beam.Target = centerCrystal;
            beam.SourcePath = "Crystal";
            beam.TargetPath = "Runic_Circle_Platform/Crystal";
            beam.Line = line;
            beam.LinkLight = linkLight;
            beam.LinkedObjects = linkedFxObjects;
            beam.BeamColor = new Color(0.18f, 0.92f, 0.72f, 0.88f);
            beam.BeamWidth = 0.075f;
            beam.SourceYOffset = 0f;
            beam.TargetYOffset = 0f;
            beam.UseRendererBoundsEndpoints = true;
            beam.SegmentCount = 20;
            beam.ArcHeight = 0.42f;
            beam.DistanceArcFactor = 0.018f;
            beam.WaveAmplitude = 0.055f;
            beam.WaveFrequency = 1.35f;
            beam.PulseFrequency = 1.15f;
            beam.PulseStrength = 0.16f;

            var linkTarget = EnsureComponent<StageSceneObjectTarget>(link.gameObject);
            linkTarget.TargetKey = "RunicTowerLink";
            linkTarget.GroupId = 0;
            linkTarget.BindRuntimeGroup = true;
            linkTarget.DefaultDurationMs = 220;
            linkTarget.HiddenScale = 1f;
            linkTarget.HiddenYOffset = 0f;
            linkTarget.DisableCollidersWhenHidden = false;
            linkTarget.UseWorldUpMotion = false;
            linkTarget.StartHidden = false;
            linkTarget.CurrentPoseIsHidden = false;
            linkTarget.EnableRiseFromGround = false;
            linkTarget.EnableIdleFloat = false;
            linkTarget.MotionRoots = System.Array.Empty<Transform>();

            EditorUtility.SetDirty(link.gameObject);
            link.gameObject.SetActive(false);
        }

        private static GameObject[] CollectCrystalFxObjects(Transform sourceParent)
        {
            var fxChildren = new List<GameObject>();
            for (int i = 0; i < sourceParent.childCount; i++)
            {
                Transform child = sourceParent.GetChild(i);
                if (child.name.StartsWith("FX_Runic_Crystal_", System.StringComparison.Ordinal))
                    fxChildren.Add(child.gameObject);
            }

            return fxChildren.ToArray();
        }

        private static void ConfigureStageData(EntityDefinitionSO towerEntity)
        {
            StageDataSO stage = AssetDatabase.LoadAssetAtPath<StageDataSO>(StageDataPath);
            if (stage == null)
            {
                Debug.LogError($"[ForestFirstStepStageSetup] Stage data missing: {StageDataPath}");
                return;
            }

            EnsureTowerRegistry(stage, towerEntity);

            EventInfoSO centerEvent = EnsureEvent(stage, 4, "Section02", "Enter Center Seal");
            EnsureCenterAreaCondition(centerEvent);
            centerEvent.Actions.Clear();
            centerEvent.Actions.Add(PlayVfx(StageVfxKeys.MarkerCyan, 1200));
            centerEvent.Actions.Add(SetGateDoor(open: false, durationMs: 900));
            centerEvent.Actions.Add(SetSceneObject(string.Empty, 7, visible: true, durationMs: 900));
            AddVoidWallRespawns(centerEvent.Actions);
            centerEvent.Actions.Add(SetSceneObject("Crystal", 0, visible: true, durationMs: 1400));
            foreach (TowerSpec tower in Towers)
                centerEvent.Actions.Add(SpawnTower(tower));
            centerEvent.Actions.Add(SetObjectState(TowerPhaseStateId, 1));

            ConfigureHoldEvent(stage, 5, "Section03", "North Tower Hold", Towers[0], phase: 1);
            ConfigureHoldEvent(stage, 6, "Section03", "East Tower Hold", Towers[1], phase: 1);

            EventInfoSO unlockSecondPair = EnsureEvent(stage, 7, "Section03", "Unlock South West Tower Holds");
            unlockSecondPair.Conditions.Clear();
            unlockSecondPair.Conditions.Add(ObjectState(Towers[0].CompleteStateId, 1));
            unlockSecondPair.Conditions.Add(ObjectState(Towers[1].CompleteStateId, 1));
            unlockSecondPair.Actions.Clear();
            unlockSecondPair.Actions.Add(SetObjectState(TowerPhaseStateId, 2));

            ConfigureHoldEvent(stage, 8, "Section03", "South Tower Hold", Towers[2], phase: 2);
            ConfigureHoldEvent(stage, 9, "Section03", "West Tower Hold", Towers[3], phase: 2);

            EventInfoSO completeTowers = EnsureEvent(stage, 10, "Section03", "Tower Holds Complete");
            completeTowers.Conditions.Clear();
            completeTowers.Conditions.Add(ObjectState(Towers[2].CompleteStateId, 1));
            completeTowers.Conditions.Add(ObjectState(Towers[3].CompleteStateId, 1));
            completeTowers.Actions.Clear();
            completeTowers.Actions.Add(SetObjectState(TowerPhaseStateId, 3));

            EditorUtility.SetDirty(stage);
            StageExporter.Export(stage);
        }

        private static void EnsureTowerRegistry(StageDataSO stage, EntityDefinitionSO towerEntity)
        {
            StageRegisteredEntity registry = stage.Registry.Find(item => item != null && item.Key == TowerEntityKey);
            if (registry == null)
            {
                registry = new StageRegisteredEntity();
                stage.Registry.Add(registry);
            }

            registry.Key = TowerEntityKey;
            registry.EntityDef = towerEntity;
            registry.DefaultGroupId = 0;
            registry.PatternRef = null;
        }

        private static EventInfoSO EnsureEvent(StageDataSO stage, int eventId, string section, string title)
        {
            EventInfoSO evt = stage.Events.Find(e => e != null && e.EventId == eventId);
            if (evt == null)
            {
                evt = new EventInfoSO();
                stage.Events.Add(evt);
            }

            evt.EventId = eventId;
            evt.Section = section;
            evt.Title = title;
            evt.Enabled = true;
            evt.IsOneShot = true;
            return evt;
        }

        private static void EnsureCenterAreaCondition(EventInfoSO centerEvent)
        {
            ConditionInfoSO condition = centerEvent.Conditions.Count > 0 && centerEvent.Conditions[0] != null
                ? centerEvent.Conditions[0]
                : new ConditionInfoSO();

            if (centerEvent.Conditions.Count == 0)
                centerEvent.Conditions.Add(condition);

            condition.Type = ConditionType.AreaPlayerCount;
            condition.TargetId = 0;
            condition.SecondaryTargetId = 0;
            condition.TargetKey = string.Empty;
            condition.Count = 1;
            condition.CountRequirement = StageCountRequirementMode.FixedCount;
            condition.ShowProgressUi = true;
            condition.ShowAreaOutline = true;
            condition.ProgressLabel = "Area Count";
            condition.ProgressDurationMs = 1200;
        }

        private static void ConfigureHoldEvent(StageDataSO stage, int eventId, string section, string title, TowerSpec tower, int phase)
        {
            EventInfoSO evt = EnsureEvent(stage, eventId, section, title);
            evt.Conditions.Clear();
            evt.Conditions.Add(ObjectState(TowerPhaseStateId, phase));
            evt.Conditions.Add(HoldArea(tower, requiredBeats: 8));
            evt.Actions.Clear();
            evt.Actions.Add(SetObjectState(tower.CompleteStateId, 1));
        }

        private static ConditionInfoSO ObjectState(int targetId, int value)
        {
            return new ConditionInfoSO
            {
                Type = ConditionType.ObjectStateEquals,
                TargetId = targetId,
                Count = value
            };
        }

        private static ConditionInfoSO HoldArea(TowerSpec tower, int requiredBeats)
        {
            List<Vector2Int> cells = BuildSquareCells(tower.AreaCenter, 1);
            RectInt bounds = CalculateBounds(cells);
            return new ConditionInfoSO
            {
                Type = ConditionType.AreaHoldBeats,
                TargetId = 1,
                SecondaryTargetId = tower.LinkGroupId,
                TargetKey = tower.LinkTargetKey,
                Count = requiredBeats,
                Area = bounds,
                AreaShape = StageAreaShapeType.CustomCells,
                AreaCells = cells,
                CountRequirement = StageCountRequirementMode.FixedCount,
                ShowProgressUi = true,
                ShowAreaOutline = true,
                ProgressLabel = $"{tower.Label} Tower",
                ProgressDurationMs = 1000
            };
        }

        private static ActionInfoSO SetObjectState(int targetId, int state)
        {
            return new ActionInfoSO
            {
                Type = ActionType.SetObjectState,
                ParamId = targetId,
                GroupId = state,
                DurationMs = 3500
            };
        }

        private static ActionInfoSO SetSceneObject(string key, int groupId, bool visible, int durationMs, int delayMs = 0)
        {
            return new ActionInfoSO
            {
                Type = ActionType.SetSceneObjectActive,
                StringVal = key ?? string.Empty,
                ParamId = groupId,
                GroupId = visible ? 1 : 0,
                DurationMs = durationMs,
                Position = new Vector3(delayMs, 0f, 0f)
            };
        }

        private static ActionInfoSO SetGateDoor(bool open, int durationMs)
        {
            return new ActionInfoSO
            {
                Type = ActionType.SetGateDoorOpen,
                ParamId = 1,
                GroupId = open ? 1 : 0,
                DurationMs = durationMs
            };
        }

        private static ActionInfoSO SpawnTower(TowerSpec tower)
        {
            return new ActionInfoSO
            {
                Type = ActionType.SpawnObject,
                HeaderParam = TowerEntityKey,
                Position = new Vector3(Mathf.FloorToInt(tower.Position.x), 0f, Mathf.FloorToInt(tower.Position.z)),
                ObjectSize = new Vector2Int(2, 2),
                GroupId = tower.LinkGroupId,
                DurationMs = 3500
            };
        }

        private static ActionInfoSO PlayVfx(string key, int durationMs)
        {
            return new ActionInfoSO
            {
                Type = ActionType.PlayVfx,
                VfxKey = key,
                DurationMs = durationMs
            };
        }

        private static void AddVoidWallRespawns(List<ActionInfoSO> actions)
        {
            int[,] wallCells =
            {
                {48, 0, 41},
                {48, 0, 40},
                {48, 0, 39},
                {48, 0, 38},
                {48, 1, 37},
                {48, 0, 36},
                {48, 0, 35},
                {48, 0, 43},
                {48, 0, 42}
            };

            for (int i = 0; i < wallCells.GetLength(0); i++)
            {
                actions.Add(new ActionInfoSO
                {
                    Type = ActionType.SpawnObject,
                    HeaderParam = "VoidWall",
                    Position = new Vector3(wallCells[i, 0], wallCells[i, 1], wallCells[i, 2]),
                    ObjectSize = Vector2Int.one,
                    GroupId = 7,
                    DurationMs = 3500
                });
            }
        }

        private static List<Vector2Int> BuildSquareCells(Vector2Int center, int radius)
        {
            var cells = new List<Vector2Int>();
            for (int y = center.y - radius; y <= center.y + radius; y++)
            {
                for (int x = center.x - radius; x <= center.x + radius; x++)
                    cells.Add(new Vector2Int(x, y));
            }
            return cells;
        }

        private static RectInt CalculateBounds(List<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0)
                return new RectInt(0, 0, 1, 1);

            int minX = cells[0].x;
            int maxX = cells[0].x;
            int minY = cells[0].y;
            int maxY = cells[0].y;
            foreach (Vector2Int cell in cells)
            {
                minX = Mathf.Min(minX, cell.x);
                maxX = Mathf.Max(maxX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxY = Mathf.Max(maxY, cell.y);
            }

            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private readonly struct TowerSpec
        {
            public readonly string Label;
            public readonly string ObjectName;
            public readonly Vector3 Position;
            public readonly Vector2Int AreaCenter;
            public readonly string LinkTargetKey;
            public readonly int LinkGroupId;
            public readonly int CompleteStateId;

            public TowerSpec(string label, string objectName, Vector3 position, Vector2Int areaCenter, string linkTargetKey, int linkGroupId, int completeStateId)
            {
                Label = label;
                ObjectName = objectName;
                Position = position;
                AreaCenter = areaCenter;
                LinkTargetKey = linkTargetKey;
                LinkGroupId = linkGroupId;
                CompleteStateId = completeStateId;
            }
        }
    }
}
