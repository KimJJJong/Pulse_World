#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ForestTutorialHierarchyOrganizer
{
    private const string ScenePath = "Assets/0.MainProject/Scenes/Game/Game_Forest_Tutorial.unity";
    private const string EnvironmentRootName = "Forest_Decoration_Set";
    private const string MeshyGroupName = "Generated_Meshy_Props";
    private const string HandPlacedPropsName = "Props_HandPlaced";
    private const string LocalFogRootName = "FX_FullscreenDepthFog";

    [MenuItem("RhythmRPG/Editors/World/Organize Forest Tutorial Hierarchy")]
    public static void Organize()
    {
        OpenTargetScene();

        var environmentRoot = GameObject.Find(EnvironmentRootName);
        if (environmentRoot == null)
        {
            Debug.LogError($"[ForestTutorialHierarchyOrganizer] Missing root: {EnvironmentRootName}");
            return;
        }

        var renamed = 0;
        var moved = 0;

        renamed += RenameForestVisualSamples(environmentRoot.transform);
        renamed += RenameIfFound(environmentRoot.transform, "LightingPipeline_Showcase", "Showcase_LightingPipeline");
        moved += MoveLocalFogUnderEnvironment(environmentRoot.transform);
        moved += GroupMeshyRoots(environmentRoot.transform);
        moved += GroupHandPlacedProps(environmentRoot.transform);
        SortEnvironmentChildren(environmentRoot.transform);
        SortSceneRoots();

        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[ForestTutorialHierarchyOrganizer] Organized hierarchy. Renamed={renamed}, Moved={moved}, Scene={scene.name}.");
    }

    [MenuItem("RhythmRPG/Editors/World/Validate Forest Tutorial Hierarchy")]
    public static void Validate()
    {
        OpenTargetScene();

        var environmentRoot = GameObject.Find(EnvironmentRootName);
        var meshyRoot = environmentRoot != null ? FindDirectChild(environmentRoot.transform, MeshyGroupName) : null;
        var propsRoot = environmentRoot != null ? FindDirectChild(environmentRoot.transform, HandPlacedPropsName) : null;
        var fogRoot = GameObject.Find(LocalFogRootName);
        var topLevelMeshy = SceneManager.GetActiveScene()
            .GetRootGameObjects()
            .Count(go => go.name.StartsWith("Meshy_AI_", StringComparison.Ordinal));

        if (environmentRoot == null || meshyRoot == null || propsRoot == null || fogRoot == null || topLevelMeshy > 0)
        {
            Debug.LogError(
                "[ForestTutorialHierarchyOrganizer] Validation failed. " +
                $"EnvironmentRoot={(environmentRoot != null)}, MeshyRoot={(meshyRoot != null)}, " +
                $"PropsRoot={(propsRoot != null)}, FogRoot={(fogRoot != null)}, TopLevelMeshy={topLevelMeshy}.");
            return;
        }

        Debug.Log(
            "[ForestTutorialHierarchyOrganizer] VALIDATION OK. " +
            $"EnvironmentChildren={environmentRoot.transform.childCount}, " +
            $"MeshyGroups={meshyRoot.childCount}, HandPlacedProps={propsRoot.childCount}, TopLevelMeshy={topLevelMeshy}.");
    }

    private static void OpenTargetScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }

    private static int RenameForestVisualSamples(Transform environmentRoot)
    {
        var renamed = 0;
        var samples = FindDirectChild(environmentRoot, "Forest_Visual_Samples") ?? FindDirectChild(environmentRoot, "Samples_ForestVisual");
        if (samples == null)
        {
            return 0;
        }

        if (samples.name != "Samples_ForestVisual")
        {
            samples.name = "Samples_ForestVisual";
            renamed++;
        }

        renamed += RenameIfFound(samples, "Scene_Lighting_Sample", "Sample_SceneLighting");
        renamed += RenameIfFound(samples, "Crystal", "Crystal_Gate_01");
        renamed += RenameIfFound(samples, "Crystal (1)", "Crystal_Gate_02");
        renamed += RenameIfFound(samples, "Latern", "Lantern_Warm_01");
        renamed += RenameIfFound(samples, "Latern (1)", "Lantern_Warm_02");
        renamed += RenameIfFound(samples, "Latern (2)", "Lantern_Warm_03");
        return renamed;
    }

    private static int MoveLocalFogUnderEnvironment(Transform environmentRoot)
    {
        var moved = 0;
        var fogRoot = GameObject.Find(LocalFogRootName);
        if (fogRoot == null)
        {
            return moved;
        }

        if (fogRoot.transform.parent != environmentRoot)
        {
            fogRoot.transform.SetParent(environmentRoot, true);
            moved++;
        }

        RenameIfFound(fogRoot.transform, "DepthFog_Boundary_Gate", "FogBoundary_Gate");

        return moved;
    }

    private static int GroupMeshyRoots(Transform environmentRoot)
    {
        var moved = 0;
        var meshyRoot = FindOrCreateChild(environmentRoot, MeshyGroupName);
        var roots = SceneManager.GetActiveScene()
            .GetRootGameObjects()
            .Where(go => go.name.StartsWith("Meshy_AI_", StringComparison.Ordinal))
            .OrderBy(go => go.name, StringComparer.Ordinal)
            .ToList();

        var counters = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            var category = GetMeshyCategory(root.name);
            var categoryRoot = FindOrCreateChild(meshyRoot, category.GroupName);
            counters.TryGetValue(category.Prefix, out var count);
            count++;
            counters[category.Prefix] = count;

            root.transform.SetParent(categoryRoot, true);
            root.name = $"{category.Prefix}_{count:000}";
            moved++;
        }

        return moved;
    }

    private static int GroupHandPlacedProps(Transform environmentRoot)
    {
        var propsRoot = FindOrCreateChild(environmentRoot, HandPlacedPropsName);
        var reservedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Samples_ForestVisual",
            "Forest_Visual_Samples",
            "Showcase_LightingPipeline",
            "LightingPipeline_Showcase",
            LocalFogRootName,
            "AreaFog_RoadClear_Zones",
            MeshyGroupName,
            HandPlacedPropsName
        };

        var children = new List<Transform>();
        foreach (Transform child in environmentRoot)
        {
            if (!reservedNames.Contains(child.name))
            {
                children.Add(child);
            }
        }

        foreach (var child in children)
        {
            child.SetParent(propsRoot, true);
        }

        return children.Count;
    }

    private static (string GroupName, string Prefix) GetMeshyCategory(string objectName)
    {
        if (objectName.Contains("Emerald_Pine", StringComparison.Ordinal))
        {
            return ("Trees_EmeraldPine", "Tree_EmeraldPine");
        }

        if (objectName.Contains("Origami_Pine", StringComparison.Ordinal))
        {
            return ("Trees_OrigamiPine", "Tree_OrigamiPine");
        }

        if (objectName.Contains("Stone_Stack", StringComparison.Ordinal))
        {
            return ("Rocks_StoneStack", "Rock_StoneStack");
        }

        if (objectName.Contains("Mossy_Stone_Monolith", StringComparison.Ordinal))
        {
            return ("Stones_MossyMonolith", "Stone_MossyMonolith");
        }

        if (objectName.Contains("Broken_wooden_fence", StringComparison.Ordinal))
        {
            return ("Fences_BrokenWooden", "Fence_BrokenWooden");
        }

        return ("Misc_MeshyProps", "Prop_Meshy");
    }

    private static int RenameIfFound(Transform parent, string oldName, string newName)
    {
        var child = FindDirectChild(parent, oldName);
        if (child == null || child.name == newName)
        {
            return 0;
        }

        child.name = newName;
        return 1;
    }

    private static void RenameChildrenBySuffix(Transform parent, string suffix, string newName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.EndsWith(suffix, StringComparison.Ordinal))
            {
                child.name = newName;
            }
        }
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        var existing = FindDirectChild(parent, name);
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static void SortEnvironmentChildren(Transform environmentRoot)
    {
        SetSibling(environmentRoot, "Samples_ForestVisual", 0);
        SetSibling(environmentRoot, "Showcase_LightingPipeline", 1);
        SetSibling(environmentRoot, LocalFogRootName, 2);
        SetSibling(environmentRoot, HandPlacedPropsName, 3);
        SetSibling(environmentRoot, MeshyGroupName, 4);
    }

    private static void SortSceneRoots()
    {
        var gameplayRoot = GameObject.Find("Gameplay_Functions");
        var environmentRoot = GameObject.Find(EnvironmentRootName);
        gameplayRoot?.transform.SetSiblingIndex(0);
        environmentRoot?.transform.SetSiblingIndex(1);
    }

    private static void SetSibling(Transform parent, string childName, int index)
    {
        var child = FindDirectChild(parent, childName);
        child?.SetSiblingIndex(index);
    }
}
#endif
