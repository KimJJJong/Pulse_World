#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class LowPolyWeedSetup
{
    private const string AssetFolder = "Assets/0.MainProject/Art/LowPolyWeed";
    private const string ShaderName = "RhythmRPG/Nature/LowPolyWeed";
    private const string MaterialPath = AssetFolder + "/M_LowPolyWeed.mat";
    private const string ShortMeshPath = AssetFolder + "/SM_LowPolyWeed_Short.asset";
    private const string MediumMeshPath = AssetFolder + "/SM_LowPolyWeed_Medium.asset";
    private const string TallMeshPath = AssetFolder + "/SM_LowPolyWeed_Tall.asset";
    private const string ShortPrefabPath = AssetFolder + "/PF_LowPolyWeed_Short.prefab";
    private const string MediumPrefabPath = AssetFolder + "/PF_LowPolyWeed_Medium.prefab";
    private const string TallPrefabPath = AssetFolder + "/PF_LowPolyWeed_Tall.prefab";
    private const string PatchPrefabPath = AssetFolder + "/PF_LowPolyWeed_Patch.prefab";

    [MenuItem("RhythmRPG/Editors/World/Build Low Poly Weed Assets")]
    public static void Build()
    {
        EnsureAssetFolder();
        AssetDatabase.ImportAsset(AssetFolder + "/S_LowPolyWeed.shader", ImportAssetOptions.ForceSynchronousImport);

        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            throw new InvalidOperationException($"Shader was not found: {ShaderName}");
        }

        var material = CreateOrUpdateMaterial(shader);
        var shortMesh = LoadOrCreateMesh(ShortMeshPath, CreateWeedMesh("SM_LowPolyWeed_Short", 9, 0.52f, 0.08f, 10));
        var mediumMesh = LoadOrCreateMesh(MediumMeshPath, CreateWeedMesh("SM_LowPolyWeed_Medium", 13, 0.82f, 0.12f, 20));
        var tallMesh = LoadOrCreateMesh(TallMeshPath, CreateWeedMesh("SM_LowPolyWeed_Tall", 17, 1.12f, 0.16f, 30));

        var shortPrefab = CreateOrUpdateSinglePrefab(ShortPrefabPath, "PF_LowPolyWeed_Short", shortMesh, material);
        var mediumPrefab = CreateOrUpdateSinglePrefab(MediumPrefabPath, "PF_LowPolyWeed_Medium", mediumMesh, material);
        var tallPrefab = CreateOrUpdateSinglePrefab(TallPrefabPath, "PF_LowPolyWeed_Tall", tallMesh, material);
        var patchPrefab = CreateOrUpdatePatchPrefab(shortMesh, mediumMesh, tallMesh, material);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = patchPrefab;
        EditorGUIUtility.PingObject(patchPrefab);

        Debug.Log(
            "[LowPolyWeedSetup] Build complete. " +
            $"Material={material.name}, Meshes=3, Prefabs=4, " +
            $"ShortVerts={shortMesh.vertexCount}, MediumVerts={mediumMesh.vertexCount}, TallVerts={tallMesh.vertexCount}, " +
            $"Selected={patchPrefab.name}.");

        _ = shortPrefab;
        _ = mediumPrefab;
        _ = tallPrefab;
    }

    [MenuItem("RhythmRPG/Editors/World/Validate Low Poly Weed Assets")]
    public static void Validate()
    {
        var shader = Shader.Find(ShaderName);
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var shortMesh = AssetDatabase.LoadAssetAtPath<Mesh>(ShortMeshPath);
        var mediumMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MediumMeshPath);
        var tallMesh = AssetDatabase.LoadAssetAtPath<Mesh>(TallMeshPath);
        var patchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PatchPrefabPath);

        if (shader == null)
        {
            Debug.LogError($"[LowPolyWeedSetup] Validation failed: shader was not found: {ShaderName}");
            return;
        }

        if (material == null || material.shader != shader || !material.enableInstancing)
        {
            Debug.LogError("[LowPolyWeedSetup] Validation failed: material is missing, uses the wrong shader, or instancing is disabled.");
            return;
        }

        if (!IsValidWeedMesh(shortMesh) || !IsValidWeedMesh(mediumMesh) || !IsValidWeedMesh(tallMesh))
        {
            Debug.LogError("[LowPolyWeedSetup] Validation failed: one or more weed mesh assets are missing or empty.");
            return;
        }

        if (patchPrefab == null || patchPrefab.GetComponentsInChildren<MeshRenderer>(true).Length < 5)
        {
            Debug.LogError("[LowPolyWeedSetup] Validation failed: patch prefab was not generated correctly.");
            return;
        }

        Debug.Log(
            "[LowPolyWeedSetup] VALIDATION OK. " +
            $"Shader={shader.name}, Material={material.name}, " +
            $"ShortTris={shortMesh.triangles.Length / 3}, MediumTris={mediumMesh.triangles.Length / 3}, " +
            $"TallTris={tallMesh.triangles.Length / 3}, PatchRenderers={patchPrefab.GetComponentsInChildren<MeshRenderer>(true).Length}.");
    }

    private static void EnsureAssetFolder()
    {
        var parts = AssetFolder.Split('/');
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

    private static Material CreateOrUpdateMaterial(Shader shader)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.name = "M_LowPolyWeed";
        material.shader = shader;
        material.enableInstancing = true;
        material.renderQueue = -1;
        SetColor(material, "_BottomColor", new Color(0.13f, 0.27f, 0.09f, 1f));
        SetColor(material, "_MiddleColor", new Color(0.34f, 0.52f, 0.14f, 1f));
        SetColor(material, "_TopColor", new Color(0.74f, 0.82f, 0.27f, 1f));
        SetFloat(material, "_GradientSteps", 3f);
        SetFloat(material, "_LightSteps", 3f);
        SetFloat(material, "_AmbientStrength", 0.38f);
        SetFloat(material, "_HueVariation", 0.16f);
        SetFloat(material, "_ValueVariation", 0.10f);
        SetFloat(material, "_HeightVariation", 0.12f);
        SetFloat(material, "_WindStrength", 0.34f);
        SetFloat(material, "_WindSpeed", 1.15f);
        SetFloat(material, "_WindFrequency", 2.25f);
        SetFloat(material, "_WindNoise", 0.28f);
        SetFloat(material, "_BendHeight", 1.0f);

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetColor(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static void SetFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static Mesh LoadOrCreateMesh(string path, Mesh mesh)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }

        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static GameObject CreateOrUpdateSinglePrefab(string path, string name, Mesh mesh, Material material)
    {
        var root = new GameObject(name);
        AddMeshRenderer(root, mesh, material);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateOrUpdatePatchPrefab(Mesh shortMesh, Mesh mediumMesh, Mesh tallMesh, Material material)
    {
        var root = new GameObject("PF_LowPolyWeed_Patch");
        CreateMeshChild("Weed_Short_A", root.transform, shortMesh, material, new Vector3(-0.35f, 0f, -0.20f), 17f, 0.92f);
        CreateMeshChild("Weed_Short_B", root.transform, shortMesh, material, new Vector3(0.25f, 0f, -0.25f), -41f, 0.78f);
        CreateMeshChild("Weed_Medium_A", root.transform, mediumMesh, material, new Vector3(-0.05f, 0f, 0.02f), 0f, 1.0f);
        CreateMeshChild("Weed_Medium_B", root.transform, mediumMesh, material, new Vector3(0.38f, 0f, 0.12f), 53f, 0.86f);
        CreateMeshChild("Weed_Tall_A", root.transform, tallMesh, material, new Vector3(-0.25f, 0f, 0.22f), -18f, 0.96f);
        CreateMeshChild("Weed_Tall_B", root.transform, tallMesh, material, new Vector3(0.10f, 0f, 0.28f), 28f, 0.84f);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PatchPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void CreateMeshChild(
        string name,
        Transform parent,
        Mesh mesh,
        Material material,
        Vector3 localPosition,
        float yaw,
        float scale)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        child.transform.localScale = Vector3.one * scale;
        AddMeshRenderer(child, mesh, material);
    }

    private static void AddMeshRenderer(GameObject gameObject, Mesh mesh, Material material)
    {
        var meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;
    }

    private static bool IsValidWeedMesh(Mesh mesh)
    {
        return mesh != null
            && mesh.vertexCount > 0
            && mesh.triangles != null
            && mesh.triangles.Length >= 12
            && mesh.uv != null
            && mesh.uv.Length == mesh.vertexCount;
    }

    private static Mesh CreateWeedMesh(string name, int bladeCount, float height, float baseRadius, int seed)
    {
        var random = new System.Random(seed);
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        for (var i = 0; i < bladeCount; i++)
        {
            var yaw = (i / (float)bladeCount) * Mathf.PI * 2f + RandomRange(random, -0.23f, 0.23f);
            var direction = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
            var side = new Vector3(-direction.z, 0f, direction.x);
            var bladeHeight = height * RandomRange(random, 0.72f, 1.22f);
            var rootWidth = height * RandomRange(random, 0.040f, 0.070f);
            var midWidth = rootWidth * RandomRange(random, 0.48f, 0.70f);
            var lean = RandomRange(random, 0.12f, 0.32f);
            var twist = RandomRange(random, -0.10f, 0.10f);
            var baseOffset = direction * RandomRange(random, 0.0f, baseRadius);

            var root = baseOffset;
            var mid = baseOffset + direction * (bladeHeight * lean * 0.48f) + side * twist + Vector3.up * (bladeHeight * 0.48f);
            var shoulder = baseOffset + direction * (bladeHeight * lean * 0.82f) - side * (twist * 0.4f) + Vector3.up * (bladeHeight * 0.78f);
            var tip = baseOffset + direction * (bladeHeight * lean) + Vector3.up * bladeHeight;

            var rootLeft = root - side * rootWidth;
            var rootRight = root + side * rootWidth;
            var midLeft = mid - side * midWidth;
            var midRight = mid + side * midWidth;
            var shoulderLeft = shoulder - side * (midWidth * 0.46f);
            var shoulderRight = shoulder + side * (midWidth * 0.46f);

            AddQuad(vertices, uvs, triangles, rootLeft, rootRight, midRight, midLeft, 0f, 0.48f);
            AddQuad(vertices, uvs, triangles, midLeft, midRight, shoulderRight, shoulderLeft, 0.48f, 0.78f);
            AddTriangle(vertices, uvs, triangles, shoulderLeft, shoulderRight, tip, 0.78f, 0.78f, 1f);
        }

        AddGroundTufts(vertices, uvs, triangles, bladeCount, baseRadius, height, random);

        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        var bounds = mesh.bounds;
        bounds.Expand(new Vector3(height * 0.75f, height * 0.24f, height * 0.75f));
        mesh.bounds = bounds;
        Unwrapping.GenerateSecondaryUVSet(mesh);
        return mesh;
    }

    private static void AddGroundTufts(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        int count,
        float radius,
        float height,
        System.Random random)
    {
        var tufts = Mathf.Max(4, count / 2);
        for (var i = 0; i < tufts; i++)
        {
            var yaw = (i / (float)tufts) * Mathf.PI * 2f + RandomRange(random, -0.28f, 0.28f);
            var direction = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
            var side = new Vector3(-direction.z, 0f, direction.x);
            var length = height * RandomRange(random, 0.13f, 0.22f);
            var width = height * RandomRange(random, 0.025f, 0.045f);
            var root = direction * RandomRange(random, 0.0f, radius * 0.65f) + Vector3.up * 0.012f;
            var tip = root + direction * length + Vector3.up * RandomRange(random, 0.01f, 0.035f);
            AddTriangle(vertices, uvs, triangles, root - side * width, root + side * width, tip, 0f, 0f, 0.16f);
        }
    }

    private static void AddQuad(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        float bottomUv,
        float topUv)
    {
        AddTriangle(vertices, uvs, triangles, a, b, c, bottomUv, bottomUv, topUv);
        AddTriangle(vertices, uvs, triangles, a, c, d, bottomUv, topUv, topUv);
    }

    private static void AddTriangle(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        float auv,
        float buv,
        float cuv)
    {
        var start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        uvs.Add(new Vector2(0f, auv));
        uvs.Add(new Vector2(1f, buv));
        uvs.Add(new Vector2(0.5f, cuv));
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}
#endif
