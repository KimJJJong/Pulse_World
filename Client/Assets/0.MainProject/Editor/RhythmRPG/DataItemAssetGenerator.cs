#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using RhythmRPG.Visual;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class DataItemAssetGenerator
{
    private const string MenuPath = "RhythmRPG/Editors/Content/Rebuild Data Item Assets";
    private const string PreviewMenuPath = "RhythmRPG/Editors/Content/Render Helm Prefab Preview";
    private const string IconFolder = "Assets/Resources/Icons";
    private const string WeaponFolder = "Assets/Resources/Prefabs/Weapon";
    private const string ArmorFolder = "Assets/Resources/Prefabs/Armor";
    private const string AccessoryFolder = "Assets/Resources/Prefabs/Accessory";
    private const string MaterialFolder = "Assets/Resources/Prefabs/Armor/Materials";
    private const string AccessoryMaterialFolder = "Assets/Resources/Prefabs/Accessory/Materials";
    private const string MeshFolder = "Assets/Resources/Prefabs/Armor/Meshes";

    private static readonly string[] CommonIconPaths =
    {
        IconFolder + "/Potion_Red.png",
        IconFolder + "/Potion_Blue.png",
        IconFolder + "/Ore_Iron.png",
        IconFolder + "/Leather.png",
        IconFolder + "/Blueprint_Sword.png",
        IconFolder + "/Coin_Gold.png",
        IconFolder + "/Gem_Diamond.png",
    };

    private static readonly string[] EquipmentIconPaths =
    {
        IconFolder + "/W_Sword001.png",
        IconFolder + "/W_Sword002.png",
        IconFolder + "/W_Axe001.png",
        IconFolder + "/W_Bow001.png",
        IconFolder + "/W_Dagger001.png",
        IconFolder + "/W_Staff001.png",
        IconFolder + "/A_Helm001.png",
        IconFolder + "/A_Body001.png",
        IconFolder + "/A_Boots002.png",
        IconFolder + "/A_Orb001.png",
    };

    [MenuItem(MenuPath)]
    public static void Rebuild()
    {
        EnsureFolder(IconFolder);
        EnsureFolder(WeaponFolder);
        EnsureFolder(ArmorFolder);
        EnsureFolder(AccessoryFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(AccessoryMaterialFolder);
        EnsureFolder(MeshFolder);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        if (!File.Exists(IconFolder + "/A_Orb001.png"))
            CreateOrUpdateOrbIcon(IconFolder + "/A_Orb001.png");
        ConfigureItemIcons(CommonIconPaths, "Common item");
        ConfigureItemIcons(EquipmentIconPaths, "Equipment");
        CreateOrUpdateWeaponPrefab(
            "Sword001",
            "Assets/KayKit/Characters/KayKit - Adventurers (for Unity)/Prefabs/Accessories/sword_1handed.prefab",
            WeaponFolder + "/Sword001.prefab");
        CreateOrUpdateWeaponPrefab(
            "Sword002",
            "Assets/KayKit/Characters/KayKit - Adventurers (for Unity)/Prefabs/Accessories/sword_2handed.prefab",
            WeaponFolder + "/Sword002.prefab");
        CreateOrUpdateWeaponPrefab(
            "Axe001",
            "Assets/KayKit/Characters/KayKit - Adventurers (for Unity)/Prefabs/Accessories/axe_1handed.prefab",
            WeaponFolder + "/Axe001.prefab");
        CreateOrUpdateWeaponPrefab(
            "Bow001",
            "Assets/KayKit/Characters/KayKit - Adventurers (for Unity)/Prefabs/Accessories/bow_withString.prefab",
            WeaponFolder + "/Bow001.prefab");
        CreateOrUpdateWeaponPrefab(
            "Dagger001",
            "Assets/KayKit/Characters/KayKit - Adventurers (for Unity)/Prefabs/Accessories/dagger.prefab",
            WeaponFolder + "/Dagger001.prefab");
        CreateOrUpdateWeaponPrefab(
            "Staff001",
            "Assets/KayKit/Characters/KayKit - Adventurers (for Unity)/Prefabs/Accessories/wand.prefab",
            WeaponFolder + "/Staff001.prefab");

        var leather = CreateOrUpdateMaterial(MaterialFolder + "/M_Item_Leather.mat", "#4F2E1D", 0.06f, 0.62f);
        var darkLeather = CreateOrUpdateMaterial(MaterialFolder + "/M_Item_DarkLeather.mat", "#24140E", 0.04f, 0.74f);
        var leatherEdge = CreateOrUpdateMaterial(MaterialFolder + "/M_Item_LeatherEdge.mat", "#744426", 0.06f, 0.58f);
        var brass = CreateOrUpdateMaterial(MaterialFolder + "/M_Item_Brass.mat", "#D09A38", 0.32f, 0.40f);
        var sole = CreateOrUpdateMaterial(MaterialFolder + "/M_Item_DarkSole.mat", "#17120F", 0.08f, 0.78f);
        var orbCore = CreateOrUpdateMaterial(AccessoryMaterialFolder + "/M_BeatOrb_Core.mat", "#6FEAFF", 0.0f, 0.18f);
        var orbRing = CreateOrUpdateMaterial(AccessoryMaterialFolder + "/M_BeatOrb_Ring.mat", "#F2C94C", 0.12f, 0.28f);
        var orbGlow = CreateOrUpdateGlowMaterial(AccessoryMaterialFolder + "/M_BeatOrb_GlowVolume.mat", "#7EFBFF", 0.16f, 1.25f);
        SetEmission(orbCore, new Color(0.34f, 0.96f, 1f, 1f), 1.25f);
        SetEmission(orbRing, new Color(1f, 0.78f, 0.28f, 1f), 0.65f);

        BuildHelmet(leather, darkLeather, leatherEdge, brass);
        BuildArmor(leather, darkLeather, leatherEdge, brass);
        BuildBoots(leather, darkLeather, leatherEdge, brass, sole);
        BuildBeatOrb(orbCore, orbRing, orbGlow);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        VerifyGeneratedAssets();

        Debug.Log("[DataItemAssetGenerator] Rebuilt item icons import settings and equipment prefabs from Data table paths.");
    }

    [MenuItem(PreviewMenuPath)]
    public static void RenderHelmetPreview()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArmorFolder + "/Helm001.prefab");
        if (prefab == null)
        {
            Debug.LogWarning("[DataItemAssetGenerator] Cannot render helmet preview because Helm001.prefab is missing.");
            return;
        }

        const int size = 1024;
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        string outputDirectory = Path.Combine(projectRoot, "Temp", "ItemPrefabPreviews");
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, "Helm001_Preview.png");

        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        var cameraObject = new GameObject("TempHelmPreviewCamera");
        var lightObject = new GameObject("TempHelmPreviewLight");
        var fillLightObject = new GameObject("TempHelmPreviewFillLight");
        Camera previewCamera = null;
        RenderTexture renderTexture = null;
        Texture2D texture = null;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            if (instance == null)
            {
                Debug.LogWarning("[DataItemAssetGenerator] Failed to instantiate Helm001.prefab for preview.");
                return;
            }

            instance.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            fillLightObject.hideFlags = HideFlags.HideAndDontSave;

            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.Euler(0f, -18f, 0f);

            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.transform.position = new Vector3(0f, 1.42f, -2.45f);
            previewCamera.transform.rotation = Quaternion.identity;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.16f, 0.17f, 0.18f, 1f);
            previewCamera.fieldOfView = 28f;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 20f;

            var keyLight = lightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.35f;
            keyLight.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            var fillLight = fillLightObject.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.45f;
            fillLight.transform.rotation = Quaternion.Euler(20f, 45f, 0f);

            renderTexture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            previewCamera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            previewCamera.Render();

            texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
            texture.Apply();

            File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            Debug.Log($"[DataItemAssetGenerator] Rendered helmet prefab preview: {outputPath}");
        }
        finally
        {
            if (previewCamera != null)
                previewCamera.targetTexture = null;
            RenderTexture.active = previousActive;
            if (renderTexture != null)
                renderTexture.Release();
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
            if (renderTexture != null)
                UnityEngine.Object.DestroyImmediate(renderTexture);
            if (instance != null)
                UnityEngine.Object.DestroyImmediate(instance);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(lightObject);
            UnityEngine.Object.DestroyImmediate(fillLightObject);
        }
    }

    private static void ConfigureItemIcons(string[] paths, string label)
    {
        foreach (string path in paths)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[DataItemAssetGenerator] {label} icon missing before import setup: {path}");
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    private static void CreateOrUpdateOrbIcon(string path)
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = (new Vector2(x, y) - center) / (size * 0.5f);
                float dist = uv.magnitude;
                Color pixel = Color.clear;

                float ringEllipse = Mathf.Sqrt(Mathf.Pow(uv.x / 0.74f, 2f) + Mathf.Pow((uv.y + 0.04f) / 0.31f, 2f));
                float ring = Mathf.Clamp01(1f - Mathf.Abs(ringEllipse - 1f) / 0.052f);
                float ringAlpha = ring * (uv.y < -0.02f ? 0.92f : 0.48f);
                Color ringColor = Color.Lerp(new Color(0.34f, 0.95f, 1f, 1f), new Color(1f, 0.88f, 0.42f, 1f), Mathf.Clamp01((uv.x + 0.75f) / 1.5f));
                pixel = AlphaBlend(pixel, ringColor, ringAlpha);

                float orb = Mathf.Clamp01(1f - Mathf.Max(0f, dist - 0.30f) / 0.075f);
                float core = Mathf.Clamp01(1f - dist / 0.30f);
                Color orbColor = Color.Lerp(new Color(0.50f, 0.96f, 1f, 1f), new Color(1f, 0.91f, 0.46f, 1f), Mathf.Clamp01(dist * 1.8f));
                pixel = AlphaBlend(pixel, orbColor, orb * 0.95f);
                pixel = AlphaBlend(pixel, new Color(0.86f, 1f, 1f, 1f), core * 0.58f);

                float sparkle = Mathf.Clamp01(1f - Vector2.Distance(uv, new Vector2(-0.12f, 0.16f)) / 0.08f);
                pixel = AlphaBlend(pixel, Color.white, sparkle * 0.75f);

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    private static Color AlphaBlend(Color destination, Color source, float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        float outAlpha = alpha + destination.a * (1f - alpha);
        if (outAlpha <= 0.0001f)
            return Color.clear;

        return new Color(
            (source.r * alpha + destination.r * destination.a * (1f - alpha)) / outAlpha,
            (source.g * alpha + destination.g * destination.a * (1f - alpha)) / outAlpha,
            (source.b * alpha + destination.b * destination.a * (1f - alpha)) / outAlpha,
            outAlpha);
    }

    private static void CreateOrUpdateWeaponPrefab(string targetName, string sourcePath, string targetPath)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null)
        {
            Debug.LogWarning($"[DataItemAssetGenerator] Source weapon prefab not found: {sourcePath}");
            return;
        }

        var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null)
        {
            Debug.LogWarning($"[DataItemAssetGenerator] Failed to instantiate source weapon prefab: {sourcePath}");
            return;
        }

        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = targetName;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        RemoveColliders(instance);

        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, targetPath);
        UnityEngine.Object.DestroyImmediate(instance);

        if (prefab == null)
            Debug.LogWarning($"[DataItemAssetGenerator] Failed to save weapon prefab: {targetPath}");
    }

    private static void BuildHelmet(Material leather, Material darkLeather, Material edge, Material brass)
    {
        var root = new GameObject("Helm001");

        var domeMesh = CreateOrUpdateMeshAsset(
            MeshFolder + "/Helm001_OpenDome.asset",
            CreateDomeMesh("Helm001_OpenDome", 0.40f, 0.34f, 0.37f, 32, 8));
        var rearRimMesh = CreateOrUpdateMeshAsset(
            MeshFolder + "/Helm001_RearRim.asset",
            CreateOvalArcBandMesh("Helm001_RearRim", 0.43f, 0.38f, 0.030f, 0.035f, 24, 135f, 405f));
        var browRimMesh = CreateOrUpdateMeshAsset(
            MeshFolder + "/Helm001_BrowRim.asset",
            CreateOvalArcBandMesh("Helm001_BrowRim", 0.43f, 0.40f, 0.040f, 0.040f, 14, 32f, 148f));

        AddMesh(root.transform, "OpenLeatherDome", domeMesh,
            new Vector3(0f, 1.41f, 0f), Vector3.zero, Vector3.one, leather);
        AddMesh(root.transform, "RearSideRim", rearRimMesh,
            new Vector3(0f, 1.405f, 0f), Vector3.zero, Vector3.one, edge);
        AddMesh(root.transform, "CurvedBrowRim", browRimMesh,
            new Vector3(0f, 1.43f, 0f), Vector3.zero, Vector3.one, edge);
        AddPrimitive(root.transform, "LeftCheekGuard", PrimitiveType.Sphere,
            new Vector3(-0.34f, 1.17f, 0.18f), new Vector3(0f, -18f, -5f), new Vector3(0.07f, 0.22f, 0.055f), leather);
        AddPrimitive(root.transform, "RightCheekGuard", PrimitiveType.Sphere,
            new Vector3(0.34f, 1.17f, 0.18f), new Vector3(0f, 18f, 5f), new Vector3(0.07f, 0.22f, 0.055f), leather);
        AddPrimitive(root.transform, "LeftCheekStrap", PrimitiveType.Cube,
            new Vector3(-0.36f, 1.23f, 0.08f), new Vector3(0f, -18f, -5f), new Vector3(0.045f, 0.26f, 0.035f), darkLeather);
        AddPrimitive(root.transform, "RightCheekStrap", PrimitiveType.Cube,
            new Vector3(0.36f, 1.23f, 0.08f), new Vector3(0f, 18f, 5f), new Vector3(0.045f, 0.26f, 0.035f), darkLeather);
        AddPrimitive(root.transform, "LeftSidePlate", PrimitiveType.Sphere,
            new Vector3(-0.39f, 1.34f, -0.05f), new Vector3(0f, 0f, -4f), new Vector3(0.08f, 0.13f, 0.12f), leather);
        AddPrimitive(root.transform, "RightSidePlate", PrimitiveType.Sphere,
            new Vector3(0.39f, 1.34f, -0.05f), new Vector3(0f, 0f, 4f), new Vector3(0.08f, 0.13f, 0.12f), leather);

        for (int i = -2; i <= 2; i++)
        {
            AddPrimitive(root.transform, "BrowRivet" + (i + 3), PrimitiveType.Sphere,
                new Vector3(i * 0.085f, 1.455f, 0.405f), Vector3.zero, new Vector3(0.018f, 0.018f, 0.018f), brass);
        }

        AddPrimitive(root.transform, "LeftSideRivet", PrimitiveType.Sphere,
            new Vector3(-0.405f, 1.35f, 0.025f), Vector3.zero, new Vector3(0.028f, 0.028f, 0.028f), brass);
        AddPrimitive(root.transform, "RightSideRivet", PrimitiveType.Sphere,
            new Vector3(0.405f, 1.35f, 0.025f), Vector3.zero, new Vector3(0.028f, 0.028f, 0.028f), brass);

        SavePrimitivePrefab(root, ArmorFolder + "/Helm001.prefab");
    }

    private static GameObject AddMesh(
        Transform parent,
        string name,
        Mesh mesh,
        Vector3 localPosition,
        Vector3 localEuler,
        Vector3 localScale,
        Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        go.transform.localScale = localScale;

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        return go;
    }

    private static Mesh CreateDomeMesh(string meshName, float radiusX, float height, float radiusZ, int segments, int rings)
    {
        var vertices = new List<Vector3> { new Vector3(0f, height, 0f) };
        var triangles = new List<int>();

        for (int ring = 1; ring <= rings; ring++)
        {
            float t = ring / (float)rings;
            float phi = t * Mathf.PI * 0.5f;
            float y = Mathf.Cos(phi) * height;
            float radiusScale = Mathf.Sin(phi);

            for (int segment = 0; segment < segments; segment++)
            {
                float theta = segment / (float)segments * Mathf.PI * 2f;
                vertices.Add(new Vector3(
                    Mathf.Cos(theta) * radiusX * radiusScale,
                    y,
                    Mathf.Sin(theta) * radiusZ * radiusScale));
            }
        }

        for (int segment = 0; segment < segments; segment++)
        {
            int current = 1 + segment;
            int next = 1 + ((segment + 1) % segments);
            triangles.Add(0);
            triangles.Add(next);
            triangles.Add(current);
        }

        for (int ring = 1; ring < rings; ring++)
        {
            int currentRing = 1 + (ring - 1) * segments;
            int nextRing = 1 + ring * segments;

            for (int segment = 0; segment < segments; segment++)
            {
                int a = currentRing + segment;
                int b = currentRing + ((segment + 1) % segments);
                int c = nextRing + segment;
                int d = nextRing + ((segment + 1) % segments);

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(d);
                triangles.Add(c);
            }
        }

        var mesh = new Mesh { name = meshName };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateOvalBandMesh(string meshName, float radiusX, float radiusZ, float thickness, float height, int segments)
    {
        return CreateOvalArcBandMesh(meshName, radiusX, radiusZ, thickness, height, segments, 0f, 360f);
    }

    private static Mesh CreateOvalArcBandMesh(
        string meshName,
        float radiusX,
        float radiusZ,
        float thickness,
        float height,
        int segments,
        float startDegrees,
        float endDegrees)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        float innerX = Mathf.Max(0.01f, radiusX - thickness);
        float innerZ = Mathf.Max(0.01f, radiusZ - thickness);

        for (int segment = 0; segment <= segments; segment++)
        {
            float degrees = Mathf.Lerp(startDegrees, endDegrees, segment / (float)segments);
            float theta = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(theta);
            float sin = Mathf.Sin(theta);
            vertices.Add(new Vector3(cos * radiusX, height * 0.5f, sin * radiusZ));
            vertices.Add(new Vector3(cos * radiusX, -height * 0.5f, sin * radiusZ));
            vertices.Add(new Vector3(cos * innerX, height * 0.5f, sin * innerZ));
            vertices.Add(new Vector3(cos * innerX, -height * 0.5f, sin * innerZ));
        }

        for (int segment = 0; segment < segments; segment++)
        {
            int current = segment * 4;
            int next = (segment + 1) * 4;

            AddQuad(triangles, current + 0, next + 0, current + 1, next + 1);
            AddQuad(triangles, current + 2, current + 3, next + 2, next + 3);
            AddQuad(triangles, current + 0, current + 2, next + 0, next + 2);
            AddQuad(triangles, current + 1, next + 1, current + 3, next + 3);
        }

        var mesh = new Mesh { name = meshName };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
        triangles.Add(b);
        triangles.Add(d);
        triangles.Add(c);
    }

    private static Mesh CreateOrUpdateMeshAsset(string path, Mesh source)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            AssetDatabase.CreateAsset(source, path);
            return source;
        }

        EditorUtility.CopySerialized(source, mesh);
        mesh.name = source.name;
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static void BuildArmor(Material leather, Material darkLeather, Material edge, Material brass)
    {
        var root = new GameObject("Body001");

        AddPrimitive(root.transform, "TorsoVest", PrimitiveType.Cube,
            new Vector3(0f, 1.02f, 0f), Vector3.zero, new Vector3(0.64f, 0.82f, 0.30f), leather);
        AddPrimitive(root.transform, "InnerChestShadow", PrimitiveType.Cube,
            new Vector3(0f, 1.18f, 0.17f), Vector3.zero, new Vector3(0.42f, 0.58f, 0.035f), darkLeather);
        AddPrimitive(root.transform, "LeftFrontPanel", PrimitiveType.Cube,
            new Vector3(-0.18f, 1.06f, 0.2f), new Vector3(0f, 0f, 5f), new Vector3(0.30f, 0.72f, 0.045f), leather);
        AddPrimitive(root.transform, "RightFrontPanel", PrimitiveType.Cube,
            new Vector3(0.18f, 1.06f, 0.2f), new Vector3(0f, 0f, -5f), new Vector3(0.30f, 0.72f, 0.045f), leather);
        AddPrimitive(root.transform, "Collar", PrimitiveType.Cylinder,
            new Vector3(0f, 1.55f, 0.02f), Vector3.zero, new Vector3(0.38f, 0.055f, 0.25f), edge);
        AddPrimitive(root.transform, "LeftShoulderPad", PrimitiveType.Sphere,
            new Vector3(-0.48f, 1.37f, 0f), new Vector3(0f, 0f, 10f), new Vector3(0.22f, 0.12f, 0.27f), edge);
        AddPrimitive(root.transform, "RightShoulderPad", PrimitiveType.Sphere,
            new Vector3(0.48f, 1.37f, 0f), new Vector3(0f, 0f, -10f), new Vector3(0.22f, 0.12f, 0.27f), edge);
        AddPrimitive(root.transform, "UpperBelt", PrimitiveType.Cube,
            new Vector3(0f, 1.14f, 0.25f), Vector3.zero, new Vector3(0.78f, 0.08f, 0.075f), darkLeather);
        AddPrimitive(root.transform, "LowerBelt", PrimitiveType.Cube,
            new Vector3(0f, 0.78f, 0.25f), Vector3.zero, new Vector3(0.78f, 0.08f, 0.075f), darkLeather);
        AddPrimitive(root.transform, "UpperBuckle", PrimitiveType.Cube,
            new Vector3(0f, 1.14f, 0.30f), Vector3.zero, new Vector3(0.12f, 0.10f, 0.035f), brass);
        AddPrimitive(root.transform, "LowerBuckle", PrimitiveType.Cube,
            new Vector3(0f, 0.78f, 0.30f), Vector3.zero, new Vector3(0.12f, 0.10f, 0.035f), brass);
        AddPrimitive(root.transform, "LeftHemPlate", PrimitiveType.Cube,
            new Vector3(-0.19f, 0.53f, 0.09f), new Vector3(0f, 0f, -4f), new Vector3(0.28f, 0.22f, 0.22f), leather);
        AddPrimitive(root.transform, "RightHemPlate", PrimitiveType.Cube,
            new Vector3(0.19f, 0.53f, 0.09f), new Vector3(0f, 0f, 4f), new Vector3(0.28f, 0.22f, 0.22f), leather);

        SavePrimitivePrefab(root, ArmorFolder + "/Body001.prefab");
    }

    private static void BuildBoots(Material leather, Material darkLeather, Material edge, Material brass, Material sole)
    {
        var root = new GameObject("Boots002");

        BuildBoot(root.transform, "Left", -0.22f, leather, darkLeather, edge, brass, sole);
        BuildBoot(root.transform, "Right", 0.22f, leather, darkLeather, edge, brass, sole);

        SavePrimitivePrefab(root, ArmorFolder + "/Boots002.prefab");
    }

    private static void BuildBeatOrb(Material core, Material ring, Material glow)
    {
        var root = new GameObject("BeatOrb001");
        const float orbitRadius = 0.52f;
        const float orbitHeight = 1.88f;

        var orb = AddPrimitive(root.transform, "Orb", PrimitiveType.Sphere,
            new Vector3(orbitRadius, orbitHeight, 0f), Vector3.zero, new Vector3(0.115f, 0.115f, 0.115f), core);
        var coreRenderer = orb.GetComponent<Renderer>();
        var glowVolume = AddPrimitive(orb.transform, "GlowVolume", PrimitiveType.Sphere,
            Vector3.zero, Vector3.zero, new Vector3(2.15f, 2.15f, 2.15f), glow);
        var glowRenderer = glowVolume.GetComponent<Renderer>();
        if (glowRenderer != null)
        {
            glowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;
        }

        AddPrimitive(root.transform, "OrbitRing", PrimitiveType.Cylinder,
            new Vector3(0f, orbitHeight, 0f), new Vector3(90f, 0f, 0f), new Vector3(orbitRadius, 0.010f, orbitRadius), ring);

        var light = new GameObject("OrbLight");
        light.transform.SetParent(orb.transform, false);
        light.transform.localPosition = Vector3.zero;
        var pointLight = light.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = new Color(0.55f, 0.96f, 1f, 1f);
        pointLight.intensity = 0.02f;
        pointLight.range = 0.75f;

        var visual = root.AddComponent<BeatOrbAccessoryVisual>();
        var serializedVisual = new SerializedObject(visual);
        serializedVisual.FindProperty("orb").objectReferenceValue = orb.transform;
        serializedVisual.FindProperty("glowVolume").objectReferenceValue = glowVolume.transform;
        serializedVisual.FindProperty("orbLight").objectReferenceValue = pointLight;
        serializedVisual.FindProperty("coreRenderer").objectReferenceValue = coreRenderer;
        serializedVisual.FindProperty("glowRenderer").objectReferenceValue = glowRenderer;
        serializedVisual.FindProperty("radius").floatValue = orbitRadius;
        serializedVisual.FindProperty("height").floatValue = orbitHeight;
        serializedVisual.FindProperty("orbitDegreesPerSecond").floatValue = 132f;
        serializedVisual.FindProperty("pulseSpeed").floatValue = 3.15f;
        serializedVisual.FindProperty("pulseScale").floatValue = 0.18f;
        serializedVisual.FindProperty("glowPulseScale").floatValue = 0.08f;
        serializedVisual.FindProperty("glowAlpha").floatValue = 0.18f;
        serializedVisual.ApplyModifiedPropertiesWithoutUndo();

        SavePrimitivePrefab(root, AccessoryFolder + "/BeatOrb001.prefab");
    }

    private static void BuildBoot(
        Transform root,
        string prefix,
        float x,
        Material leather,
        Material darkLeather,
        Material edge,
        Material brass,
        Material sole)
    {
        AddPrimitive(root, prefix + "Upper", PrimitiveType.Cube,
            new Vector3(x, 0.55f, 0f), new Vector3(0f, 0f, x < 0 ? -4f : 4f), new Vector3(0.20f, 0.46f, 0.22f), leather);
        AddPrimitive(root, prefix + "Cuff", PrimitiveType.Cube,
            new Vector3(x, 0.80f, 0.01f), Vector3.zero, new Vector3(0.26f, 0.11f, 0.27f), edge);
        AddPrimitive(root, prefix + "Foot", PrimitiveType.Cube,
            new Vector3(x, 0.20f, 0.16f), Vector3.zero, new Vector3(0.23f, 0.16f, 0.42f), leather);
        AddPrimitive(root, prefix + "ToeCap", PrimitiveType.Sphere,
            new Vector3(x, 0.22f, 0.39f), Vector3.zero, new Vector3(0.12f, 0.08f, 0.14f), edge);
        AddPrimitive(root, prefix + "Sole", PrimitiveType.Cube,
            new Vector3(x, 0.10f, 0.16f), Vector3.zero, new Vector3(0.25f, 0.055f, 0.48f), sole);
        AddPrimitive(root, prefix + "AnkleStrap", PrimitiveType.Cube,
            new Vector3(x, 0.50f, 0.16f), Vector3.zero, new Vector3(0.25f, 0.055f, 0.06f), darkLeather);
        AddPrimitive(root, prefix + "Buckle", PrimitiveType.Cube,
            new Vector3(x, 0.50f, 0.20f), Vector3.zero, new Vector3(0.07f, 0.075f, 0.025f), brass);
    }

    private static GameObject AddPrimitive(
        Transform parent,
        string name,
        PrimitiveType primitive,
        Vector3 localPosition,
        Vector3 localEuler,
        Vector3 localScale,
        Material material)
    {
        var go = GameObject.CreatePrimitive(primitive);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        go.transform.localScale = localScale;

        var collider = go.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;

        return go;
    }

    private static void SavePrimitivePrefab(GameObject root, string path)
    {
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);

        if (prefab == null)
            Debug.LogWarning($"[DataItemAssetGenerator] Failed to save prefab: {path}");
    }

    private static void RemoveColliders(GameObject root)
    {
        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);
    }

    private static Material CreateOrUpdateMaterial(string path, string colorHex, float metallic, float roughness)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(FindLitShader());
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = FindLitShader();
        }

        if (!ColorUtility.TryParseHtmlString(colorHex, out Color color))
            color = Color.white;

        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 1f - Mathf.Clamp01(roughness));

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOrUpdateGlowMaterial(string path, string colorHex, float alpha, float emissionIntensity)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(FindLitShader());
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = FindLitShader();
        }

        if (!ColorUtility.TryParseHtmlString(colorHex, out Color color))
            color = new Color(0.5f, 0.95f, 1f, 1f);

        color.a = Mathf.Clamp01(alpha);
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.9f);

        SetEmission(material, new Color(color.r, color.g, color.b, 1f), emissionIntensity);
        ConfigureTransparentMaterial(material);

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetEmission(Material material, Color color, float intensity)
    {
        if (material == null || !material.HasProperty("_EmissionColor"))
            return;

        material.SetColor("_EmissionColor", color * Mathf.Max(0f, intensity));
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 3f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent + 20;
    }

    private static Shader FindLitShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard")
            ?? Shader.Find("Diffuse");
    }

    private static void VerifyGeneratedAssets()
    {
        foreach (string path in CommonIconPaths)
        {
            string resourcePath = path
                .Replace("Assets/Resources/", string.Empty)
                .Replace(".png", string.Empty);
            if (Resources.Load<Sprite>(resourcePath) == null)
                Debug.LogWarning($"[DataItemAssetGenerator] Sprite cannot be loaded through Resources path: {resourcePath}");
        }

        string[] prefabPaths =
        {
            WeaponFolder + "/Sword001.prefab",
            WeaponFolder + "/Sword002.prefab",
            WeaponFolder + "/Axe001.prefab",
            WeaponFolder + "/Bow001.prefab",
            WeaponFolder + "/Dagger001.prefab",
            WeaponFolder + "/Staff001.prefab",
            ArmorFolder + "/Helm001.prefab",
            ArmorFolder + "/Body001.prefab",
            ArmorFolder + "/Boots002.prefab",
            AccessoryFolder + "/BeatOrb001.prefab",
        };

        foreach (string path in prefabPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                Debug.LogWarning($"[DataItemAssetGenerator] Prefab missing after rebuild: {path}");
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
