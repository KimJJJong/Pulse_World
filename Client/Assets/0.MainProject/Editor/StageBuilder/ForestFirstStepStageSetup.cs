using System.Collections.Generic;
using System.IO;
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
        private const string NormalSummonRingEntityAssetPath = "Assets/Resources/Data/Entity_503_StageSummon_MossyStoneRing.asset";
        private const string EliteSummonGateEntityAssetPath = "Assets/Resources/Data/Entity_504_StageSummon_RunicStoneGate.asset";
        private const string NormalSummonRingEntityKey = "StageSummonRing";
        private const string EliteSummonGateEntityKey = "StageEliteSummonGate";
        private const int NormalSummonRingEntityId = 503;
        private const int EliteSummonGateEntityId = 504;
        private const string TowerTargetKey = "RunicTower";
        private const string TowerCrystalTargetKey = "RunicTowerCrystal";
        private const int TowerPhaseStateId = 100;
        private const string SummonPortalRootName = "Section2_SummonPortals";
        private const string SummonSpawnPointRootName = "Section2_SummonSpawnPoints";
        private const string SummonPortalPrefabFolder = "Assets/Resources/Prefabs/StageSummons";
        private const string NormalSummonRingPrefabPath = SummonPortalPrefabFolder + "/PF_StageSummon_MossyStoneRing.prefab";
        private const string EliteSummonGatePrefabPath = SummonPortalPrefabFolder + "/PF_StageSummon_RunicStoneGate.prefab";
        private const string NormalSummonRingEntityPrefabPath = SummonPortalPrefabFolder + "/PF_Entity_StageSummon_MossyStoneRing.prefab";
        private const string EliteSummonGateEntityPrefabPath = SummonPortalPrefabFolder + "/PF_Entity_StageSummon_RunicStoneGate.prefab";
        private const string NormalSummonTargetKey = "Section2_NormalSummonRing";
        private const string EliteSummonTargetKey = "Section2_EliteSummonGate";
        private const string BatSummonMonsterIdsCsv = "1010";
        private const string BatSummonMonsterPatternCsv = "Enemy_Bat";
        private const string EvilMageSummonMonsterIdsCsv = "1012";
        private const string EvilMageSummonMonsterPatternCsv = "Enemy_EvilMage";
        private const string SpecterSummonMonsterIdsCsv = "1027";
        private const string SpecterSummonMonsterPatternCsv = "Enemy_Specter";
        private const string MixedSummonMonsterIdsCsv = "1010,1012,1027";
        private const string MixedSummonMonsterPatternCsv = "Enemy_Bat,Enemy_EvilMage,Enemy_Specter";
        private const string LegacyFirstPhaseSummonMonsterIdsCsv = "1010,1012";
        private const string LegacyFirstPhaseSummonMonsterPatternCsv = "Enemy_Bat,Enemy_EvilMage";
        private const int EliteGateSceneGroupId = 2190;
        private const int BatSummonMaxAlive = 2;
        private const int SingleSummonMaxAlive = 1;
        private const int NormalSummonMaxAlive = BatSummonMaxAlive;
        private const int NormalSummonIntervalBeats = 15;
        private const int TowerHoldAreaRadius = 2;

        private static readonly Vector3 ObeliskRotation = new(270f, 0f, 0f);
        private static readonly Vector3 ObeliskScale = new(200f, 200f, 200f);
        private static readonly Vector3 CenterCrystalPosition = new(69.66f, 2.35f, 39.44f);
        private static readonly Vector3 EliteGatePosition = new(69.66f, 5.95f, 39.44f);
        private static readonly Vector3 EliteGateRotation = new(294.648254f, 270f, 180f);
        private static readonly Vector3 EliteGateScale = new(180.43f, 180.43f, 180.43f);

        private static readonly TowerSpec[] Towers =
        {
            new("North", "Runic_Obelisk_North", new Vector3(69.66f, 0.83f, 53.20f), new Vector2Int(70, 53), "RunicTowerLink_North", 31, 101),
            new("East", "Runic_Obelisk_East", new Vector3(82.80f, 0.83f, 39.44f), new Vector2Int(83, 39), "RunicTowerLink_East", 32, 102),
            new("South", "Runic_Obelisk_South", new Vector3(69.66f, 0.83f, 25.70f), new Vector2Int(70, 26), "RunicTowerLink_South", 33, 103),
            new("West", "Runic_Obelisk_West", new Vector3(56.50f, 0.83f, 39.44f), new Vector2Int(57, 39), "RunicTowerLink_West", 34, 104)
        };

        private static readonly SummonRingSpec[] SummonRings =
        {
            new("North", "Section2_NormalSummonRing_North", new Vector3(78.70f, 0.06f, 48.35f), new Vector3Int(79, 0, 48), 2101, 2201, BatSummonMaxAlive, NormalSummonIntervalBeats, 2, BatSummonMonsterIdsCsv, BatSummonMonsterPatternCsv),
            new("East", "Section2_NormalSummonRing_East", new Vector3(78.70f, 0.06f, 30.55f), new Vector3Int(79, 0, 31), 2102, 2202, SingleSummonMaxAlive, NormalSummonIntervalBeats, 4, EvilMageSummonMonsterIdsCsv, EvilMageSummonMonsterPatternCsv),
            new("South", "Section2_NormalSummonRing_South", new Vector3(60.65f, 0.06f, 30.55f), new Vector3Int(61, 0, 31), 2103, 2203, SingleSummonMaxAlive, NormalSummonIntervalBeats, 2, SpecterSummonMonsterIdsCsv, SpecterSummonMonsterPatternCsv),
            new("West", "Section2_NormalSummonRing_West", new Vector3(60.65f, 0.06f, 48.35f), new Vector3Int(61, 0, 48), 2104, 2204, SingleSummonMaxAlive, NormalSummonIntervalBeats, 4, MixedSummonMonsterIdsCsv, MixedSummonMonsterPatternCsv)
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
            ConfigureSummonPortals();
            EntityDefinitionSO normalSummonRingEntity = EnsureSummonEntityDefinition(
                NormalSummonRingEntityAssetPath,
                NormalSummonRingEntityId,
                "Entity_503_StageSummon_MossyStoneRing",
                NormalSummonRingEntityPrefabPath);
            EntityDefinitionSO eliteSummonGateEntity = EnsureSummonEntityDefinition(
                EliteSummonGateEntityAssetPath,
                EliteSummonGateEntityId,
                "Entity_504_StageSummon_RunicStoneGate",
                EliteSummonGateEntityPrefabPath);

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
            ConfigureStageData(towerEntity, normalSummonRingEntity, eliteSummonGateEntity);
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
            target.BindRuntimeGroup = true;
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

        [MenuItem("Tools/RhythmRPG/Stage/Validate Forest First Step Center Crystal")]
        public static void ValidateCenterCrystal()
        {
            EnsureSceneLoaded();

            Transform centerCrystal = GameObject.Find("Runic_Circle_Platform/Crystal")?.transform;
            if (centerCrystal == null)
            {
                Debug.LogError("[ForestFirstStepStageSetup] Center crystal validation failed: object not found.");
                return;
            }

            var target = centerCrystal.GetComponent<StageSceneObjectTarget>();
            var positionOk = Vector3.Distance(centerCrystal.position, CenterCrystalPosition) <= 0.05f;
            var targetOk = target != null
                           && string.Equals(target.TargetKey, "Crystal", System.StringComparison.OrdinalIgnoreCase)
                           && target.GroupId == 1
                           && target.StartHidden
                           && !target.CurrentPoseIsHidden
                           && target.EnableRiseFromGround
                           && target.UseWorldUpMotion
                           && Mathf.Abs(target.RiseHiddenYOffset - -2.15f) <= 0.01f;

            if (!positionOk || !targetOk)
            {
                Debug.LogError(
                    "[ForestFirstStepStageSetup] Center crystal validation failed. " +
                    $"Position={centerCrystal.position}, Expected={CenterCrystalPosition}, TargetOk={targetOk}.");
                return;
            }

            Debug.Log("[ForestFirstStepStageSetup] Center crystal validation OK. It rises from below to the altar-top pose.");
        }

        private static void ConfigureSummonPortals()
        {
            EnsureSummonPortalFolder();

            GameObject ringVisualPrefab = EnsureSummonPortalPrefab(
                "Mossy_Stone_Ring",
                NormalSummonRingPrefabPath,
                StageSummonPortalKind.NormalRing);
            GameObject gateVisualPrefab = EnsureSummonPortalPrefab(
                "Runic_Stone_Gate",
                EliteSummonGatePrefabPath,
                StageSummonPortalKind.EliteGate);

            if (ringVisualPrefab == null || gateVisualPrefab == null)
                return;

            EnsureSummonEntityPrefab(
                ringVisualPrefab,
                NormalSummonRingEntityPrefabPath,
                StageSummonPortalKind.NormalRing,
                new Vector3(0f, 0.06f, 0f),
                new Vector3(270f, 0f, 0f),
                new Vector3(100f, 100f, 100f),
                NormalSummonTargetKey);
            EnsureSummonEntityPrefab(
                gateVisualPrefab,
                EliteSummonGateEntityPrefabPath,
                StageSummonPortalKind.EliteGate,
                new Vector3(0f, EliteGatePosition.y, 0f),
                EliteGateRotation,
                EliteGateScale,
                EliteSummonTargetKey);

            Transform spawnRoot = EnsureSummonSpawnPointRoot();
            DestroyLooseSceneObject("Mossy_Stone_Ring", null);
            DestroyLooseSceneObject("Runic_Stone_Gate", null);
            DestroySummonPortalSceneRoot();

            foreach (SummonRingSpec ring in SummonRings)
                EnsureSummonSpawnPoint(spawnRoot, ring);

            EditorUtility.SetDirty(spawnRoot.gameObject);
        }

        private static void EnsureSummonPortalFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");

            if (!AssetDatabase.IsValidFolder(SummonPortalPrefabFolder))
                AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "StageSummons");
        }

        private static GameObject EnsureSummonPortalPrefab(string sourceObjectName, string prefabPath, StageSummonPortalKind kind)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                GameObject source = GameObject.Find(sourceObjectName);
                if (source != null)
                {
                    GameObject clone = Object.Instantiate(source);
                    clone.name = Path.GetFileNameWithoutExtension(prefabPath);
                    ConfigureSummonPortalTarget(
                        clone,
                        kind,
                        kind == StageSummonPortalKind.NormalRing ? NormalSummonTargetKey : EliteSummonTargetKey,
                        kind == StageSummonPortalKind.NormalRing ? 0 : EliteGateSceneGroupId);
                    ConfigurePrefabMarker(clone, kind);
                    PrefabUtility.SaveAsPrefabAsset(clone, prefabPath);
                    Object.DestroyImmediate(clone);
                }

                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }

            if (prefab == null)
            {
                Debug.LogError($"[ForestFirstStepStageSetup] Summon prefab missing and source not found: {prefabPath}");
                return null;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            ConfigureSummonPortalTarget(
                prefabRoot,
                kind,
                kind == StageSummonPortalKind.NormalRing ? NormalSummonTargetKey : EliteSummonTargetKey,
                kind == StageSummonPortalKind.NormalRing ? 0 : EliteGateSceneGroupId);
            ConfigurePrefabMarker(prefabRoot, kind);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static GameObject EnsureSummonEntityPrefab(
            GameObject visualPrefab,
            string prefabPath,
            StageSummonPortalKind kind,
            Vector3 visualLocalPosition,
            Vector3 visualLocalRotation,
            Vector3 visualLocalScale,
            string targetKey)
        {
            if (visualPrefab == null)
                return null;

            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            GameObject visual = InstantiatePrefab(visualPrefab, root.transform);
            if (visual != null)
            {
                visual.name = "Visual";
                visual.transform.localPosition = visualLocalPosition;
                visual.transform.localRotation = Quaternion.Euler(visualLocalRotation);
                visual.transform.localScale = visualLocalScale;
                RemoveComponentIfExists<StageSceneObjectTarget>(visual);
                RemoveComponentIfExists<StageSceneObjectAutoReveal>(visual);
                RemoveComponentIfExists<StageSummonPortalMarker>(visual);
            }

            StageSceneObjectTarget target = ConfigureSummonPortalTarget(
                root,
                kind,
                targetKey,
                0,
                bindRuntimeGroup: true,
                motionRoot: visual != null ? visual.transform : null);

            var autoReveal = EnsureComponent<StageSceneObjectAutoReveal>(root);
            autoReveal.Target = target;
            autoReveal.DelayMs = 0;
            autoReveal.DurationMs = target.DefaultDurationMs;
            autoReveal.ShakeCameraOnReveal = kind == StageSummonPortalKind.EliteGate;
            autoReveal.ShakeDelaySeconds = kind == StageSummonPortalKind.EliteGate ? 0.08f : 0f;
            autoReveal.CameraShakeDuration = 0.48f;
            autoReveal.CameraShakeStrength = kind == StageSummonPortalKind.EliteGate ? 0.075f : 0.035f;
            autoReveal.CameraShakeFrequency = 22f;

            var marker = EnsureComponent<StageSummonPortalMarker>(root);
            marker.Kind = kind;
            marker.PortalKey = kind == StageSummonPortalKind.NormalRing ? NormalSummonTargetKey : EliteSummonTargetKey;
            marker.SceneGroupId = 0;
            marker.SpawnGroupId = 0;
            marker.SpawnPoint = null;
            marker.SpawnCell = Vector3Int.zero;
            marker.MaxAlive = NormalSummonMaxAlive;
            marker.SpawnIntervalBeats = NormalSummonIntervalBeats;
            marker.InitialDelayBeats = 1;
            marker.MonsterIdsCsv = MixedSummonMonsterIdsCsv;
            marker.MonsterPattern = MixedSummonMonsterPatternCsv;

            ConfigurePassThroughColliders(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static void RemoveComponentIfExists<T>(GameObject go) where T : Component
        {
            if (go == null)
                return;

            T component = go.GetComponent<T>();
            if (component != null)
                Object.DestroyImmediate(component);
        }

        private static void ConfigurePassThroughColliders(GameObject root)
        {
            if (root == null)
                return;

            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.isTrigger = true;
                EditorUtility.SetDirty(collider);
            }
        }

        private static Transform EnsureSummonSpawnPointRoot()
        {
            GameObject root = GameObject.Find(SummonSpawnPointRootName);
            if (root == null)
                root = new GameObject(SummonSpawnPointRootName);

            return root.transform;
        }

        private static StageSummonSpawnPointMarker EnsureSummonSpawnPoint(Transform parent, SummonRingSpec ring)
        {
            string objectName = GetSummonSpawnPointName(ring);
            Transform existing = parent != null ? parent.Find(objectName) : null;
            if (existing == null)
            {
                GameObject loose = GameObject.Find(objectName);
                if (loose != null)
                    existing = loose.transform;
            }

            bool created = existing == null;
            if (created)
            {
                var go = new GameObject(objectName);
                existing = go.transform;
            }

            if (parent != null && existing.parent != parent)
                existing.SetParent(parent, true);

            existing.position = new Vector3(ring.SpawnCell.x, ring.SpawnCell.y, ring.SpawnCell.z);

            StageSummonSpawnPointMarker marker = existing.GetComponent<StageSummonSpawnPointMarker>();
            bool markerCreated = marker == null;
            if (marker == null)
                marker = existing.gameObject.AddComponent<StageSummonSpawnPointMarker>();

            marker.PortalKey = ring.ObjectName;
            marker.SpawnGroupId = ring.SpawnGroupId;
            if (created || markerCreated)
            {
                marker.MaxAlive = ring.MaxAlive;
                marker.SpawnIntervalBeats = ring.IntervalBeats;
                marker.InitialDelayBeats = ring.InitialDelayBeats;
                marker.MonsterIdsCsv = ring.MonsterIdsCsv;
                marker.MonsterPattern = ring.MonsterPatternCsv;
            }
            else
            {
                marker.MaxAlive = ShouldReplaceGeneratedSummonMaxAlive(marker.MaxAlive)
                    ? ring.MaxAlive
                    : Mathf.Max(1, marker.MaxAlive);
                marker.SpawnIntervalBeats = ShouldReplaceGeneratedSummonInterval(marker.SpawnIntervalBeats)
                    ? ring.IntervalBeats
                    : Mathf.Max(1, marker.SpawnIntervalBeats);
                marker.InitialDelayBeats = Mathf.Max(0, marker.InitialDelayBeats);
                if (ShouldReplaceGeneratedSummonMonsterIds(marker.MonsterIdsCsv))
                    marker.MonsterIdsCsv = ring.MonsterIdsCsv;
                if (ShouldReplaceGeneratedSummonMonsterPattern(marker.MonsterPattern))
                    marker.MonsterPattern = ring.MonsterPatternCsv;
            }

            EditorUtility.SetDirty(marker);
            EditorUtility.SetDirty(existing.gameObject);
            return marker;
        }

        private static string GetSummonSpawnPointName(SummonRingSpec ring)
            => "SP_" + ring.ObjectName;

        private static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance != null)
                return instance;

            return Object.Instantiate(prefab, parent);
        }

        private static void DestroyLooseSceneObject(string objectName, Transform preserveRoot)
        {
            GameObject existing = GameObject.Find(objectName);
            if (existing == null)
                return;

            if (preserveRoot != null && (existing.transform == preserveRoot || existing.transform.parent == preserveRoot))
                return;

            Object.DestroyImmediate(existing);
        }

        private static void DestroySummonPortalSceneRoot()
        {
            GameObject root = GameObject.Find(SummonPortalRootName);
            if (root != null)
                Object.DestroyImmediate(root);
        }

        private static StageSceneObjectTarget ConfigureSummonPortalTarget(
            GameObject root,
            StageSummonPortalKind kind,
            string targetKey,
            int sceneGroupId,
            bool bindRuntimeGroup = false,
            Transform motionRoot = null)
        {
            var target = EnsureComponent<StageSceneObjectTarget>(root);
            target.TargetKey = targetKey;
            target.GroupId = sceneGroupId;
            target.BindRuntimeGroup = bindRuntimeGroup;
            target.DefaultDurationMs = kind == StageSummonPortalKind.EliteGate ? 1500 : 950;
            target.HiddenScale = kind == StageSummonPortalKind.EliteGate ? 0.72f : 0.64f;
            target.HiddenYOffset = -0.35f;
            target.DisableCollidersWhenHidden = true;
            target.UseWorldUpMotion = true;
            target.StartHidden = true;
            target.CurrentPoseIsHidden = false;
            target.EnableRiseFromGround = true;
            target.ReplayShowAnimationWhenAlreadyVisible = kind == StageSummonPortalKind.EliteGate;
            target.RiseHiddenYOffset = kind == StageSummonPortalKind.EliteGate ? -2.3f : -0.75f;
            target.RiseOvershootHeight = kind == StageSummonPortalKind.EliteGate ? 0.18f : 0.08f;
            target.EnableIdleFloat = false;
            target.MotionRoots = motionRoot != null ? new[] { motionRoot } : System.Array.Empty<Transform>();
            EditorUtility.SetDirty(target);
            return target;
        }

        private static void ConfigurePrefabMarker(GameObject root, StageSummonPortalKind kind)
        {
            var marker = EnsureComponent<StageSummonPortalMarker>(root);
            marker.Kind = kind;
            marker.PortalKey = kind == StageSummonPortalKind.NormalRing ? "SummonRing" : "EliteSummonGate";
            marker.SceneGroupId = kind == StageSummonPortalKind.NormalRing ? 0 : EliteGateSceneGroupId;
            marker.SpawnGroupId = 0;
            marker.SpawnCell = Vector3Int.zero;
            marker.MaxAlive = NormalSummonMaxAlive;
            marker.SpawnIntervalBeats = NormalSummonIntervalBeats;
            marker.InitialDelayBeats = 1;
            marker.MonsterIdsCsv = MixedSummonMonsterIdsCsv;
            marker.MonsterPattern = MixedSummonMonsterPatternCsv;
            EditorUtility.SetDirty(marker);
        }

        private static bool ShouldReplaceGeneratedSummonMaxAlive(int value)
            => value == BatSummonMaxAlive || value == SingleSummonMaxAlive;

        private static bool ShouldReplaceGeneratedSummonInterval(int value)
            => value == 8 || value == NormalSummonIntervalBeats;

        private static bool ShouldReplaceGeneratedSummonMonsterIds(string value)
        {
            string normalized = NormalizeCsv(value);
            return string.IsNullOrWhiteSpace(normalized)
                   || normalized == NormalizeCsv(BatSummonMonsterIdsCsv)
                   || normalized == NormalizeCsv(EvilMageSummonMonsterIdsCsv)
                   || normalized == NormalizeCsv(SpecterSummonMonsterIdsCsv)
                   || normalized == NormalizeCsv(MixedSummonMonsterIdsCsv)
                   || normalized == NormalizeCsv(LegacyFirstPhaseSummonMonsterIdsCsv);
        }

        private static bool ShouldReplaceGeneratedSummonMonsterPattern(string value)
        {
            string normalized = NormalizeCsv(value);
            return string.IsNullOrWhiteSpace(normalized)
                   || normalized == NormalizeCsv(BatSummonMonsterPatternCsv)
                   || normalized == NormalizeCsv(EvilMageSummonMonsterPatternCsv)
                   || normalized == NormalizeCsv(SpecterSummonMonsterPatternCsv)
                   || normalized == NormalizeCsv(MixedSummonMonsterPatternCsv)
                   || normalized == NormalizeCsv(LegacyFirstPhaseSummonMonsterPatternCsv);
        }

        private static string NormalizeCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string[] tokens = value.Split(new[] { ',', ';', '|', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
                tokens[i] = tokens[i].Trim().ToLowerInvariant();

            return string.Join(",", tokens);
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

        private static EntityDefinitionSO EnsureSummonEntityDefinition(
            string assetPath,
            int entityId,
            string entityName,
            string prefabPath)
        {
            var entity = AssetDatabase.LoadAssetAtPath<EntityDefinitionSO>(assetPath);
            if (entity == null)
            {
                entity = ScriptableObject.CreateInstance<EntityDefinitionSO>();
                AssetDatabase.CreateAsset(entity, assetPath);
            }

            entity.EntityId = entityId;
            entity.EntityName = entityName;
            entity.Type = EntityType.Object;
            entity.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
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
            linkLight.color = new Color(0.14f, 0.85f, 0.48f, 1f);
            linkLight.range = 7.15f;
            linkLight.intensity = 1.75f;

            var line = EnsureComponent<LineRenderer>(link.gameObject);
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.08f;
            line.endWidth = 0.11f;
            line.startColor = new Color(0.16f, 0.88f, 0.54f, 0.90f);
            line.endColor = new Color(0.42f, 1f, 0.66f, 0.72f);

            var beam = EnsureComponent<StageCrystalLinkBeam>(link.gameObject);
            beam.Source = crystal;
            beam.Target = centerCrystal;
            beam.SourcePath = "Crystal";
            beam.TargetPath = "Runic_Circle_Platform/Crystal";
            beam.Line = line;
            beam.LinkLight = linkLight;
            beam.LinkedObjects = linkedFxObjects;
            beam.BeamColor = new Color(0.16f, 0.88f, 0.54f, 0.90f);
            beam.BeamWidth = 0.085f;
            beam.SourceYOffset = 0f;
            beam.TargetYOffset = 0f;
            beam.UseRendererBoundsEndpoints = true;
            beam.SegmentCount = 20;
            beam.ArcHeight = 0.42f;
            beam.DistanceArcFactor = 0.018f;
            beam.WaveAmplitude = 0.055f;
            beam.WaveFrequency = 1.35f;
            beam.PulseFrequency = 1.15f;
            beam.PulseStrength = 0.22f;
            beam.LinkLightMinIntensity = 1.65f;
            beam.LinkLightMaxIntensity = 2.75f;

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

            Transform towerRoot = sourceParent.root;
            Transform rootGlow = towerRoot != null ? towerRoot.Find("FX_Runic_Crystal_Glow") : null;
            if (rootGlow != null && !fxChildren.Contains(rootGlow.gameObject))
                fxChildren.Add(rootGlow.gameObject);

            return fxChildren.ToArray();
        }

        private static void ConfigureStageData(
            EntityDefinitionSO towerEntity,
            EntityDefinitionSO normalSummonRingEntity,
            EntityDefinitionSO eliteSummonGateEntity)
        {
            StageDataSO stage = AssetDatabase.LoadAssetAtPath<StageDataSO>(StageDataPath);
            if (stage == null)
            {
                Debug.LogError($"[ForestFirstStepStageSetup] Stage data missing: {StageDataPath}");
                return;
            }

            EnsureTowerRegistry(stage, towerEntity);
            EnsureSummonRegistry(stage, normalSummonRingEntity, eliteSummonGateEntity);

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
            centerEvent.Actions.Add(SpawnSummonRing(SummonRings[0]));
            centerEvent.Actions.Add(SetSummonPortal(SummonRings[0], active: true));
            centerEvent.Actions.Add(SpawnSummonRing(SummonRings[1]));
            centerEvent.Actions.Add(SetSummonPortal(SummonRings[1], active: true));
            centerEvent.Actions.Add(SetObjectState(TowerPhaseStateId, 1));

            ConfigureHoldEvent(stage, 5, "Section03", "North Tower Hold", Towers[0], phase: 1);
            ConfigureHoldEvent(stage, 6, "Section03", "East Tower Hold", Towers[1], phase: 1);

            EventInfoSO unlockSecondPair = EnsureEvent(stage, 7, "Section03", "Unlock South West Tower Holds");
            unlockSecondPair.Conditions.Clear();
            unlockSecondPair.Conditions.Add(ObjectState(Towers[0].CompleteStateId, 1));
            unlockSecondPair.Conditions.Add(ObjectState(Towers[1].CompleteStateId, 1));
            unlockSecondPair.Actions.Clear();
            unlockSecondPair.Actions.Add(SetObjectState(TowerPhaseStateId, 2));
            unlockSecondPair.Actions.Add(SpawnSummonRing(SummonRings[2]));
            unlockSecondPair.Actions.Add(SetSummonPortal(SummonRings[2], active: true));
            unlockSecondPair.Actions.Add(SpawnSummonRing(SummonRings[3]));
            unlockSecondPair.Actions.Add(SetSummonPortal(SummonRings[3], active: true));

            ConfigureHoldEvent(stage, 8, "Section03", "South Tower Hold", Towers[2], phase: 2);
            ConfigureHoldEvent(stage, 9, "Section03", "West Tower Hold", Towers[3], phase: 2);

            EventInfoSO completeTowers = EnsureEvent(stage, 10, "Section03", "Tower Holds Complete");
            completeTowers.Conditions.Clear();
            completeTowers.Conditions.Add(ObjectState(Towers[2].CompleteStateId, 1));
            completeTowers.Conditions.Add(ObjectState(Towers[3].CompleteStateId, 1));
            completeTowers.Actions.Clear();
            foreach (SummonRingSpec ring in SummonRings)
            {
                completeTowers.Actions.Add(SetSummonPortal(ring, active: false));
                completeTowers.Actions.Add(RemoveEntityGroup(ResolveSummonSpawnGroupId(ring)));
            }

            completeTowers.Actions.Add(SetSceneObject(NormalSummonTargetKey, 0, visible: false, durationMs: 850));
            foreach (SummonRingSpec ring in SummonRings)
                completeTowers.Actions.Add(RemoveEntityGroup(ring.SceneGroupId, delayMs: 950));
            completeTowers.Actions.Add(SpawnEliteSummonGate());
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

        private static void EnsureSummonRegistry(
            StageDataSO stage,
            EntityDefinitionSO normalSummonRingEntity,
            EntityDefinitionSO eliteSummonGateEntity)
        {
            EnsureRegistryEntry(stage, NormalSummonRingEntityKey, normalSummonRingEntity);
            EnsureRegistryEntry(stage, EliteSummonGateEntityKey, eliteSummonGateEntity);
        }

        private static void EnsureRegistryEntry(StageDataSO stage, string key, EntityDefinitionSO entity)
        {
            if (stage == null || string.IsNullOrWhiteSpace(key))
                return;

            StageRegisteredEntity registry = stage.Registry.Find(item => item != null && item.Key == key);
            if (registry == null)
            {
                registry = new StageRegisteredEntity();
                stage.Registry.Add(registry);
            }

            registry.Key = key;
            registry.EntityDef = entity;
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
            condition.CountRequirement = StageCountRequirementMode.ParticipantCount;
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
            List<Vector2Int> cells = BuildSquareCells(tower.AreaCenter, TowerHoldAreaRadius);
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

        private static ActionInfoSO SetSummonPortal(SummonRingSpec ring, bool active)
        {
            StageSummonSpawnPointMarker marker = FindSummonSpawnPoint(ring);
            Vector3Int spawnCell = marker != null ? marker.GetCell() : ring.SpawnCell;
            int spawnGroupId = marker != null && marker.SpawnGroupId > 0 ? marker.SpawnGroupId : ring.SpawnGroupId;
            int maxAlive = marker != null ? Mathf.Max(1, marker.MaxAlive) : ring.MaxAlive;
            int intervalBeats = marker != null ? Mathf.Max(1, marker.SpawnIntervalBeats) : ring.IntervalBeats;
            int initialDelayBeats = marker != null ? Mathf.Max(0, marker.InitialDelayBeats) : ring.InitialDelayBeats;
            string monsterIds = marker != null && !string.IsNullOrWhiteSpace(marker.MonsterIdsCsv)
                ? marker.MonsterIdsCsv
                : ring.MonsterIdsCsv;
            string pattern = marker != null && !string.IsNullOrWhiteSpace(marker.MonsterPattern)
                ? marker.MonsterPattern
                : ring.MonsterPatternCsv;
            string portalKey = marker != null && !string.IsNullOrWhiteSpace(marker.PortalKey)
                ? marker.PortalKey
                : ring.ObjectName;

            return new ActionInfoSO
            {
                Type = ActionType.SetSummonPortalActive,
                StringVal = portalKey,
                ParamId = spawnGroupId,
                GroupId = active ? 1 : 0,
                Position = new Vector3(spawnCell.x, spawnCell.y, spawnCell.z),
                ObjectSize = new Vector2Int(maxAlive, initialDelayBeats),
                GuideTitle = monsterIds,
                GuideBody = pattern,
                DurationMs = intervalBeats
            };
        }

        private static int ResolveSummonSpawnGroupId(SummonRingSpec ring)
        {
            StageSummonSpawnPointMarker marker = FindSummonSpawnPoint(ring);
            return marker != null && marker.SpawnGroupId > 0 ? marker.SpawnGroupId : ring.SpawnGroupId;
        }

        private static StageSummonSpawnPointMarker FindSummonSpawnPoint(SummonRingSpec ring)
        {
            string objectName = GetSummonSpawnPointName(ring);
            Transform root = GameObject.Find(SummonSpawnPointRootName)?.transform;
            Transform target = root != null ? root.Find(objectName) : null;
            if (target == null)
                target = GameObject.Find(objectName)?.transform;

            return target != null ? target.GetComponent<StageSummonSpawnPointMarker>() : null;
        }

        private static ActionInfoSO RemoveEntityGroup(int groupId, int delayMs = 0)
        {
            return new ActionInfoSO
            {
                Type = ActionType.RemoveEntityGroup,
                ParamId = groupId,
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

        private static ActionInfoSO SpawnSummonRing(SummonRingSpec ring)
        {
            return new ActionInfoSO
            {
                Type = ActionType.SpawnObject,
                HeaderParam = NormalSummonRingEntityKey,
                Position = new Vector3(Mathf.RoundToInt(ring.Position.x), 0f, Mathf.RoundToInt(ring.Position.z)),
                ObjectSize = new Vector2Int(2, 2),
                GroupId = ring.SceneGroupId,
                DurationMs = 3500
            };
        }

        private static ActionInfoSO SpawnEliteSummonGate()
        {
            return new ActionInfoSO
            {
                Type = ActionType.SpawnObject,
                HeaderParam = EliteSummonGateEntityKey,
                Position = new Vector3(Mathf.RoundToInt(EliteGatePosition.x), 0f, Mathf.RoundToInt(EliteGatePosition.z)),
                ObjectSize = new Vector2Int(2, 2),
                GroupId = EliteGateSceneGroupId,
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

        private readonly struct SummonRingSpec
        {
            public readonly string Label;
            public readonly string ObjectName;
            public readonly Vector3 Position;
            public readonly Vector3Int SpawnCell;
            public readonly int SceneGroupId;
            public readonly int SpawnGroupId;
            public readonly int MaxAlive;
            public readonly int IntervalBeats;
            public readonly int InitialDelayBeats;
            public readonly string MonsterIdsCsv;
            public readonly string MonsterPatternCsv;

            public SummonRingSpec(
                string label,
                string objectName,
                Vector3 position,
                Vector3Int spawnCell,
                int sceneGroupId,
                int spawnGroupId,
                int maxAlive,
                int intervalBeats,
                int initialDelayBeats,
                string monsterIdsCsv,
                string monsterPatternCsv)
            {
                Label = label;
                ObjectName = objectName;
                Position = position;
                SpawnCell = spawnCell;
                SceneGroupId = sceneGroupId;
                SpawnGroupId = spawnGroupId;
                MaxAlive = maxAlive;
                IntervalBeats = intervalBeats;
                InitialDelayBeats = initialDelayBeats;
                MonsterIdsCsv = monsterIdsCsv;
                MonsterPatternCsv = monsterPatternCsv;
            }
        }
    }
}
