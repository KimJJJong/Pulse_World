using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TownFontApplicator
{
    private const string TownMapScenePath = "Assets/0.MainProject/Scenes/Town/TownMap.unity";
    private const string TownForestScenePath = "Assets/0.MainProject/Scenes/Town/Town_Forest.unity";
    private const string TownPrefabRoot = "Assets/0.MainProject/Prefabs/UI/Town";
    private const string GowunBatangFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Gowun Batang.asset";
    private const string GowunBatangFontFallbackPath = "Assets/TextMesh Pro/Resources/Gowun Batang.asset";
    private const string NanumGothicFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/NanumGothic SDF.asset";
    private const string NanumGothicFontFallbackPath = "Assets/TextMesh Pro/Resources/NanumGothic SDF.asset";

    [MenuItem("RhythmRPG/Editors/UI/Apply Town Fonts")]
    public static void ApplyTownFonts()
    {
        var font = LoadFont();
        if (font == null)
        {
            Debug.LogError("[TownFontApplicator] Korean UI font asset was not found.");
            return;
        }

        var originalScenePath = EditorSceneManager.GetActiveScene().path;
        var sceneCount = 0;
        var prefabCount = 0;

        sceneCount += ApplyToScene(TownMapScenePath, font);
        sceneCount += ApplyToScene(TownForestScenePath, font);
        prefabCount += ApplyToTownPrefabs(font);

        if (!string.IsNullOrWhiteSpace(originalScenePath))
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);

        AssetDatabase.SaveAssets();
        Debug.Log($"[TownFontApplicator] Applied {font.name} to {sceneCount} Town scene texts and {prefabCount} Town prefab texts.");
    }

    private static int ApplyToScene(string scenePath, TMP_FontAsset font)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var count = 0;
        foreach (var root in scene.GetRootGameObjects())
            count += ApplyToRoot(root, font);

        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        return count;
    }

    private static int ApplyToTownPrefabs(TMP_FontAsset font)
    {
        var count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { TownPrefabRoot }))
        {
            var prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var applied = ApplyToRoot(root, font);
                if (applied > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    count += applied;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return count;
    }

    private static int ApplyToRoot(GameObject root, TMP_FontAsset font)
    {
        var count = 0;
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null)
                continue;

            if (text.font == font && text.fontSharedMaterial == font.material)
                continue;

            text.font = font;
            text.fontSharedMaterial = font.material;
            EditorUtility.SetDirty(text);
            count++;
        }

        return count;
    }

    private static TMP_FontAsset LoadFont()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GowunBatangFontPath);
        if (font == null)
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GowunBatangFontFallbackPath);
        if (font == null)
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NanumGothicFontPath);
        if (font == null)
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NanumGothicFontFallbackPath);
        return font;
    }
}
