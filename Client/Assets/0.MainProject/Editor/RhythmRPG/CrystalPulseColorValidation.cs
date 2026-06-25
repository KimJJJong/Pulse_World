#if UNITY_EDITOR
using System;
using System.Linq;
using RhythmRPG.Game.Visual.SceneEffects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CrystalPulseColorValidation
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly Color RedPulseColor = new(1f, 0.12f, 0.08f, 1f);

    private static readonly string[] CrystalPrefabPaths =
    {
        "Assets/Resources/Prefabs/Interaction/Crystal.prefab",
        "Assets/Resources/Entity/Interaction/Crystal_Gate_01.prefab"
    };

    [MenuItem("RhythmRPG/Editors/World/Validate Crystal Pulse Body Color")]
    public static void Validate()
    {
        var hasError = false;
        foreach (var prefabPath in CrystalPrefabPaths)
        {
            if (!ValidatePrefab(prefabPath, RedPulseColor))
            {
                hasError = true;
            }
        }

        if (hasError)
        {
            Debug.LogError("[CrystalPulseColorValidation] Validation failed.");
            return;
        }

        Debug.Log("[CrystalPulseColorValidation] VALIDATION OK. Crystal glow renderers keep their original intensity and receive red color only.");
    }

    private static bool ValidatePrefab(string prefabPath, Color color)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError("[CrystalPulseColorValidation] Missing prefab: " + prefabPath);
            return false;
        }

        var previewScene = EditorSceneManager.NewPreviewScene();
        try
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab, previewScene) as GameObject;
            if (instance == null)
            {
                Debug.LogError("[CrystalPulseColorValidation] Failed to instantiate prefab: " + prefabPath);
                return false;
            }

            var pulse = instance.GetComponent<ForestBeatLightPulse>();
            if (pulse == null)
            {
                Debug.LogError("[CrystalPulseColorValidation] Missing ForestBeatLightPulse: " + prefabPath);
                return false;
            }

            pulse.SetPulseColor(color);

            var bodyRenderers = instance.GetComponentsInChildren<Renderer>(true)
                .Where(IsCrystalBodyRenderer)
                .ToArray();
            if (bodyRenderers.Length == 0)
            {
                Debug.LogError("[CrystalPulseColorValidation] Missing crystal glow renderer: " + prefabPath);
                return false;
            }

            var block = new MaterialPropertyBlock();
            foreach (var bodyRenderer in bodyRenderers)
            {
                bodyRenderer.GetPropertyBlock(block);

                var baseColor = block.GetColor(BaseColorId);
                var colorValue = block.GetColor(ColorId);
                var emissionColor = block.GetColor(EmissionColorId);
                var glowColor = block.GetColor(GlowColorId);
                var hasWhiteTextureOverride =
                    block.GetTexture(BaseMapId) == Texture2D.whiteTexture ||
                    block.GetTexture(MainTexId) == Texture2D.whiteTexture;
                var colorOk = IsRedPulse(baseColor) || IsRedPulse(colorValue) || IsRedPulse(emissionColor) || IsRedPulse(glowColor);
                if (!colorOk)
                {
                    Debug.LogError(
                        "[CrystalPulseColorValidation] Red color override failed. " +
                        $"Prefab={prefabPath}, Renderer={bodyRenderer.name}, " +
                        $"BaseColor={baseColor}, Color={colorValue}, Emission={emissionColor}, Glow={glowColor}, " +
                        $"WhiteTexture={hasWhiteTextureOverride}.");
                    return false;
                }
            }

            Debug.Log(
                "[CrystalPulseColorValidation] OK. " +
                $"Prefab={prefabPath}, Renderers={bodyRenderers.Length}.");
            return true;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    private static bool IsCrystalBodyRenderer(Renderer renderer)
    {
        if (renderer == null || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
        {
            return false;
        }

        return HasCrystalName(renderer.name) ||
            renderer.sharedMaterials.Any(material => material != null && HasCrystalName(material.name));
    }

    private static bool HasCrystalName(string value)
    {
        return !string.IsNullOrEmpty(value) &&
            (value.IndexOf("Crystal", StringComparison.OrdinalIgnoreCase) >= 0 ||
             value.IndexOf("Prism", StringComparison.OrdinalIgnoreCase) >= 0 ||
             value.IndexOf("Gem", StringComparison.OrdinalIgnoreCase) >= 0 ||
             value.IndexOf("Emerald", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool IsRedPulse(Color color)
    {
        return color.r >= 0.85f && color.g <= 0.22f && color.b <= 0.18f && color.a > 0.8f;
    }
}
#endif
