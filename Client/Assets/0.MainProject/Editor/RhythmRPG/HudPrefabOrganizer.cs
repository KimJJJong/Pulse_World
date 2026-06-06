#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HudPrefabOrganizer
{
    private const string MenuRoot = "RhythmRPG/Editors/UI";
    private const string TownMapScenePath = "Assets/0.MainProject/Scenes/Town/TownMap.unity";
    private const string TownForestScenePath = "Assets/0.MainProject/Scenes/Town/Town_Forest.unity";
    private const string InGameHudPrefabPath = "Assets/0.MainProject/Resources/GameInit/Canvas_RhythmHUD.prefab";
    private const string TownHudPrefabFolder = "Assets/0.MainProject/Prefabs/UI/Town";

    private static readonly string[] TownScenePaths =
    {
        TownMapScenePath,
        TownForestScenePath
    };

    private static readonly string[] InGameScenePaths =
    {
        "Assets/0.MainProject/Scenes/Game/Game.unity",
        "Assets/0.MainProject/Scenes/Game/Game_01.unity",
        "Assets/0.MainProject/Scenes/Game/Game_Forest_01.unity",
        "Assets/0.MainProject/Scenes/Game/Game_Forest_Tutorial.unity"
    };

    private static readonly HudRoot[] TownHudRoots =
    {
        new("Canvas_TownExpeditionOverlay", TownHudPrefabFolder + "/PF_Canvas_TownExpeditionOverlay.prefab"),
        new("TownInventory_UI", TownHudPrefabFolder + "/PF_TownInventory_UI.prefab"),
        new("Canvas_TownHomeOverlay", TownHudPrefabFolder + "/PF_Canvas_TownHomeOverlay.prefab")
    };

    [MenuItem(MenuRoot + "/Prefabize Town And InGame HUDs")]
    public static void PrefabizeTownAndInGameHuds()
    {
        string originalScenePath = EditorSceneManager.GetActiveScene().path;

        try
        {
            EnsureFolders();
            SaveTownHudPrefabsFromTownMap();

            foreach (string scenePath in TownScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                EnsureInGameHudInstance(scene);
                EnsureTownHudInstances(scene);
                RebindSceneReferences();
                Save(scene);
            }

            foreach (string scenePath in InGameScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                EnsureInGameHudInstance(scene);
                RebindSceneReferences();
                Save(scene);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HudPrefabOrganizer] Prefabized Town HUDs and unified InGame HUD scene instances.");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(originalScenePath))
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }
    }

    [MenuItem(MenuRoot + "/Verify HUD Prefab Links")]
    public static void VerifyHudPrefabLinks()
    {
        string originalScenePath = EditorSceneManager.GetActiveScene().path;

        try
        {
            foreach (string scenePath in TownScenePaths)
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                RequirePrefabLink("Canvas_RhythmHUD", InGameHudPrefabPath, scenePath);
                foreach (HudRoot root in TownHudRoots)
                    RequirePrefabLink(root.Name, root.PrefabPath, scenePath);
            }

            foreach (string scenePath in InGameScenePaths)
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                RequirePrefabLink("Canvas_RhythmHUD", InGameHudPrefabPath, scenePath);
            }

            Debug.Log("[HudPrefabOrganizer] HUD prefab link verification passed.");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(originalScenePath))
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }
    }

    private static void SaveTownHudPrefabsFromTownMap()
    {
        var scene = EditorSceneManager.OpenScene(TownMapScenePath, OpenSceneMode.Single);

        foreach (HudRoot root in TownHudRoots)
        {
            var source = FindSceneObject(root.Name);
            if (source == null)
                throw new InvalidOperationException($"{root.Name} is missing in {TownMapScenePath}.");

            var connected = PrefabUtility.SaveAsPrefabAssetAndConnect(source, root.PrefabPath, InteractionMode.AutomatedAction);
            if (connected == null)
                throw new InvalidOperationException($"Failed to save prefab: {root.PrefabPath}");
        }

        RebindSceneReferences();
        Save(scene);
    }

    private static void EnsureTownHudInstances(Scene scene)
    {
        foreach (HudRoot root in TownHudRoots)
            EnsurePrefabInstance(root.Name, root.PrefabPath, scene);
    }

    private static void EnsureInGameHudInstance(Scene scene)
    {
        EnsurePrefabInstance("Canvas_RhythmHUD", InGameHudPrefabPath, scene);
    }

    private static void EnsurePrefabInstance(string rootName, string prefabPath, Scene scene)
    {
        GameObject[] existingObjects = FindSceneObjects(rootName);
        GameObject existing = FindPreferredExistingObject(existingObjects, prefabPath);
        if (existing != null && IsConnectedToPrefab(existing, prefabPath))
        {
            DestroyDuplicates(existingObjects, existing);
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            throw new InvalidOperationException($"Prefab not found: {prefabPath}");

        TransformSnapshot snapshot = existing != null
            ? TransformSnapshot.Capture(existing.transform)
            : TransformSnapshot.RootDefault;

        int siblingIndex = existing != null ? existing.transform.GetSiblingIndex() : -1;
        bool activeSelf = existing == null || existing.activeSelf;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
            throw new InvalidOperationException($"Failed to instantiate prefab: {prefabPath}");

        instance.name = rootName;
        snapshot.Apply(instance.transform);
        instance.SetActive(activeSelf);
        if (siblingIndex >= 0)
            instance.transform.SetSiblingIndex(siblingIndex);

        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing);

        DestroyDuplicates(existingObjects, instance);

        EditorUtility.SetDirty(instance);
    }

    private static void RebindSceneReferences()
    {
        var homeController = UnityEngine.Object.FindFirstObjectByType<TownHomeUiController>(FindObjectsInactive.Include);
        var homeOverlay = FindSceneObject("Canvas_TownHomeOverlay");
        if (homeController != null && homeOverlay != null)
        {
            var serializedController = new SerializedObject(homeController);
            serializedController.FindProperty("_root").objectReferenceValue = homeOverlay;
            serializedController.FindProperty("_navigator").objectReferenceValue = homeOverlay.GetComponent<HomeUiPageNavigator>();
            serializedController.FindProperty("_cameraDirector").objectReferenceValue =
                homeController.GetComponent<HomeSceneCameraDirector>() ?? homeOverlay.GetComponent<HomeSceneCameraDirector>();
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(homeController);
        }

        var expeditionPanel = UnityEngine.Object.FindFirstObjectByType<TownExpeditionPanel>(FindObjectsInactive.Include);
        if (expeditionPanel != null)
        {
            var serializedPanel = new SerializedObject(expeditionPanel);
            serializedPanel.FindProperty("_homeUiController").objectReferenceValue = homeController;
            serializedPanel.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(expeditionPanel);
        }

        var sceneContext = UnityEngine.Object.FindFirstObjectByType<BaseSceneContext>(FindObjectsInactive.Include);
        var hud = UnityEngine.Object.FindFirstObjectByType<HudPresenter>(FindObjectsInactive.Include);
        if (sceneContext != null && hud != null)
        {
            var serializedContext = new SerializedObject(sceneContext);
            serializedContext.FindProperty("_hud").objectReferenceValue = hud;
            serializedContext.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sceneContext);
        }
    }

    private static void RequirePrefabLink(string rootName, string prefabPath, string scenePath)
    {
        var roots = FindSceneObjects(rootName);
        if (roots.Length != 1)
            throw new InvalidOperationException($"{rootName} count in {scenePath} is {roots.Length}, expected 1.");

        var root = roots[0];
        if (root == null)
            throw new InvalidOperationException($"{rootName} is missing in {scenePath}.");

        if (!IsConnectedToPrefab(root, prefabPath))
        {
            string currentPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            throw new InvalidOperationException($"{rootName} in {scenePath} is linked to '{currentPath}', expected '{prefabPath}'.");
        }
    }

    private static bool IsConnectedToPrefab(GameObject root, string prefabPath)
    {
        if (root == null)
            return false;

        string currentPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
        return string.Equals(currentPath, prefabPath, StringComparison.Ordinal);
    }

    private static GameObject FindPreferredExistingObject(GameObject[] candidates, string prefabPath)
    {
        foreach (GameObject candidate in candidates)
        {
            if (IsConnectedToPrefab(candidate, prefabPath))
                return candidate;
        }

        return candidates.Length > 0 ? candidates[0] : null;
    }

    private static void DestroyDuplicates(GameObject[] candidates, GameObject keep)
    {
        foreach (GameObject candidate in candidates)
        {
            if (candidate == null || candidate == keep)
                continue;

            UnityEngine.Object.DestroyImmediate(candidate);
        }
    }

    private static GameObject FindSceneObject(string name)
    {
        GameObject[] objects = FindSceneObjects(name);
        return objects.Length > 0 ? objects[0] : null;
    }

    private static GameObject[] FindSceneObjects(string name)
    {
        var scene = SceneManager.GetActiveScene();
        var matches = new System.Collections.Generic.List<GameObject>();

        foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (gameObject == null
                || !gameObject.scene.IsValid()
                || !gameObject.scene.isLoaded
                || gameObject.scene != scene
                || !string.Equals(gameObject.name, name, StringComparison.Ordinal))
            {
                continue;
            }

            matches.Add(gameObject);
        }

        matches.Sort(CompareHierarchyOrder);
        return matches.ToArray();
    }

    private static int CompareHierarchyOrder(GameObject left, GameObject right)
    {
        int leftDepth = GetDepth(left.transform);
        int rightDepth = GetDepth(right.transform);
        if (leftDepth != rightDepth)
            return leftDepth.CompareTo(rightDepth);

        return left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex());
    }

    private static int GetDepth(Transform transform)
    {
        int depth = 0;
        while (transform.parent != null)
        {
            depth++;
            transform = transform.parent;
        }

        return depth;
    }

    private static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException($"Failed to save scene: {scene.path}");
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/0.MainProject/Prefabs");
        EnsureFolder("Assets/0.MainProject/Prefabs/UI");
        EnsureFolder(TownHudPrefabFolder);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(folder);
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException($"Invalid folder path: {folder}");

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private readonly struct HudRoot
    {
        public readonly string Name;
        public readonly string PrefabPath;

        public HudRoot(string name, string prefabPath)
        {
            Name = name;
            PrefabPath = prefabPath;
        }
    }

    private readonly struct TransformSnapshot
    {
        public static readonly TransformSnapshot RootDefault = new(null, -1, Vector3.zero, Quaternion.identity, Vector3.one, null);

        private readonly Transform _parent;
        private readonly int _siblingIndex;
        private readonly Vector3 _localPosition;
        private readonly Quaternion _localRotation;
        private readonly Vector3 _localScale;
        private readonly RectTransformSnapshot? _rectTransform;

        private TransformSnapshot(
            Transform parent,
            int siblingIndex,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            RectTransformSnapshot? rectTransform)
        {
            _parent = parent;
            _siblingIndex = siblingIndex;
            _localPosition = localPosition;
            _localRotation = localRotation;
            _localScale = localScale;
            _rectTransform = rectTransform;
        }

        public static TransformSnapshot Capture(Transform transform)
        {
            RectTransformSnapshot? rectSnapshot = transform is RectTransform rect
                ? RectTransformSnapshot.Capture(rect)
                : null;

            return new TransformSnapshot(
                transform.parent,
                transform.GetSiblingIndex(),
                transform.localPosition,
                transform.localRotation,
                transform.localScale,
                rectSnapshot);
        }

        public void Apply(Transform transform)
        {
            transform.SetParent(_parent, false);
            transform.localPosition = _localPosition;
            transform.localRotation = _localRotation;
            transform.localScale = _localScale;
            if (_siblingIndex >= 0)
                transform.SetSiblingIndex(_siblingIndex);

            if (_rectTransform.HasValue && transform is RectTransform rect)
                _rectTransform.Value.Apply(rect);
        }
    }

    private readonly struct RectTransformSnapshot
    {
        private readonly Vector2 _anchorMin;
        private readonly Vector2 _anchorMax;
        private readonly Vector2 _anchoredPosition;
        private readonly Vector2 _sizeDelta;
        private readonly Vector2 _pivot;

        private RectTransformSnapshot(RectTransform rect)
        {
            _anchorMin = rect.anchorMin;
            _anchorMax = rect.anchorMax;
            _anchoredPosition = rect.anchoredPosition;
            _sizeDelta = rect.sizeDelta;
            _pivot = rect.pivot;
        }

        public static RectTransformSnapshot Capture(RectTransform rect) => new(rect);

        public void Apply(RectTransform rect)
        {
            rect.anchorMin = _anchorMin;
            rect.anchorMax = _anchorMax;
            rect.anchoredPosition = _anchoredPosition;
            rect.sizeDelta = _sizeDelta;
            rect.pivot = _pivot;
        }
    }
}
#endif
