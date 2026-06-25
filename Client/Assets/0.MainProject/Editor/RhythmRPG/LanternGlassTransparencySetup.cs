#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class LanternGlassTransparencySetup
{
    private const string GlassPath = "Forest_Visual_Samples/Latern/SM_Lantern_Glass (1)";
    private const string FlamePath = "Forest_Visual_Samples/Latern/SM_Lantern_Flame (1)";
    private const string AssetFolder = "Assets/0.MainProject/Art/ForestLightingPipeline";
    private const string SourceMaterialPath = AssetFolder + "/M_Lantern_Glass.mat";
    private const string TransparentMaterialPath = AssetFolder + "/M_Lantern_Glass_Transparent_Sample.mat";

    private static readonly Color GlassTint = new(1f, 0.68f, 0.32f, 0.32f);
    private static readonly Color WarmGlow = new(1f, 0.5f, 0.18f, 1f);

    [MenuItem("RhythmRPG/Editors/World/Apply Lantern Glass Transparency")]
    public static void Apply()
    {
        var glass = FindSceneObjectByPath(GlassPath);
        var flame = FindSceneObjectByPath(FlamePath);
        if (glass == null || flame == null)
        {
            Debug.LogError($"[LanternGlassTransparencySetup] Target missing. Glass={glass != null}, Flame={flame != null}");
            return;
        }

        var glassSnapshot = TransformSnapshot.Capture(glass.transform, GlassPath);
        var flameSnapshot = TransformSnapshot.Capture(flame.transform, FlamePath);

        var renderer = glass.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            Debug.LogError($"[LanternGlassTransparencySetup] MeshRenderer not found: {GlassPath}");
            return;
        }

        var material = CreateOrUpdateTransparentGlassMaterial();
        if (material == null)
        {
            Debug.LogError("[LanternGlassTransparencySetup] Transparent material could not be created.");
            return;
        }

        var materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            materials = new[] { material };
        }
        else
        {
            materials[0] = material;
        }

        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        EditorUtility.SetDirty(renderer);

        var transformsOk = glassSnapshot.Matches() && flameSnapshot.Matches();
        if (!transformsOk)
        {
            Debug.LogError("[LanternGlassTransparencySetup] Aborted: a Lantern glass or flame Transform changed during material setup.");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[LanternGlassTransparencySetup] Applied transparent lantern glass. " +
            $"Material={material.name}, Alpha={GlassTint.a}, ZWrite=Off, TransformUnchanged=True, FlameVisible=True.");
    }

    [MenuItem("RhythmRPG/Editors/World/Validate Lantern Glass Transparency")]
    public static void Validate()
    {
        var glass = FindSceneObjectByPath(GlassPath);
        var flame = FindSceneObjectByPath(FlamePath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(TransparentMaterialPath);
        var renderer = glass != null ? glass.GetComponent<MeshRenderer>() : null;

        var materialAssigned = renderer != null
            && renderer.sharedMaterial == material;

        var materialTransparent = material != null
            && material.HasProperty("_Surface")
            && Mathf.Approximately(material.GetFloat("_Surface"), 1f)
            && material.HasProperty("_ZWrite")
            && Mathf.Approximately(material.GetFloat("_ZWrite"), 0f)
            && material.HasProperty("_BaseColor")
            && material.GetColor("_BaseColor").a <= 0.34f;

        var flameVisible = flame != null && flame.activeInHierarchy;
        var transformsOk = glass != null
            && flame != null
            && TransformMatchesRecordedGlass(glass.transform)
            && TransformMatchesRecordedFlame(flame.transform);

        if (!materialAssigned || !materialTransparent || !flameVisible || !transformsOk)
        {
            Debug.LogError(
                "[LanternGlassTransparencySetup] Validation failed. " +
                $"MaterialAssigned={materialAssigned}, Transparent={materialTransparent}, FlameVisible={flameVisible}, TransformsUnchanged={transformsOk}.");
            return;
        }

        Debug.Log(
            "[LanternGlassTransparencySetup] VALIDATION OK. " +
            $"GlassAlpha={material.GetColor("_BaseColor").a}, FlameVisible=True, TargetTransformsUnchanged=True.");
    }

    private static Material CreateOrUpdateTransparentGlassMaterial()
    {
        EnsureAssetFolder();

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[LanternGlassTransparencySetup] Shader not found: Universal Render Pipeline/Lit");
            return null;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(TransparentMaterialPath);
        if (material == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
            material = source != null ? new Material(source) : new Material(shader);
            AssetDatabase.CreateAsset(material, TransparentMaterialPath);
        }

        material.name = "M_Lantern_Glass_Transparent_Sample";
        material.shader = shader;
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetOverrideTag("RenderType", "Transparent");

        SetFloat(material, "_Surface", 1f);
        SetFloat(material, "_Blend", 0f);
        SetFloat(material, "_AlphaClip", 0f);
        SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One);
        SetFloat(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        SetFloat(material, "_ZWrite", 0f);
        SetFloat(material, "_Cull", (float)CullMode.Off);
        SetFloat(material, "_ReceiveShadows", 0f);
        SetFloat(material, "_Smoothness", 0.82f);
        SetFloat(material, "_Metallic", 0f);

        SetColor(material, "_BaseColor", GlassTint);
        SetColor(material, "_Color", GlassTint);
        SetColor(material, "_EmissionColor", Hdr(WarmGlow, 1.15f));

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_EMISSION");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        material.SetShaderPassEnabled("DepthOnly", false);
        material.SetShaderPassEnabled("SHADOWCASTER", false);
        material.SetShaderPassEnabled("MOTIONVECTORS", false);

        EditorUtility.SetDirty(material);
        return material;
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

    private static GameObject FindSceneObjectByPath(string path)
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.scene.IsValid() && GetPath(go) == path)
            {
                return go;
            }
        }

        return null;
    }

    private static string GetPath(GameObject go)
    {
        var path = go.name;
        var current = go.transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static void SetFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetColor(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static Color Hdr(Color color, float exposureValue)
    {
        var hdr = color * Mathf.Pow(2f, exposureValue);
        hdr.a = 1f;
        return hdr;
    }

    private static bool TransformMatchesRecordedGlass(Transform transform)
    {
        return Approximately(transform.position, new Vector3(10.2413588f, 0.776579559f, 12.8605785f))
            && Approximately(transform.localPosition, new Vector3(7.233f, 1.148f, 10.5180006f))
            && Approximately(transform.eulerAngles, Vector3.zero)
            && Approximately(transform.localEulerAngles, Vector3.zero)
            && Approximately(transform.localScale, new Vector3(0.222269416f, 0.340913743f, 0.222269416f));
    }

    private static bool TransformMatchesRecordedFlame(Transform transform)
    {
        return Approximately(transform.position, new Vector3(10.2413588f, 0.7142562f, 12.8605785f))
            && Approximately(transform.localPosition, new Vector3(7.233f, 1.1068f, 10.5180006f))
            && Approximately(transform.eulerAngles, Vector3.zero)
            && Approximately(transform.localEulerAngles, Vector3.zero)
            && Approximately(transform.localScale, new Vector3(0.1070186f, 0.18474704f, 0.1070186f));
    }

    private static bool Approximately(Vector3 lhs, Vector3 rhs)
    {
        return Vector3.SqrMagnitude(lhs - rhs) < 0.00001f;
    }

    private readonly struct TransformSnapshot
    {
        private readonly Transform _transform;
        private readonly Transform _parent;
        private readonly Vector3 _position;
        private readonly Vector3 _localPosition;
        private readonly Quaternion _rotation;
        private readonly Quaternion _localRotation;
        private readonly Vector3 _localScale;

        private TransformSnapshot(Transform transform, string path)
        {
            _transform = transform;
            _parent = transform.parent;
            _position = transform.position;
            _localPosition = transform.localPosition;
            _rotation = transform.rotation;
            _localRotation = transform.localRotation;
            _localScale = transform.localScale;
            Path = path;
        }

        public string Path { get; }

        public static TransformSnapshot Capture(Transform transform, string path)
        {
            return new TransformSnapshot(transform, path);
        }

        public bool Matches()
        {
            return _transform != null
                && _transform.parent == _parent
                && Approximately(_transform.position, _position)
                && Approximately(_transform.localPosition, _localPosition)
                && Quaternion.Angle(_transform.rotation, _rotation) < 0.001f
                && Quaternion.Angle(_transform.localRotation, _localRotation) < 0.001f
                && Approximately(_transform.localScale, _localScale);
        }
    }
}
#endif
