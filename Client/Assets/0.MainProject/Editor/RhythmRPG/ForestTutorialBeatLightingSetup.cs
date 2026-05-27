#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using RhythmRPG.Game.Visual.SceneEffects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ForestTutorialBeatLightingSetup
{
    private const string ScenePath = "Assets/0.MainProject/Scenes/Game/Game_Forest_Tutorial.unity";

    private static readonly Color LanternColor = new(1f, 150f / 255f, 50f / 255f, 1f);
    private static readonly Color CrystalColor = new(50f / 255f, 200f / 255f, 1f, 1f);

    private static readonly BeatTargetConfig[] TargetConfigs =
    {
        new(
            "Forest_Decoration_Set/Samples_ForestVisual/Lantern_Warm_01",
            LanternColor,
            new[] { "SM_Lantern_Flame", "SM_Lantern_Glass" },
            new[] { "FX_Lantern_Warm_Embers" },
            lightPeak: 1.78f,
            emissionPeak: 1.08f,
            alphaPeak: 1.03f,
            particlePeak: 1.22f,
            durationBeats: 0.5f,
            falloff: 2.05f,
            flicker: 0.03f,
            lightBase: 0.72f,
            rendererBase: 0.48f,
            particleBase: 0.85f),
        new(
            "Forest_Decoration_Set/Samples_ForestVisual/Lantern_Warm_02",
            LanternColor,
            new[] { "SM_Lantern_Flame", "SM_Lantern_Glass" },
            new[] { "FX_Lantern_Warm_Embers" },
            lightPeak: 1.78f,
            emissionPeak: 1.08f,
            alphaPeak: 1.03f,
            particlePeak: 1.22f,
            durationBeats: 0.5f,
            falloff: 2.05f,
            flicker: 0.03f,
            lightBase: 0.72f,
            rendererBase: 0.48f,
            particleBase: 0.85f),
        new(
            "Forest_Decoration_Set/Samples_ForestVisual/Lantern_Warm_03",
            LanternColor,
            new[] { "SM_Lantern_Flame", "SM_Lantern_Glass" },
            new[] { "FX_Lantern_Warm_Embers" },
            lightPeak: 1.78f,
            emissionPeak: 1.08f,
            alphaPeak: 1.03f,
            particlePeak: 1.22f,
            durationBeats: 0.5f,
            falloff: 2.05f,
            flicker: 0.03f,
            lightBase: 0.72f,
            rendererBase: 0.48f,
            particleBase: 0.85f),
        new(
            "Forest_Decoration_Set/Samples_ForestVisual/Crystal_Gate_01",
            CrystalColor,
            new[] { "FX_Azure_Crystal_Prism_EmissionShell" },
            new[] { "FX_Azure_Crystal_Prism_Glow" },
            lightPeak: 1.16f,
            emissionPeak: 1.32f,
            alphaPeak: 2.75f,
            particlePeak: 1.12f,
            durationBeats: 0.56f,
            falloff: 1.75f,
            flicker: 0.012f),
        new(
            "Forest_Decoration_Set/Samples_ForestVisual/Crystal_Gate_02",
            CrystalColor,
            new[] { "FX_Azure_Crystal_Prism_EmissionShell" },
            new[] { "FX_Azure_Crystal_Prism_Glow" },
            lightPeak: 1.16f,
            emissionPeak: 1.32f,
            alphaPeak: 2.75f,
            particlePeak: 1.12f,
            durationBeats: 0.56f,
            falloff: 1.75f,
            flicker: 0.012f)
    };

    [MenuItem("RhythmRPG/Editors/World/Apply Forest Tutorial Beat Lighting")]
    public static void Apply()
    {
        OpenTargetScene();

        var snapshots = CaptureTargetTransforms();
        var configured = 0;

        foreach (var config in TargetConfigs)
        {
            var root = FindSceneObjectByPath(config.RootPath);
            if (root == null)
            {
                Debug.LogError($"[ForestTutorialBeatLightingSetup] Missing target root: {config.RootPath}");
                continue;
            }

            var pulse = root.GetComponent<ForestBeatLightPulse>();
            if (pulse == null)
            {
                pulse = root.AddComponent<ForestBeatLightPulse>();
            }

            pulse.Configure(
                GetActiveLights(root),
                GetNamedComponents<Renderer>(root, config.RendererNames),
                GetNamedComponents<ParticleSystem>(root, config.ParticleNames),
                config.Color,
                config.LightPeak,
                config.EmissionPeak,
                config.AlphaPeak,
                config.ParticlePeak,
                config.DurationBeats,
                config.Falloff,
                config.Flicker,
                lightBase: config.LightBase,
                rendererBase: config.RendererBase,
                particleBase: config.ParticleBase);

            EditorUtility.SetDirty(pulse);
            EditorUtility.SetDirty(root);
            configured++;
        }

        if (!ValidateSnapshots(snapshots))
        {
            Debug.LogError("[ForestTutorialBeatLightingSetup] Aborted: a Lantern/Crystal Transform changed during beat lighting setup.");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[ForestTutorialBeatLightingSetup] Applied beat lighting. Targets={configured}, Scene={scene.name}.");
    }

    [MenuItem("RhythmRPG/Editors/World/Validate Forest Tutorial Beat Lighting")]
    public static void Validate()
    {
        OpenTargetScene();

        var missing = 0;
        var invalid = 0;
        foreach (var config in TargetConfigs)
        {
            var root = FindSceneObjectByPath(config.RootPath);
            if (root == null)
            {
                missing++;
                Debug.LogError($"[ForestTutorialBeatLightingSetup] Missing target root: {config.RootPath}");
                continue;
            }

            var pulse = root.GetComponent<ForestBeatLightPulse>();
            if (pulse == null)
            {
                invalid++;
                Debug.LogError($"[ForestTutorialBeatLightingSetup] Missing ForestBeatLightPulse: {config.RootPath}");
                continue;
            }

            var rendererCountOk = pulse.RendererTargetCount == config.RendererNames.Length;
            var lightCountOk = pulse.LightTargetCount > 0;
            var particleCountOk = pulse.ParticleTargetCount == config.ParticleNames.Length;

            if (!rendererCountOk || !lightCountOk || !particleCountOk)
            {
                invalid++;
                Debug.LogError(
                    "[ForestTutorialBeatLightingSetup] Invalid target bindings. " +
                    $"Path={config.RootPath}, Lights={pulse.LightTargetCount}, Renderers={pulse.RendererTargetCount}, Particles={pulse.ParticleTargetCount}.");
            }
        }

        if (missing > 0 || invalid > 0)
        {
            Debug.LogError($"[ForestTutorialBeatLightingSetup] Validation failed. Missing={missing}, Invalid={invalid}.");
            return;
        }

        Debug.Log($"[ForestTutorialBeatLightingSetup] VALIDATION OK. Targets={TargetConfigs.Length}, BeatSynced=True.");
    }

    private static void OpenTargetScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }

    private static Light[] GetActiveLights(GameObject root)
    {
        return root.GetComponentsInChildren<Light>(true)
            .Where(light => light != null && light.enabled && light.gameObject.activeSelf)
            .ToArray();
    }

    private static T[] GetNamedComponents<T>(GameObject root, IReadOnlyList<string> names) where T : Component
    {
        var components = new List<T>(names.Count);
        foreach (var name in names)
        {
            var child = FindDescendant(root.transform, name);
            if (child == null)
            {
                Debug.LogError($"[ForestTutorialBeatLightingSetup] Missing child '{name}' under {GetPath(root)}");
                continue;
            }

            var component = child.GetComponent<T>();
            if (component == null)
            {
                Debug.LogError($"[ForestTutorialBeatLightingSetup] Missing {typeof(T).Name} on {GetPath(child.gameObject)}");
                continue;
            }

            components.Add(component);
        }

        return components.ToArray();
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (child.name == name)
            {
                return child;
            }

            var found = FindDescendant(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static List<TransformSnapshot> CaptureTargetTransforms()
    {
        var snapshots = new List<TransformSnapshot>();
        foreach (var config in TargetConfigs)
        {
            var root = FindSceneObjectByPath(config.RootPath);
            if (root != null)
            {
                snapshots.Add(TransformSnapshot.Capture(root.transform, config.RootPath));
            }
        }

        return snapshots;
    }

    private static bool ValidateSnapshots(List<TransformSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (!snapshot.Matches())
            {
                Debug.LogError($"[ForestTutorialBeatLightingSetup] Transform changed: {snapshot.Path}");
                return false;
            }
        }

        return true;
    }

    private static GameObject FindSceneObjectByPath(string path)
    {
        foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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

    private readonly struct BeatTargetConfig
    {
        public BeatTargetConfig(
            string rootPath,
            Color color,
            string[] rendererNames,
            string[] particleNames,
            float lightPeak,
            float emissionPeak,
            float alphaPeak,
            float particlePeak,
            float durationBeats,
            float falloff,
            float flicker,
            float lightBase = 1f,
            float rendererBase = 1f,
            float particleBase = 1f)
        {
            RootPath = rootPath;
            Color = color;
            RendererNames = rendererNames;
            ParticleNames = particleNames;
            LightPeak = lightPeak;
            EmissionPeak = emissionPeak;
            AlphaPeak = alphaPeak;
            ParticlePeak = particlePeak;
            DurationBeats = durationBeats;
            Falloff = falloff;
            Flicker = flicker;
            LightBase = lightBase;
            RendererBase = rendererBase;
            ParticleBase = particleBase;
        }

        public string RootPath { get; }
        public Color Color { get; }
        public string[] RendererNames { get; }
        public string[] ParticleNames { get; }
        public float LightPeak { get; }
        public float EmissionPeak { get; }
        public float AlphaPeak { get; }
        public float ParticlePeak { get; }
        public float DurationBeats { get; }
        public float Falloff { get; }
        public float Flicker { get; }
        public float LightBase { get; }
        public float RendererBase { get; }
        public float ParticleBase { get; }
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
                && Approximately(_transform.rotation, _rotation)
                && Approximately(_transform.localRotation, _localRotation)
                && Approximately(_transform.localScale, _localScale);
        }

        private static bool Approximately(Vector3 lhs, Vector3 rhs)
        {
            return Vector3.SqrMagnitude(lhs - rhs) < 0.000001f;
        }

        private static bool Approximately(Quaternion lhs, Quaternion rhs)
        {
            return Quaternion.Angle(lhs, rhs) < 0.001f;
        }
    }
}
#endif
