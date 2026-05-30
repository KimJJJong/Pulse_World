#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class TownForestStoneDecorationScatter
{
    private const string ScenePath = "Assets/0.MainProject/Scenes/Town/Town_Forest.unity";
    private const string StoneSourceRootName = "Stone_Decoration";
    private const string PlacementRootName = "Codex_Forest_Stone_Scatter";
    private const int TargetStoneCount = 132;
    private const int RandomSeed = 20260529;

    [MenuItem("RhythmRPG/Editors/World/Scatter Stone Decoration Between Forest Trees")]
    public static void Scatter()
    {
        var scene = EnsureTownForestScene();
        var stoneSourceRoot = GameObject.Find(StoneSourceRootName);
        if (stoneSourceRoot == null)
        {
            Debug.LogError("[TownForestStoneDecorationScatter] Missing Stone_Decoration root.");
            return;
        }

        ClearPreviousPlacement();

        var sourceStones = CollectSourceStones(stoneSourceRoot.transform);
        var pinePoints = CollectPinePoints(scene);
        if (sourceStones.Count == 0 || pinePoints.Count < 2)
        {
            Debug.LogError(
                "[TownForestStoneDecorationScatter] Not enough source stones or pine trees. " +
                $"Sources={sourceStones.Count}, Pines={pinePoints.Count}");
            return;
        }

        var root = new GameObject(PlacementRootName);
        root.transform.SetParent(stoneSourceRoot.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        var rng = new System.Random(RandomSeed);
        var candidates = BuildBetweenTreeCandidates(pinePoints, rng);
        Shuffle(candidates, rng);

        var placedPositions = new List<Vector2>();
        var usageCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var placedCount = 0;

        foreach (var candidate in candidates)
        {
            if (placedCount >= TargetStoneCount)
            {
                break;
            }

            if (!CanPlaceAt(candidate, pinePoints, placedPositions, 1.05f))
            {
                continue;
            }

            PlaceStoneInstance(sourceStones, root.transform, candidate, rng, placedCount, usageCounts);
            placedPositions.Add(candidate);
            placedCount++;

            if (placedCount >= TargetStoneCount || rng.NextDouble() > 0.38)
            {
                continue;
            }

            var clusterOffset = RandomUnitVector(rng) * RandomRange(rng, 0.72f, 1.42f);
            var clusterCandidate = candidate + clusterOffset;
            if (!CanPlaceAt(clusterCandidate, pinePoints, placedPositions, 0.82f))
            {
                continue;
            }

            PlaceStoneInstance(sourceStones, root.transform, clusterCandidate, rng, placedCount, usageCounts);
            placedPositions.Add(clusterCandidate);
            placedCount++;
        }

        if (placedCount < TargetStoneCount)
        {
            FillRemainingFromPineClusters(sourceStones, root.transform, pinePoints, placedPositions, rng, usageCounts, ref placedCount);
        }

        Selection.activeGameObject = root;
        FrameSceneView(root);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[TownForestStoneDecorationScatter] Stone scatter complete. " +
            $"Scene={scene.name}, Root={GetPath(root.transform)}, Stones={placedCount}, " +
            $"Pines={pinePoints.Count}, SourceTypes={usageCounts.Count}, " +
            $"Usage={string.Join(", ", usageCounts.Select(kvp => kvp.Key + ":" + kvp.Value))}");
    }

    [MenuItem("RhythmRPG/Editors/World/Validate Stone Decoration Forest Scatter")]
    public static void Validate()
    {
        var root = GameObject.Find(PlacementRootName);
        if (root == null)
        {
            Debug.LogError("[TownForestStoneDecorationScatter] Validation failed: scatter root not found.");
            return;
        }

        var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        if (root.transform.childCount < TargetStoneCount || renderers.Length < TargetStoneCount)
        {
            Debug.LogError(
                "[TownForestStoneDecorationScatter] Validation failed: not enough stones were placed. " +
                $"Children={root.transform.childCount}, Renderers={renderers.Length}, Expected={TargetStoneCount}");
            return;
        }

        Debug.Log(
            "[TownForestStoneDecorationScatter] VALIDATION OK. " +
            $"Root={GetPath(root.transform)}, Children={root.transform.childCount}, Renderers={renderers.Length}.");
    }

    private static Scene EnsureTownForestScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.path == ScenePath)
        {
            return scene;
        }

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static void ClearPreviousPlacement()
    {
        var oldRoots = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(go => go.name == PlacementRootName)
            .ToArray();

        foreach (var oldRoot in oldRoots)
        {
            Object.DestroyImmediate(oldRoot);
        }
    }

    private static List<GameObject> CollectSourceStones(Transform stoneSourceRoot)
    {
        var stones = new List<GameObject>();
        foreach (Transform child in stoneSourceRoot)
        {
            if (child.name == PlacementRootName)
            {
                continue;
            }

            if (child.GetComponentInChildren<MeshRenderer>(true) != null)
            {
                stones.Add(child.gameObject);
            }
        }

        return stones;
    }

    private static List<TreePoint> CollectPinePoints(Scene scene)
    {
        return Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(go => go.scene == scene)
            .Where(go => go.activeInHierarchy)
            .Where(go => go.name.IndexOf("Pine", StringComparison.OrdinalIgnoreCase) >= 0)
            .Where(go => go.GetComponent<MeshRenderer>() != null)
            .Select(go => new TreePoint(go.name, new Vector2(go.transform.position.x, go.transform.position.z)))
            .OrderBy(point => point.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static List<Vector2> BuildBetweenTreeCandidates(IReadOnlyList<TreePoint> pinePoints, System.Random rng)
    {
        var candidates = new List<Vector2>();
        for (var i = 0; i < pinePoints.Count; i++)
        {
            var current = pinePoints[i];
            var neighbors = pinePoints
                .Where((point, index) => index != i)
                .Select(point => new
                {
                    Point = point,
                    Distance = Vector2.Distance(current.Position, point.Position)
                })
                .Where(item => item.Distance >= 2.35f && item.Distance <= 7.25f)
                .OrderBy(item => item.Distance)
                .Take(5)
                .ToArray();

            foreach (var neighbor in neighbors)
            {
                if (string.CompareOrdinal(neighbor.Point.Name, current.Name) <= 0)
                {
                    continue;
                }

                var a = current.Position;
                var b = neighbor.Point.Position;
                var direction = (b - a).normalized;
                var perpendicular = new Vector2(-direction.y, direction.x);
                var midpoint = (a + b) * 0.5f;
                var variants = rng.NextDouble() < 0.55 ? 2 : 1;

                for (var variant = 0; variant < variants; variant++)
                {
                    var along = direction * RandomRange(rng, -0.55f, 0.55f);
                    var side = perpendicular * RandomRange(rng, -1.25f, 1.25f);
                    candidates.Add(midpoint + along + side);
                }
            }
        }

        return candidates;
    }

    private static void FillRemainingFromPineClusters(
        IReadOnlyList<GameObject> sourceStones,
        Transform root,
        IReadOnlyList<TreePoint> pinePoints,
        List<Vector2> placedPositions,
        System.Random rng,
        Dictionary<string, int> usageCounts,
        ref int placedCount)
    {
        var attempts = 0;
        while (placedCount < TargetStoneCount && attempts < TargetStoneCount * 80)
        {
            attempts++;
            var anchor = pinePoints[rng.Next(pinePoints.Count)];
            var neighbor = pinePoints
                .Where(point => point.Name != anchor.Name)
                .OrderBy(point => Vector2.Distance(anchor.Position, point.Position))
                .Skip(rng.Next(0, 4))
                .FirstOrDefault();

            if (neighbor.Name == null)
            {
                continue;
            }

            var midpoint = (anchor.Position + neighbor.Position) * 0.5f;
            var candidate = midpoint + RandomUnitVector(rng) * RandomRange(rng, 0.35f, 1.85f);
            if (!CanPlaceAt(candidate, pinePoints, placedPositions, 0.9f))
            {
                continue;
            }

            PlaceStoneInstance(sourceStones, root, candidate, rng, placedCount, usageCounts);
            placedPositions.Add(candidate);
            placedCount++;
        }
    }

    private static bool CanPlaceAt(
        Vector2 candidate,
        IReadOnlyList<TreePoint> pinePoints,
        IReadOnlyList<Vector2> placedPositions,
        float minStoneSpacing)
    {
        var nearestTreeDistance = float.MaxValue;
        var nearbyTreeCount = 0;
        foreach (var pinePoint in pinePoints)
        {
            var distance = Vector2.Distance(candidate, pinePoint.Position);
            nearestTreeDistance = Mathf.Min(nearestTreeDistance, distance);
            if (distance <= 5.7f)
            {
                nearbyTreeCount++;
            }
        }

        if (nearestTreeDistance < 1.05f || nearestTreeDistance > 4.85f || nearbyTreeCount < 2)
        {
            return false;
        }

        foreach (var placed in placedPositions)
        {
            if (Vector2.Distance(candidate, placed) < minStoneSpacing)
            {
                return false;
            }
        }

        return true;
    }

    private static void PlaceStoneInstance(
        IReadOnlyList<GameObject> sourceStones,
        Transform root,
        Vector2 position,
        System.Random rng,
        int index,
        Dictionary<string, int> usageCounts)
    {
        var source = PickStoneSource(sourceStones, rng);
        var instance = Object.Instantiate(source, root);
        var label = GetShortSourceLabel(source.name);
        instance.name = $"ForestStone_{index + 1:000}_{label}";
        instance.transform.position = new Vector3(
            position.x,
            source.transform.position.y + RandomRange(rng, -0.035f, 0.045f),
            position.y);

        var sourceEuler = source.transform.rotation.eulerAngles;
        instance.transform.rotation = Quaternion.Euler(sourceEuler.x, RandomRange(rng, 0f, 360f), sourceEuler.z);
        instance.transform.localScale = source.transform.localScale * GetScaleMultiplier(source.name, rng);

        if (!usageCounts.ContainsKey(label))
        {
            usageCounts[label] = 0;
        }

        usageCounts[label]++;
    }

    private static GameObject PickStoneSource(IReadOnlyList<GameObject> sourceStones, System.Random rng)
    {
        var roll = rng.NextDouble();
        if (roll < 0.24)
        {
            return PickByKeyword(sourceStones, "Stone_Stack", rng);
        }

        if (roll < 0.46)
        {
            return PickByKeyword(sourceStones, "Mossy_Boulder", rng);
        }

        if (roll < 0.67)
        {
            return PickByKeyword(sourceStones, "Faceted", rng);
        }

        if (roll < 0.91)
        {
            return PickByKeyword(sourceStones, "Moss_Covered_Azure", rng);
        }

        return PickByKeyword(sourceStones, "Monolith", rng);
    }

    private static GameObject PickByKeyword(IReadOnlyList<GameObject> sourceStones, string keyword, System.Random rng)
    {
        var matches = sourceStones
            .Where(stone => stone.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToArray();

        if (matches.Length == 0)
        {
            return sourceStones[rng.Next(sourceStones.Count)];
        }

        return matches[rng.Next(matches.Length)];
    }

    private static float GetScaleMultiplier(string sourceName, System.Random rng)
    {
        if (sourceName.IndexOf("Monolith", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return RandomRange(rng, 0.42f, 0.58f);
        }

        if (sourceName.IndexOf("Azure", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return RandomRange(rng, 0.52f, 0.78f);
        }

        if (sourceName.IndexOf("Stone_Stack", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return RandomRange(rng, 0.58f, 0.86f);
        }

        return RandomRange(rng, 0.50f, 0.76f);
    }

    private static string GetShortSourceLabel(string sourceName)
    {
        if (sourceName.IndexOf("Stone_Stack", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "MossyStoneStack";
        }

        if (sourceName.IndexOf("Faceted", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "MossyFacetedBoulder";
        }

        if (sourceName.IndexOf("Mossy_Boulder", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "MossyBoulder";
        }

        if (sourceName.IndexOf("Monolith", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "MossyMonolith";
        }

        if (sourceName.IndexOf("Azure", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "AzureMossStone";
        }

        return "Stone";
    }

    private static Vector2 RandomUnitVector(System.Random rng)
    {
        var angle = RandomRange(rng, 0f, Mathf.PI * 2f);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static float RandomRange(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    private static void Shuffle<T>(IList<T> items, System.Random rng)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            var temp = items[i];
            items[i] = items[j];
            items[j] = temp;
        }
    }

    private static void FrameSceneView(GameObject target)
    {
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            return;
        }

        sceneView.Frame(new Bounds(target.transform.position, new Vector3(38f, 8f, 38f)), false);
        sceneView.Repaint();
    }

    private static string GetPath(Transform transform)
    {
        var names = new Stack<string>();
        var current = transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private readonly struct TreePoint
    {
        public readonly string Name;
        public readonly Vector2 Position;

        public TreePoint(string name, Vector2 position)
        {
            Name = name;
            Position = position;
        }
    }
}
#endif
