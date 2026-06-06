#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetClient.Room.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class SceneFunctionalPrefabOrganizer
{
    private const string GameForestTutorialScenePath = "Assets/0.MainProject/Scenes/Game/Game_Forest_Tutorial.unity";
    private const string TownForestScenePath = "Assets/0.MainProject/Scenes/Town/Town_Forest.unity";

    private const string GameplayFunctionalSetPrefabPath = "Assets/0.MainProject/Prefabs/SceneSets/Gameplay/PF_RhythmBattle_Functional_Set.prefab";
    private const string TownFunctionalSetFolder = "Assets/0.MainProject/Prefabs/SceneSets/Town";
    private const string TownFunctionalSetPrefabPath = TownFunctionalSetFolder + "/PF_TownForest_Functional_Set.prefab";
    private const string TownFunctionalAssetRootName = "PF_TownForest_Functional_Set";

    private const string GameplayRootName = "Gameplay_Functions";
    private const string CanvasRhythmHudName = "Canvas_RhythmHUD";
    private const string BoardViewName = "BoardView";
    private const string RuntimeEntitiesRootName = "Runtime_Entities";
    private const string RuntimeSkillRunnersRootName = "Runtime_SkillRunners";
    private const string TownAppearanceRootName = "Town_Appearance";
    private const string TownBoardTilesRootName = "Board_Tiles_Appearance";

    private static readonly string[] GameFunctionalChildren =
    {
        "BeatDebugUI_TMP",
        "BgmDirector",
        "Binder",
        BoardViewName,
        CanvasRhythmHudName,
        "ClientGameState",
        "ClientHandlers",
        "DebugManager",
        "EventSystem",
        "GameSceneContext",
        "Main Camera",
        "RhythmClient",
        "RhythmInputController"
    };

    private static readonly string[] TownFunctionalChildren =
    {
        "Main Camera",
        BoardViewName,
        RuntimeEntitiesRootName,
        RuntimeSkillRunnersRootName,
        "TownSceneContext",
        "BeatDebugUI_TMP",
        "BgmDirector",
        "Binder",
        CanvasRhythmHudName,
        "ClientGameState",
        "ClientHandlers",
        "MapData",
        "RhythmClient",
        "RhythmInputController",
        "EventSystem",
        "ApiClientProvider",
        "InventoryManager"
    };

    [MenuItem("RhythmRPG/Editors/Scene Sets/Replace Forest Scene Functions With Prefabs")]
    public static void ReplaceForestSceneFunctionsWithPrefabs()
    {
        EnsureTownFunctionalSetPrefab();
        ReplaceGameForestTutorialFunctions();
        ReconnectTownForestFunctions();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SceneFunctionalPrefabOrganizer] Replaced Town_Forest and Game_Forest_Tutorial functional objects with prefab set instances.");
    }

    [MenuItem("RhythmRPG/Editors/Scene Sets/Verify Forest Scene Function Prefabs")]
    public static void VerifyForestSceneFunctionPrefabs()
    {
        var hasError = false;

        VerifyScene(
            GameForestTutorialScenePath,
            GameplayFunctionalSetPrefabPath,
            GameFunctionalChildren,
            requireGameTiles: true,
            ref hasError);

        VerifyScene(
            TownForestScenePath,
            TownFunctionalSetPrefabPath,
            TownFunctionalChildren,
            requireGameTiles: false,
            ref hasError);

        if (!hasError)
        {
            Debug.Log("[SceneFunctionalPrefabOrganizer] VALIDATION OK. Target forest scenes use functional prefab set instances.");
        }
    }

    private static void EnsureTownFunctionalSetPrefab()
    {
        EnsureFolder(TownFunctionalSetFolder);

        var scene = EditorSceneManager.OpenScene(TownForestScenePath, OpenSceneMode.Single);
        var root = RequireRoot(GameplayRootName);
        MoveStandaloneHudUnderRoot(scene, root);
        MoveTownBoardTilesToAppearance(scene, root);
        PrepareTownBoardViewForPrefabSave(root);

        var sceneRootName = root.name;
        root.name = TownFunctionalAssetRootName;

        PrefabUtility.SaveAsPrefabAssetAndConnect(root, TownFunctionalSetPrefabPath, InteractionMode.AutomatedAction);
        root.name = sceneRootName;
        PrefabUtility.RecordPrefabInstancePropertyModifications(root);

        RebindTownFunctionalRefs(scene, root);
        SortChildren(root.transform, TownFunctionalChildren);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static void ReplaceGameForestTutorialFunctions()
    {
        var scene = EditorSceneManager.OpenScene(GameForestTutorialScenePath, OpenSceneMode.Single);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayFunctionalSetPrefabPath);
        if (prefab == null)
        {
            throw new FileNotFoundException("Missing gameplay functional set prefab.", GameplayFunctionalSetPrefabPath);
        }

        var oldRoot = FindRoot(scene, GameplayRootName);
        GameObject root;

        if (oldRoot != null && string.Equals(GetNearestPrefabPath(oldRoot), GameplayFunctionalSetPrefabPath, StringComparison.Ordinal))
        {
            root = oldRoot;
        }
        else
        {
            var snapshots = oldRoot != null ? CaptureChildSnapshots(oldRoot.transform) : new Dictionary<string, TransformSnapshot>(StringComparer.Ordinal);
            var rootSnapshot = oldRoot != null ? TransformSnapshot.From(oldRoot.transform) : TransformSnapshot.Identity;

            root = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (root == null)
            {
                throw new InvalidOperationException("[SceneFunctionalPrefabOrganizer] Failed to instantiate gameplay functional set prefab.");
            }

            root.name = GameplayRootName;
            rootSnapshot.Apply(root.transform);

            if (oldRoot != null)
            {
                CopyMatchingComponents(oldRoot.transform, root.transform);
                ApplyChildSnapshots(root.transform, snapshots);
                MoveBoardViewSceneChildren(oldRoot, root);
                Object.DestroyImmediate(oldRoot);
            }
        }

        DeleteRootObjectsByNameExcept(scene, CanvasRhythmHudName, root);
        DeleteRootFunctionalDuplicates(scene, root);
        RebindGameFunctionalRefs(scene, root);
        SortChildren(root.transform, GameFunctionalChildren);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static void ReconnectTownForestFunctions()
    {
        var scene = EditorSceneManager.OpenScene(TownForestScenePath, OpenSceneMode.Single);
        var root = RequireRoot(GameplayRootName);

        MoveStandaloneHudUnderRoot(scene, root);
        if (!string.Equals(GetNearestPrefabPath(root), TownFunctionalSetPrefabPath, StringComparison.Ordinal))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TownFunctionalSetPrefabPath);
            if (prefab == null)
            {
                throw new FileNotFoundException("Missing town functional set prefab.", TownFunctionalSetPrefabPath);
            }

            var snapshots = CaptureChildSnapshots(root.transform);
            var rootSnapshot = TransformSnapshot.From(root.transform);
            var oldRoot = root;

            root = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (root == null)
            {
                throw new InvalidOperationException("[SceneFunctionalPrefabOrganizer] Failed to instantiate town functional set prefab.");
            }

            root.name = GameplayRootName;
            rootSnapshot.Apply(root.transform);
            CopyMatchingComponents(oldRoot.transform, root.transform);
            ApplyChildSnapshots(root.transform, snapshots);
            Object.DestroyImmediate(oldRoot);
        }

        DeleteRootObjectsByNameExcept(scene, CanvasRhythmHudName, root);
        DeleteRootFunctionalDuplicates(scene, root);
        MoveTownBoardTilesToAppearance(scene, root);
        RebindTownFunctionalRefs(scene, root);
        SortChildren(root.transform, TownFunctionalChildren);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static void RebindGameFunctionalRefs(Scene scene, GameObject root)
    {
        var boardView = FindComponentInRoot<BoardView>(root);
        if (boardView != null)
        {
            boardView.ConfigureSceneRoots(boardView.transform, boardView.transform, boardView.transform);
            RebindBoardViewTemplateRefs(boardView);
        }

        foreach (var context in root.GetComponentsInChildren<GameSceneContext>(true))
        {
            RebindBaseSceneContextRefs(scene, root, context);
        }
    }

    private static void RebindTownFunctionalRefs(Scene scene, GameObject root)
    {
        var boardView = FindComponentInRoot<BoardView>(root);
        if (boardView != null)
        {
            var tileRoot = FindTownBoardTilesRoot(scene) ?? boardView.transform;
            var entityRoot = FindDirectChild(root.transform, RuntimeEntitiesRootName) ?? boardView.transform;
            var skillRoot = FindDirectChild(root.transform, RuntimeSkillRunnersRootName) ?? boardView.transform;
            boardView.ConfigureSceneRoots(tileRoot, entityRoot, skillRoot);
            RebindBoardViewTemplateRefs(boardView);
        }

        foreach (var context in root.GetComponentsInChildren<TownSceneContext>(true))
        {
            RebindBaseSceneContextRefs(scene, root, context);
        }
    }

    private static void RebindBaseSceneContextRefs(Scene scene, GameObject root, Component context)
    {
        var serialized = new SerializedObject(context);
        SetObject(serialized, "_gs", FindComponentInScene<ClientGameState>(scene, root));
        SetObject(serialized, "_handlers", FindComponentInScene<ClientHandlers>(scene, root));
        SetObject(serialized, "_mapRegistry", FindComponentInScene<MapRegistry>(scene, root));
        SetObject(serialized, "_boardView", FindComponentInScene<BoardView>(scene, root));
        SetObject(serialized, "_inputBinder", FindComponentInScene<RhythmInputControllerBinder>(scene, root));
        SetObject(serialized, "_inputController", FindComponentInScene<RhythmInputController>(scene, root));
        SetObject(serialized, "_cameraBinder", FindComponentInScene<CameraBinder>(scene, root));
        SetObject(serialized, "_cameraFollow", FindComponentInScene<CameraFollow>(scene, root));
        SetObject(serialized, "_rhythm", FindComponentInScene<RhythmClient>(scene, root));
        SetObject(serialized, "_bgmDirector", FindComponentInScene<BgmDirector>(scene, root));
        SetObject(serialized, "_autoCalib", FindComponentInScene<AudioOffsetAutoCalibrator>(scene, root));
        SetObject(serialized, "_hud", FindComponentInScene<HudPresenter>(scene, root));
        SetObject(serialized, "_beatDebug", FindComponentInScene<BeatDebugUI_TMP>(scene, root));
        SetObject(serialized, "_apiClientProvider", FindComponentInScene<ApiClientProvider>(scene, root));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(context);
    }

    private static void RebindBoardViewTemplateRefs(BoardView boardView)
    {
        var playerTemplate = FindDirectChild(boardView.transform, "PlayerPrefab_Template");
        var monsterTemplate = FindDirectChild(boardView.transform, "MonsterPrefab_Template");

        if (playerTemplate != null)
        {
            boardView.playerPrefab = playerTemplate.gameObject;
        }

        if (monsterTemplate != null)
        {
            boardView.monsterPrefab = monsterTemplate.gameObject;
        }

        EditorUtility.SetDirty(boardView);
    }

    private static void MoveStandaloneHudUnderRoot(Scene scene, GameObject root)
    {
        var hudRoots = scene.GetRootGameObjects()
            .Where(go => go != root && string.Equals(go.name, CanvasRhythmHudName, StringComparison.Ordinal))
            .ToArray();

        foreach (var hud in hudRoots)
        {
            hud.transform.SetParent(root.transform, true);
        }
    }

    private static int MoveBoardViewSceneChildren(GameObject oldRoot, GameObject newRoot)
    {
        var oldBoard = FindDeepChild(oldRoot.transform, BoardViewName);
        var newBoard = FindDeepChild(newRoot.transform, BoardViewName);
        if (oldBoard == null || newBoard == null)
        {
            return 0;
        }

        var movableChildren = oldBoard.Cast<Transform>()
            .Where(child => !IsBoardViewTemplateChild(child.name))
            .ToArray();

        foreach (var child in movableChildren)
        {
            child.SetParent(newBoard, true);
        }

        return movableChildren.Length;
    }

    private static bool IsBoardViewTemplateChild(string name)
        => string.Equals(name, "PlayerPrefab_Template", StringComparison.Ordinal)
           || string.Equals(name, "MonsterPrefab_Template", StringComparison.Ordinal);

    private static void CopyMatchingComponents(Transform sourceRoot, Transform targetRoot)
    {
        var sourceChildren = sourceRoot.GetComponentsInChildren<Transform>(true)
            .GroupBy(t => GetRelativePath(sourceRoot, t), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var targetTransform in targetRoot.GetComponentsInChildren<Transform>(true))
        {
            var path = GetRelativePath(targetRoot, targetTransform);
            if (!sourceChildren.TryGetValue(path, out var sourceTransform))
            {
                continue;
            }

            foreach (var sourceComponent in sourceTransform.GetComponents<Component>())
            {
                if (sourceComponent == null || sourceComponent is Transform)
                {
                    continue;
                }

                var targetComponent = targetTransform.GetComponent(sourceComponent.GetType());
                if (targetComponent == null)
                {
                    continue;
                }

                try
                {
                    EditorUtility.CopySerialized(sourceComponent, targetComponent);
                    EditorUtility.SetDirty(targetComponent);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SceneFunctionalPrefabOrganizer] Could not copy {sourceComponent.GetType().Name} on {path}: {ex.Message}");
                }
            }
        }
    }

    private static Dictionary<string, TransformSnapshot> CaptureChildSnapshots(Transform root)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .Where(t => t != root)
            .GroupBy(t => GetRelativePath(root, t), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => TransformSnapshot.From(group.First()), StringComparer.Ordinal);
    }

    private static void ApplyChildSnapshots(Transform root, IReadOnlyDictionary<string, TransformSnapshot> snapshots)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform == root)
            {
                continue;
            }

            var path = GetRelativePath(root, transform);
            if (snapshots.TryGetValue(path, out var snapshot))
            {
                snapshot.Apply(transform);
            }
        }
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (target == root)
        {
            return string.Empty;
        }

        var names = new Stack<string>();
        var cursor = target;
        while (cursor != null && cursor != root)
        {
            names.Push(cursor.name);
            cursor = cursor.parent;
        }

        return string.Join("/", names);
    }

    private static void VerifyScene(
        string scenePath,
        string expectedPrefabPath,
        IEnumerable<string> requiredChildren,
        bool requireGameTiles,
        ref bool hasError)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var root = FindRoot(scene, GameplayRootName);

        Report(root != null, $"{scene.name}: {GameplayRootName} exists.", $"{scene.name}: Missing {GameplayRootName}.", ref hasError);
        if (root == null)
        {
            return;
        }

        var prefabPath = GetNearestPrefabPath(root);
        Report(
            string.Equals(prefabPath, expectedPrefabPath, StringComparison.Ordinal),
            $"{scene.name}: functional root is linked to {expectedPrefabPath}.",
            $"{scene.name}: functional root prefab mismatch. Expected={expectedPrefabPath}, Actual={prefabPath}",
            ref hasError);

        foreach (var childName in requiredChildren)
        {
            Report(
                FindDeepChild(root.transform, childName) != null,
                $"{scene.name}: {childName} exists under {GameplayRootName}.",
                $"{scene.name}: Missing {childName} under {GameplayRootName}.",
                ref hasError);
        }

        var huds = Object.FindObjectsByType<HudPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(hud => hud != null && hud.gameObject.scene == scene)
            .ToArray();
        Report(huds.Length == 1, $"{scene.name}: one Rhythm HUD presenter exists.", $"{scene.name}: Rhythm HUD presenter count={huds.Length}.", ref hasError);
        if (huds.Length == 1)
        {
            Report(
                huds[0].transform.IsChildOf(root.transform),
                $"{scene.name}: Rhythm HUD is inside functional root.",
                $"{scene.name}: Rhythm HUD is not inside functional root.",
                ref hasError);
        }

        var rootLevelHud = scene.GetRootGameObjects().Any(go => string.Equals(go.name, CanvasRhythmHudName, StringComparison.Ordinal));
        Report(!rootLevelHud, $"{scene.name}: no root-level duplicate Rhythm HUD.", $"{scene.name}: root-level duplicate Rhythm HUD remains.", ref hasError);

        if (requireGameTiles)
        {
            var board = FindComponentInRoot<BoardView>(root);
            var tileCount = board != null
                ? board.transform.Cast<Transform>().Count(child => child.name.StartsWith("Tile_", StringComparison.Ordinal))
                : 0;
            Report(tileCount > 0, $"{scene.name}: BoardView scene tiles preserved ({tileCount}).", $"{scene.name}: BoardView scene tiles are missing.", ref hasError);
        }
        else if (string.Equals(scenePath, TownForestScenePath, StringComparison.Ordinal))
        {
            var board = FindComponentInRoot<BoardView>(root);
            var boardTileCount = board != null
                ? board.transform.Cast<Transform>().Count(child => child.name.StartsWith("Tile_", StringComparison.Ordinal))
                : 0;
            var appearanceTileRoot = FindTownBoardTilesRoot(scene);
            var appearanceTileCount = appearanceTileRoot != null
                ? appearanceTileRoot.Cast<Transform>().Count(child => child.name.StartsWith("Tile_", StringComparison.Ordinal))
                : 0;

            Report(boardTileCount == 0, $"{scene.name}: no Town tiles are embedded in functional BoardView.", $"{scene.name}: Town BoardView has embedded tiles ({boardTileCount}).", ref hasError);
            Report(appearanceTileCount > 0, $"{scene.name}: Town appearance tiles remain outside functional prefab ({appearanceTileCount}).", $"{scene.name}: Town appearance tile root is empty.", ref hasError);
        }
    }

    private static void DeleteRootFunctionalDuplicates(Scene scene, GameObject allowedRoot)
    {
        var reservedNames = new HashSet<string>(GameFunctionalChildren.Concat(TownFunctionalChildren), StringComparer.Ordinal)
        {
            GameplayRootName
        };

        foreach (var root in scene.GetRootGameObjects().ToArray())
        {
            if (root == allowedRoot || !reservedNames.Contains(root.name))
            {
                continue;
            }

            Object.DestroyImmediate(root);
        }
    }

    private static void DeleteRootObjectsByNameExcept(Scene scene, string objectName, GameObject allowedAncestor)
    {
        foreach (var root in scene.GetRootGameObjects().ToArray())
        {
            if (!string.Equals(root.name, objectName, StringComparison.Ordinal))
            {
                continue;
            }

            if (allowedAncestor != null && root.transform.IsChildOf(allowedAncestor.transform))
            {
                continue;
            }

            Object.DestroyImmediate(root);
        }
    }

    private static int MoveTownBoardTilesToAppearance(Scene scene, GameObject root)
    {
        var boardTransform = FindDeepChild(root.transform, BoardViewName);
        if (boardTransform == null)
        {
            return 0;
        }

        var tileRoot = FindOrCreateTownBoardTilesRoot(scene);
        var tileChildren = boardTransform.Cast<Transform>()
            .Where(child => child.name.StartsWith("Tile_", StringComparison.Ordinal))
            .ToArray();

        if (tileChildren.Length > 0 && PrefabUtility.IsPartOfPrefabInstance(root))
        {
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            boardTransform = FindDeepChild(root.transform, BoardViewName);
            tileChildren = boardTransform != null
                ? boardTransform.Cast<Transform>().Where(child => child.name.StartsWith("Tile_", StringComparison.Ordinal)).ToArray()
                : Array.Empty<Transform>();
        }

        foreach (var tile in tileChildren)
        {
            tile.SetParent(tileRoot, true);
        }

        return tileChildren.Length;
    }

    private static void PrepareTownBoardViewForPrefabSave(GameObject root)
    {
        var boardView = FindComponentInRoot<BoardView>(root);
        if (boardView == null)
        {
            return;
        }

        var serialized = new SerializedObject(boardView);
        var bakedTileRoot = serialized.FindProperty("bakedTileRoot");
        if (bakedTileRoot != null)
        {
            bakedTileRoot.objectReferenceValue = null;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(boardView);
    }

    private static Transform FindTownBoardTilesRoot(Scene scene)
    {
        var appearanceRoot = FindRoot(scene, TownAppearanceRootName);
        return appearanceRoot != null ? FindDirectChild(appearanceRoot.transform, TownBoardTilesRootName) : null;
    }

    private static Transform FindOrCreateTownBoardTilesRoot(Scene scene)
    {
        var appearanceRoot = FindRoot(scene, TownAppearanceRootName);
        if (appearanceRoot == null)
        {
            appearanceRoot = new GameObject(TownAppearanceRootName);
            SceneManager.MoveGameObjectToScene(appearanceRoot, scene);
        }

        var tileRoot = FindDirectChild(appearanceRoot.transform, TownBoardTilesRootName);
        if (tileRoot != null)
        {
            return tileRoot;
        }

        var tileRootGo = new GameObject(TownBoardTilesRootName);
        tileRootGo.transform.SetParent(appearanceRoot.transform, false);
        return tileRootGo.transform;
    }

    private static GameObject RequireRoot(string rootName)
    {
        var scene = SceneManager.GetActiveScene();
        var root = FindRoot(scene, rootName);
        if (root == null)
        {
            throw new InvalidOperationException($"[SceneFunctionalPrefabOrganizer] Missing root '{rootName}' in scene '{scene.path}'.");
        }

        return root;
    }

    private static GameObject FindRoot(Scene scene, string rootName)
    {
        return scene.GetRootGameObjects()
            .FirstOrDefault(go => string.Equals(go.name, rootName, StringComparison.Ordinal));
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        foreach (Transform child in parent)
        {
            if (string.Equals(child.name, childName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        foreach (var child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, childName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static T FindComponentInRoot<T>(GameObject root) where T : Component
    {
        return root != null ? root.GetComponentInChildren<T>(true) : null;
    }

    private static T FindComponentInScene<T>(Scene scene, GameObject preferredRoot = null) where T : Component
    {
        var inRoot = FindComponentInRoot<T>(preferredRoot);
        if (inRoot != null)
        {
            return inRoot;
        }

        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(component => component != null && component.gameObject.scene == scene);
    }

    private static void SortChildren(Transform root, IReadOnlyList<string> orderedNames)
    {
        for (var i = 0; i < orderedNames.Count; i++)
        {
            var child = FindDirectChild(root, orderedNames[i]);
            if (child != null)
            {
                child.SetSiblingIndex(i);
            }
        }
    }

    private static string GetNearestPrefabPath(GameObject gameObject)
    {
        return gameObject != null ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject) : string.Empty;
    }

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void EnsureFolder(string assetFolder)
    {
        var parts = assetFolder.Split('/');
        var current = parts[0];

        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static void Report(bool condition, string okMessage, string errorMessage, ref bool hasError)
    {
        if (condition)
        {
            Debug.Log("[SceneFunctionalPrefabOrganizer] " + okMessage);
            return;
        }

        hasError = true;
        Debug.LogError("[SceneFunctionalPrefabOrganizer] " + errorMessage);
    }

    private readonly struct TransformSnapshot
    {
        private readonly Vector3 _localPosition;
        private readonly Quaternion _localRotation;
        private readonly Vector3 _localScale;
        private readonly bool _active;

        private TransformSnapshot(Vector3 localPosition, Quaternion localRotation, Vector3 localScale, bool active)
        {
            _localPosition = localPosition;
            _localRotation = localRotation;
            _localScale = localScale;
            _active = active;
        }

        public static TransformSnapshot Identity => new(Vector3.zero, Quaternion.identity, Vector3.one, true);

        public static TransformSnapshot From(Transform transform)
            => new(transform.localPosition, transform.localRotation, transform.localScale, transform.gameObject.activeSelf);

        public void Apply(Transform transform)
        {
            transform.localPosition = _localPosition;
            transform.localRotation = _localRotation;
            transform.localScale = _localScale;
            transform.gameObject.SetActive(_active);
        }
    }
}
#endif
