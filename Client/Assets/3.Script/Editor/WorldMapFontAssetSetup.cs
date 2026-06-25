#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class WorldMapFontAssetSetup
{
    private const string SourceFolder = "Assets/Fonts/WorldMap";
    private const string TargetFolder = "Assets/TextMesh Pro/Resources/Fonts & Materials";

    [MenuItem("RhythmRPG/Editors/Setup/Build World Map TMP Fonts")]
    public static void Build()
    {
        EnsureFolder(TargetFolder);

        CreateFontAsset(
            "CinzelDecorative-Bold.ttf",
            WorldMapFontLibrary.TitleFontName,
            AtlasPopulationMode.Dynamic);

        CreateFontAsset(
            "CormorantGaramond-SemiBold.ttf",
            WorldMapFontLibrary.HeaderFontName,
            AtlasPopulationMode.Dynamic);

        CreateFontAsset(
            "Cinzel-Bold.ttf",
            WorldMapFontLibrary.ButtonFontName,
            AtlasPopulationMode.Dynamic);

        CreateFontAsset(
            "GowunBatang-Regular.ttf",
            WorldMapFontLibrary.KoreanBodyFontName,
            AtlasPopulationMode.Dynamic);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[WorldMapFontAssetSetup] World map TMP font assets are ready.");
    }

    private static void CreateFontAsset(string sourceFileName, string assetName, AtlasPopulationMode populationMode)
    {
        string sourcePath = $"{SourceFolder}/{sourceFileName}";
        EnsureSourceFontImportSettings(sourcePath);

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (sourceFont == null)
        {
            Debug.LogWarning($"[WorldMapFontAssetSetup] Missing source font: {sourcePath}");
            return;
        }

        string targetPath = $"{TargetFolder}/{assetName}.asset";
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetPath);
        if (fontAsset != null && !IsValidFontAsset(fontAsset))
        {
            AssetDatabase.DeleteAsset(targetPath);
            AssetDatabase.Refresh();
            fontAsset = null;
        }

        if (fontAsset == null)
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                populationMode,
                true);

            if (fontAsset == null)
            {
                Debug.LogWarning($"[WorldMapFontAssetSetup] Failed to create TMP font asset: {sourcePath}");
                return;
            }

            fontAsset.name = assetName;
            var material = fontAsset.material;
            var atlasTextures = fontAsset.atlasTextures != null
                ? (Texture2D[])fontAsset.atlasTextures.Clone()
                : null;

            AssetDatabase.CreateAsset(fontAsset, targetPath);
            AddSubAsset(material, targetPath, $"{assetName} Material");
            AddAtlasSubAssets(atlasTextures, targetPath, assetName);

            fontAsset.material = material;
            fontAsset.atlasTextures = atlasTextures;
            fontAsset.atlas = atlasTextures != null && atlasTextures.Length > 0 ? atlasTextures[0] : null;

            var primaryAtlas = atlasTextures != null && atlasTextures.Length > 0 ? atlasTextures[0] : null;
            Debug.Log($"[WorldMapFontAssetSetup] Created {assetName}: material={(material != null ? material.name : "null")}, atlas={(primaryAtlas != null ? primaryAtlas.name : "null")}.");
        }

        fontAsset.atlasPopulationMode = populationMode;
        fontAsset.isMultiAtlasTexturesEnabled = true;
        fontAsset.boldStyle = 0.65f;

        EditorUtility.SetDirty(fontAsset);
    }

    private static void EnsureSourceFontImportSettings(string sourcePath)
    {
        if (AssetImporter.GetAtPath(sourcePath) is not TrueTypeFontImporter importer)
            return;

        if (importer.includeFontData)
            return;

        importer.includeFontData = true;
        importer.SaveAndReimport();
    }

    private static bool IsValidFontAsset(TMP_FontAsset fontAsset)
    {
        return fontAsset != null
               && fontAsset.material != null
               && fontAsset.atlasTextures != null
               && fontAsset.atlasTextures.Length > 0
               && fontAsset.atlasTextures[0] != null;
    }

    private static void AddAtlasSubAssets(Texture2D[] atlasTextures, string parentPath, string assetName)
    {
        if (atlasTextures == null)
            return;

        for (int i = 0; i < atlasTextures.Length; i++)
        {
            var atlas = atlasTextures[i];
            AddSubAsset(atlas, parentPath, i == 0 ? $"{assetName} Atlas" : $"{assetName} Atlas {i}");
        }
    }

    private static void AddSubAsset(Object asset, string parentPath, string name)
    {
        if (asset == null || string.IsNullOrEmpty(parentPath))
            return;

        asset.hideFlags = HideFlags.None;
        asset.name = name;
        if (!AssetDatabase.Contains(asset))
            AssetDatabase.AddObjectToAsset(asset, parentPath);

        EditorUtility.SetDirty(asset);
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
#endif
