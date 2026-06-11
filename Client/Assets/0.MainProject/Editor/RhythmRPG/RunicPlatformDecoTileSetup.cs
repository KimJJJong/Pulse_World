#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RunicPlatformDecoTileSetup
{
    private const string PlatformPrefabPath = "Assets/Resources/Prefabs/Interaction/Runic_Circle_Platform.prefab";
    private const string TwinStoneBricksPath =
        "Assets/Resources/Prefabs/TowerEvent/Twin_Stone_Bricks/Meshy_AI_Twin_Stone_Bricks_0610121204_texture_fbx/Meshy_AI_Twin_Stone_Bricks_0610121204_texture.fbx";

    private const string DecoRootName = "Deco_Tiles";
    private const string TileNamePrefix = "Twin_Stone_Bridcks";
    private const string DecoTileMaterialPath = "Assets/0.MainProject/Art/ForestLightingPipeline/M_Runic_Circle_Platform_Deco_Tile_Stone.mat";
    private const string LitShaderName = "Universal Render Pipeline/Lit";

    [MenuItem("RhythmRPG/Editors/Interaction/Apply Runic Platform Deco Tiles")]
    public static void Apply()
    {
        var tileAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TwinStoneBricksPath);
        if (tileAsset == null)
        {
            Debug.LogError($"[RunicPlatformDecoTileSetup] Tile asset not found: {TwinStoneBricksPath}");
            return;
        }

        var prefabRoot = PrefabUtility.LoadPrefabContents(PlatformPrefabPath);
        try
        {
            var decoRoot = FindOrCreateDirectChild(prefabRoot.transform, DecoRootName);
            decoRoot.transform.localPosition = Vector3.zero;
            decoRoot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            decoRoot.transform.localScale = Vector3.one;
            ClearGeneratedTiles(decoRoot.transform);
            var stoneMaterial = CreateOrUpdateDecoTileMaterial(GetPlatformStoneMaterial(prefabRoot));
            if (stoneMaterial == null)
            {
                Debug.LogWarning("[RunicPlatformDecoTileSetup] Platform stone material not found. Twin_Stone_Bricks source material will be kept.");
            }

            var sourceBounds = MeasureSourceGroundBounds(tileAsset, decoRoot.transform);
            if (!sourceBounds.IsValid)
            {
                Debug.LogError("[RunicPlatformDecoTileSetup] Could not measure Twin_Stone_Bricks bounds.");
                return;
            }

            var platformRadius = MeasurePlatformRadius(prefabRoot);
            var ringRadius = Mathf.Max(platformRadius * 1.18f, 0.0125f);
            var targetRadialWidth = Mathf.Clamp(platformRadius * 0.13f, 0.00105f, 0.00185f);
            var tileScale = Mathf.Clamp(targetRadialWidth / sourceBounds.RadialWidth, 0.0005f, 2.2f);
            var circumference = Mathf.PI * 2f * ringRadius;
            var tightPitch = Mathf.Max(sourceBounds.TangentLength * tileScale * 0.9f, 0.0001f);
            var tileCount = Mathf.Clamp(Mathf.RoundToInt(circumference / tightPitch), 42, 160);

            var created = new List<GameObject>(256);
            AddTightBrickCircle(tileAsset, decoRoot.transform, created, stoneMaterial, ringRadius, tileCount, tileScale);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlatformPrefabPath);
            Debug.Log($"[RunicPlatformDecoTileSetup] Applied tight brick circle. Tiles={created.Count}, Radius={ringRadius:F6}, Scale={tileScale:F6}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("RhythmRPG/Editors/Interaction/Validate Runic Platform Deco Tiles")]
    public static void Validate()
    {
        var prefabRoot = PrefabUtility.LoadPrefabContents(PlatformPrefabPath);
        try
        {
            var decoRoot = FindDirectChild(prefabRoot.transform, DecoRootName);
            var count = 0;
            if (decoRoot != null)
            {
                for (var i = 0; i < decoRoot.childCount; i++)
                {
                    if (decoRoot.GetChild(i).name.StartsWith(TileNamePrefix, StringComparison.Ordinal))
                    {
                        count++;
                    }
                }
            }

            if (decoRoot == null || count < 48)
            {
                Debug.LogError($"[RunicPlatformDecoTileSetup] Validation failed. DecoRoot={decoRoot != null}, TileCount={count}");
                return;
            }

            Debug.Log($"[RunicPlatformDecoTileSetup] VALIDATION OK. DecoRoot=True, TileCount={count}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void AddTightBrickCircle(
        GameObject tileAsset,
        Transform parent,
        List<GameObject> created,
        Material stoneMaterial,
        float radius,
        int count,
        float scale)
    {
        const float height = 0.00008f;
        for (var i = 0; i < count; i++)
        {
            var angle01 = (float)i / count;
            var angle = angle01 * Mathf.PI * 2f;
            var angleDegrees = angle * Mathf.Rad2Deg;

            var tile = CreateTile(tileAsset, parent, $"{TileNamePrefix}_Circle_{i:00}", stoneMaterial);
            tile.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            tile.transform.localRotation = Quaternion.Euler(0f, angleDegrees, 0f);
            tile.transform.localScale = Vector3.one * scale;
            created.Add(tile);
        }
    }

    [MenuItem("RhythmRPG/Editors/Interaction/Frame Runic Platform Deco Preview")]
    public static void FramePreview()
    {
        var sceneInstance = GameObject.Find("Runic_Circle_Platform");
        if (sceneInstance == null)
        {
            Debug.LogWarning("[RunicPlatformDecoTileSetup] No Runic_Circle_Platform instance found in the active scene.");
            return;
        }

        var bounds = CalculateRendererBounds(sceneInstance);
        var sceneDecoRoot = GameObject.Find(DecoRootName);
        if (sceneDecoRoot != null)
        {
            bounds.Encapsulate(CalculateRendererBounds(sceneDecoRoot));
        }

        var viewDirection = new Vector3(0.38f, -0.72f, 0.58f).normalized;
        var size = Mathf.Max(4.2f, bounds.extents.magnitude * 0.82f);

        foreach (SceneView sceneView in SceneView.sceneViews)
        {
            sceneView.LookAt(bounds.center, Quaternion.LookRotation(viewDirection, Vector3.up), size, false, true);
            sceneView.Repaint();
        }

        Selection.activeObject = null;
        Debug.Log("[RunicPlatformDecoTileSetup] Framed Runic_Circle_Platform for deco tile preview.");
    }

    [MenuItem("RhythmRPG/Editors/Interaction/Apply Scene Twin Stone Brick Circle")]
    public static void ApplySceneTwinStoneBrickCircle()
    {
        var platform = GameObject.Find("Runic_Circle_Platform");
        var decoRoot = GameObject.Find(DecoRootName);
        if (platform == null || decoRoot == null)
        {
            Debug.LogError($"[RunicPlatformDecoTileSetup] Scene objects missing. Platform={platform != null}, DecoRoot={decoRoot != null}");
            return;
        }

        var source = FindSceneTwinStoneBrick(decoRoot.transform);
        if (source == null)
        {
            Debug.LogError($"[RunicPlatformDecoTileSetup] No Twin_Stone_Bricks child found under scene {DecoRootName}.");
            return;
        }

        var templatePrefab = PrefabUtility.GetCorrespondingObjectFromSource(source) as GameObject;
        var templateClone = templatePrefab == null ? UnityEngine.Object.Instantiate(source) : null;
        if (templateClone != null)
        {
            templateClone.name = "__Temp_Twin_Stone_Bricks_Template";
            templateClone.hideFlags = HideFlags.HideAndDontSave;
        }

        var sourceScale = source.transform.localScale;
        var sourceY = source.transform.position.y;
        var sourceBounds = CalculateRendererBounds(source);
        var sourceLength = Mathf.Max(sourceBounds.size.x, sourceBounds.size.z);
        var platformRenderer = platform.GetComponent<Renderer>();
        var platformRadius = platformRenderer != null
            ? Mathf.Max(platformRenderer.bounds.extents.x, platformRenderer.bounds.extents.z)
            : 4.2f;
        var radius = Mathf.Max(platformRadius * 1.08f, platformRadius + sourceLength * 1.35f);
        var circumference = Mathf.PI * 2f * radius;
        var tileCount = Mathf.Clamp(Mathf.RoundToInt(circumference / Mathf.Max(sourceLength * 0.9f, 0.1f)), 36, 64);

        ClearSceneTwinStoneBricks(decoRoot.transform);
        decoRoot.transform.position = new Vector3(platform.transform.position.x, 0f, platform.transform.position.z);
        decoRoot.transform.rotation = Quaternion.identity;
        decoRoot.transform.localScale = Vector3.one;

        for (var i = 0; i < tileCount; i++)
        {
            var angle01 = (float)i / tileCount;
            var angle = angle01 * Mathf.PI * 2f;
            var angleDegrees = angle * Mathf.Rad2Deg;
            var brick = templatePrefab != null
                ? PrefabUtility.InstantiatePrefab(templatePrefab, decoRoot.transform) as GameObject
                : UnityEngine.Object.Instantiate(templateClone, decoRoot.transform);
            if (brick == null)
            {
                continue;
            }

            brick.name = $"Twin_Stone_Bricks_Circle_{i:00}";
            brick.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, sourceY, Mathf.Sin(angle) * radius);
            brick.transform.localRotation = Quaternion.Euler(0f, angleDegrees, 0f);
            brick.transform.localScale = sourceScale;
            brick.SetActive(true);
            EditorUtility.SetDirty(brick);
        }

        if (templateClone != null)
        {
            UnityEngine.Object.DestroyImmediate(templateClone);
        }

        EditorUtility.SetDirty(decoRoot);
        EditorSceneManager.MarkSceneDirty(decoRoot.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[RunicPlatformDecoTileSetup] Applied scene Twin_Stone_Bricks circle. Count={tileCount}, Radius={radius:F2}, YRotationStep={360f / tileCount:F2}.");
    }

    private static void AddRing(
        GameObject tileAsset,
        Transform parent,
        List<GameObject> created,
        Material stoneMaterial,
        string label,
        float radius,
        int count,
        float targetWorldSize,
        float sourceLength,
        float offsetDegrees,
        int missingModulo)
    {
        for (var i = 0; i < count; i++)
        {
            if (missingModulo > 0 && i % missingModulo == missingModulo - 1)
            {
                continue;
            }

            var t = (float)i / count;
            var angle = t * Mathf.PI * 2f + offsetDegrees * Mathf.Deg2Rad;
            var variation = Mathf.Sin(i * 1.73f) * 0.00016f + Mathf.Cos(i * 0.91f) * 0.0001f;
            var radial = radius + variation;
            var height = 0.00006f + (i % 4) * 0.000003f;
            var tangentDegrees = angle * Mathf.Rad2Deg + 90f + Mathf.Sin(i * 2.11f) * 5f;

            var tile = CreateTile(tileAsset, parent, $"{TileNamePrefix}_{label}_{i:00}", stoneMaterial);
            tile.transform.localPosition = new Vector3(Mathf.Cos(angle) * radial, Mathf.Sin(angle) * radial, height);
            tile.transform.localRotation = GetGroundTileRotation(tangentDegrees);
            tile.transform.localScale = Vector3.one * GetScale(sourceLength, targetWorldSize, 0.8f + Mathf.Abs(Mathf.Sin(i * 0.67f)) * 0.18f);
            created.Add(tile);
        }
    }

    private static void AddBrokenOuterRing(GameObject tileAsset, Transform parent, List<GameObject> created, Material stoneMaterial, float sourceLength)
    {
        for (var i = 0; i < 36; i++)
        {
            if (i == 3 || i == 9 || i == 16 || i == 22 || i == 31)
            {
                continue;
            }

            var angle = ((float)i / 36f) * Mathf.PI * 2f;
            var radial = 0.0192f + Mathf.Sin(i * 0.83f) * 0.00034f;
            var tangentDegrees = angle * Mathf.Rad2Deg + 90f + Mathf.Cos(i * 1.19f) * 8f;
            var tile = CreateTile(tileAsset, parent, $"{TileNamePrefix}_FarBroken_{i:00}", stoneMaterial);
            tile.transform.localPosition = new Vector3(Mathf.Cos(angle) * radial, Mathf.Sin(angle) * radial, 0.000056f);
            tile.transform.localRotation = GetGroundTileRotation(tangentDegrees);
            tile.transform.localScale = Vector3.one * GetScale(sourceLength, 0.29f, 0.7f + Mathf.Abs(Mathf.Cos(i * 0.57f)) * 0.22f);
            created.Add(tile);
        }
    }

    private static void AddLooseTiles(GameObject tileAsset, Transform parent, List<GameObject> created, Material stoneMaterial, float sourceLength)
    {
        var placements = new[]
        {
            new LooseTile(22f, 0.0068f, 0.2f, 0.74f),
            new LooseTile(51f, 0.0166f, 0.22f, 0.56f),
            new LooseTile(96f, 0.0124f, 0.2f, 0.68f),
            new LooseTile(136f, 0.0194f, 0.24f, 0.62f),
            new LooseTile(188f, 0.0158f, 0.21f, 0.58f),
            new LooseTile(229f, 0.0077f, 0.2f, 0.7f),
            new LooseTile(273f, 0.0188f, 0.23f, 0.54f),
            new LooseTile(318f, 0.0136f, 0.21f, 0.64f),
            new LooseTile(342f, 0.0104f, 0.18f, 0.7f),
            new LooseTile(158f, 0.0108f, 0.18f, 0.66f),
        };

        for (var i = 0; i < placements.Length; i++)
        {
            var placement = placements[i];
            var angle = placement.Degrees * Mathf.Deg2Rad;
            var tangentDegrees = placement.Degrees + 114f;
            var tile = CreateTile(tileAsset, parent, $"{TileNamePrefix}_Loose_{i:00}", stoneMaterial);
            tile.transform.localPosition = new Vector3(Mathf.Cos(angle) * placement.Radius, Mathf.Sin(angle) * placement.Radius, 0.00007f);
            tile.transform.localRotation = GetGroundTileRotation(tangentDegrees);
            tile.transform.localScale = Vector3.one * GetScale(sourceLength, placement.TargetWorldSize, placement.ScaleBias);
            created.Add(tile);
        }
    }

    private static Quaternion GetGroundTileRotation(float tangentDegrees)
    {
        return Quaternion.AngleAxis(tangentDegrees, Vector3.forward) * Quaternion.AngleAxis(90f, Vector3.right);
    }

    private static GameObject CreateTile(GameObject tileAsset, Transform parent, string name, Material stoneMaterial)
    {
        var instance = PrefabUtility.InstantiatePrefab(tileAsset, parent) as GameObject;
        if (instance == null)
        {
            throw new InvalidOperationException($"Could not instantiate tile asset: {TwinStoneBricksPath}");
        }

        instance.name = name;
        if (stoneMaterial != null)
        {
            ApplySharedMaterial(instance, stoneMaterial);
        }

        return instance;
    }

    private static GameObject FindSceneTwinStoneBrick(Transform decoRoot)
    {
        for (var i = 0; i < decoRoot.childCount; i++)
        {
            var child = decoRoot.GetChild(i);
            if (child.name.StartsWith("Twin_Stone_Bricks", StringComparison.Ordinal)
                || child.name.StartsWith("Twin_Stone_Bridcks", StringComparison.Ordinal))
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static void ClearSceneTwinStoneBricks(Transform decoRoot)
    {
        for (var i = decoRoot.childCount - 1; i >= 0; i--)
        {
            var child = decoRoot.GetChild(i);
            if (child.name.StartsWith("Twin_Stone_Bricks", StringComparison.Ordinal)
                || child.name.StartsWith("Twin_Stone_Bridcks", StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static Material GetPlatformStoneMaterial(GameObject prefabRoot)
    {
        var renderer = prefabRoot.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            return renderer.sharedMaterial;
        }

        foreach (var childRenderer in prefabRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (childRenderer.sharedMaterial != null
                && !childRenderer.transform.name.StartsWith("FX_", StringComparison.Ordinal)
                && !childRenderer.transform.name.Contains("Crystal", StringComparison.OrdinalIgnoreCase))
            {
                return childRenderer.sharedMaterial;
            }
        }

        return null;
    }

    private static Material CreateOrUpdateDecoTileMaterial(Material sourceMaterial)
    {
        var shader = sourceMaterial != null && sourceMaterial.shader != null
            ? sourceMaterial.shader
            : Shader.Find(LitShaderName);
        if (shader == null)
        {
            return sourceMaterial;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(DecoTileMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, DecoTileMaterialPath);
        }

        material.shader = shader;
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", null);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", null);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", new Color(0.48f, 0.52f, 0.46f, 1f));
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", new Color(0.48f, 0.52f, 0.46f, 1f));
        }

        SetMaterialFloat(material, "_Metallic", 0f);
        SetMaterialFloat(material, "_Smoothness", 0.18f);
        SetMaterialFloat(material, "_Surface", 0f);
        SetMaterialFloat(material, "_AlphaClip", 0f);
        SetMaterialFloat(material, "_ZWrite", 1f);
        SetMaterialFloat(material, "_ReceiveShadows", 1f);

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", Color.black);
        }

        material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetMaterialFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void ApplySharedMaterial(GameObject root, Material material)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static float MeasureSourceMaxWorldSize(GameObject tileAsset, Transform parent)
    {
        var temp = PrefabUtility.InstantiatePrefab(tileAsset, parent) as GameObject;
        if (temp == null)
        {
            return 0f;
        }

        temp.name = "__Temp_Measure_Twin_Stone_Bricks";
        temp.transform.localPosition = Vector3.zero;
        temp.transform.localRotation = Quaternion.identity;
        temp.transform.localScale = Vector3.one;

        var bounds = CalculateRendererBounds(temp);
        UnityEngine.Object.DestroyImmediate(temp);
        return Mathf.Max(bounds.size.x, bounds.size.z);
    }

    private static SourceGroundBounds MeasureSourceGroundBounds(GameObject tileAsset, Transform parent)
    {
        var temp = PrefabUtility.InstantiatePrefab(tileAsset, parent) as GameObject;
        if (temp == null)
        {
            return SourceGroundBounds.Invalid;
        }

        temp.name = "__Temp_Measure_Twin_Stone_Bricks";
        temp.transform.localPosition = Vector3.zero;
        temp.transform.localRotation = Quaternion.identity;
        temp.transform.localScale = Vector3.one;

        var bounds = CalculateRendererBounds(temp);
        UnityEngine.Object.DestroyImmediate(temp);

        var tangentLength = Mathf.Max(bounds.size.x, bounds.size.y);
        var radialWidth = Mathf.Min(bounds.size.x, bounds.size.y);
        if (tangentLength <= 0.000001f || radialWidth <= 0.000001f)
        {
            return SourceGroundBounds.Invalid;
        }

        return new SourceGroundBounds(tangentLength, radialWidth);
    }

    private static float MeasurePlatformRadius(GameObject prefabRoot)
    {
        var renderer = prefabRoot.GetComponent<Renderer>();
        if (renderer == null)
        {
            return 0.0108f;
        }

        var bounds = renderer.bounds;
        return Mathf.Max(bounds.extents.x, bounds.extents.y);
    }

    private static float GetScale(float sourceLength, float targetWorldSize, float bias)
    {
        return Mathf.Clamp((targetWorldSize / sourceLength) * bias, 0.0005f, 1.8f);
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(root.transform.position, Vector3.zero);
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static Transform FindOrCreateDirectChild(Transform parent, string childName)
    {
        var child = FindDirectChild(parent, childName);
        if (child != null)
        {
            return child;
        }

        var childObject = new GameObject(childName);
        childObject.transform.SetParent(parent, false);
        childObject.transform.localPosition = Vector3.zero;
        childObject.transform.localRotation = Quaternion.identity;
        childObject.transform.localScale = Vector3.one;
        return childObject.transform;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name.Equals(childName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static void ClearGeneratedTiles(Transform decoRoot)
    {
        for (var i = decoRoot.childCount - 1; i >= 0; i--)
        {
            var child = decoRoot.GetChild(i);
            if (child.name.StartsWith(TileNamePrefix, StringComparison.Ordinal)
                || child.name.Equals("__Temp_Measure_Twin_Stone_Bricks", StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private readonly struct LooseTile
    {
        public LooseTile(float degrees, float radius, float targetWorldSize, float scaleBias)
        {
            Degrees = degrees;
            Radius = radius;
            TargetWorldSize = targetWorldSize;
            ScaleBias = scaleBias;
        }

        public float Degrees { get; }
        public float Radius { get; }
        public float TargetWorldSize { get; }
        public float ScaleBias { get; }
    }

    private readonly struct SourceGroundBounds
    {
        public static readonly SourceGroundBounds Invalid = new(0f, 0f);

        public SourceGroundBounds(float tangentLength, float radialWidth)
        {
            TangentLength = tangentLength;
            RadialWidth = radialWidth;
        }

        public float TangentLength { get; }
        public float RadialWidth { get; }
        public bool IsValid => TangentLength > 0f && RadialWidth > 0f;
    }
}
#endif
