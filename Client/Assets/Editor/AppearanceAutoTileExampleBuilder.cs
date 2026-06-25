#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AppearanceAutoTileExampleBuilder
{
    private const string ExampleFolder = "Assets/Resources/Data/Map/AppearancePalettes/Example";
    private const string TexturePath = ExampleFolder + "/ExampleGrassDirtSheet.png";
    private const string PalettePath = ExampleFolder + "/ExampleGrassDirtAppearanceAutoTilePalette.asset";
    private const string MapPath = "Assets/Resources/Data/Map/Example_AppearanceAutoTile.asset";
    private const string MaterialRoot = ExampleFolder + "/GeneratedMaterials/ExampleGrassDirtAppearanceAutoTilePalette/GrassBorder";

    [MenuItem("RhythmRPG/Editors/World/Create Example Appearance Auto Tile Palette")]
    public static void Build()
    {
        EnsureFolder(ExampleFolder);
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null)
        {
            Debug.LogError($"[AppearanceAutoTileExample] Texture not found: {TexturePath}");
            return;
        }

        ConfigureTextureImporter(TexturePath);
        texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

        EnsureFolder(MaterialRoot);

        var palette = AssetDatabase.LoadAssetAtPath<AppearanceAutoTilePalette>(PalettePath);
        if (palette == null)
        {
            palette = ScriptableObject.CreateInstance<AppearanceAutoTilePalette>();
            AssetDatabase.CreateAsset(palette, PalettePath);
        }

        var definition = BuildGrassDirtDefinition(texture);
        var definitions = AppearanceAutoTilePalette.CreateDefaultDefinitions();
        for (int i = 0; i < definitions.Length; i++)
        {
            if (definitions[i].Kind == AppearanceTileKind.GrassBorder)
            {
                definitions[i] = definition;
                break;
            }
        }

        palette.Tiles = definitions;
        EditorUtility.SetDirty(palette);

        var map = AssetDatabase.LoadAssetAtPath<MapAsset>(MapPath);
        if (map == null)
        {
            map = ScriptableObject.CreateInstance<MapAsset>();
            AssetDatabase.CreateAsset(map, MapPath);
        }

        BuildExampleMap(map, palette);
        EditorUtility.SetDirty(map);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(palette);
        Debug.Log($"[AppearanceAutoTileExample] Created example palette: {PalettePath}");
        Debug.Log($"[AppearanceAutoTileExample] Created example map: {MapPath}");
    }

    private static AppearanceAutoTileDefinition BuildGrassDirtDefinition(Texture2D texture)
    {
        int columns = 8;
        int rows = 8;
        float cellW = texture.width / (float)columns;
        float cellH = texture.height / (float)rows;
        int[] masks = CreateBlob47Masks();
        var variants = new AppearanceAutoTileVariant[masks.Length];
        Material interiorMaterial = null;

        for (int i = 0; i < masks.Length; i++)
        {
            int col = i % columns;
            int rowFromTop = i / columns;
            Rect rect = new Rect(
                col * cellW,
                texture.height - ((rowFromTop + 1) * cellH),
                cellW,
                cellH);

            var material = CreateOrUpdateMaterial(texture, rect, masks[i], i);
            variants[i] = new AppearanceAutoTileVariant
            {
                Mask = masks[i],
                Material = material
            };

            if (masks[i] == byte.MaxValue)
                interiorMaterial = material;
        }

        return new AppearanceAutoTileDefinition
        {
            Kind = AppearanceTileKind.GrassBorder,
            DisplayName = "Example Grass Dirt",
            PreviewColor = new Color(0.42f, 0.62f, 0.16f, 1f),
            SetupSourceTexture = texture,
            SetupColumns = 8,
            SetupRows = 8,
            DefaultMaterial = interiorMaterial != null ? interiorMaterial : variants[0].Material,
            Variants = variants
        };
    }

    private static Material CreateOrUpdateMaterial(Texture2D texture, Rect rect, int mask, int cellIndex)
    {
        string materialPath = $"{MaterialRoot}/GrassDirt_mask_{mask:000}_cell_{cellIndex:000}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = FindTextureShader();
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }

        material.name = Path.GetFileNameWithoutExtension(materialPath);
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);

        Vector2 scale = new Vector2(rect.width / texture.width, rect.height / texture.height);
        Vector2 offset = new Vector2(rect.x / texture.width, rect.y / texture.height);
        material.mainTextureScale = scale;
        material.mainTextureOffset = offset;
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureOffset("_BaseMap", offset);
        }
        if (material.HasProperty("_MainTex"))
        {
            material.SetTextureScale("_MainTex", scale);
            material.SetTextureOffset("_MainTex", offset);
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void BuildExampleMap(MapAsset map, AppearanceAutoTilePalette palette)
    {
        map.Width = 16;
        map.Height = 12;
        map.AppearancePalette = palette;
        map.Cells = new TileCell[map.Width * map.Height];
        map.AppearanceCells = new AppearanceTileCell[map.Width * map.Height];

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                map.Set(x, y, new TileCell { Kind = TileKind.Floor });

                bool paint =
                    x >= 2 && x <= 13 &&
                    y >= 2 && y <= 9 &&
                    !(x >= 7 && x <= 9 && y >= 5 && y <= 7);

                paint |= x >= 5 && x <= 11 && y == 10;
                paint |= x == 1 && y >= 5 && y <= 7;

                if (paint)
                    map.SetAppearance(x, y, new AppearanceTileCell { Kind = AppearanceTileKind.GrassBorder });
            }
        }

        map.RebuildAppearanceAutoTiles();
    }

    private static void ConfigureTextureImporter(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            return;

        bool changed = false;
        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = false;
            changed = true;
        }

        if (importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.npotScale = TextureImporterNPOTScale.None;
            changed = true;
        }

        if (importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            changed = true;
        }

        if (importer.filterMode != FilterMode.Bilinear)
        {
            importer.filterMode = FilterMode.Bilinear;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
    }

    private static Shader FindTextureShader()
    {
        return Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Texture")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Standard");
    }

    private static void EnsureFolder(string path)
    {
        path = path.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folderName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent))
            return;

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folderName);
    }

    private static int[] CreateBlob47Masks()
    {
        var masks = new List<int>();
        for (int mask = 0; mask <= 255; mask++)
        {
            if (IsValidBlobMask(mask))
                masks.Add(mask);
        }

        return masks.ToArray();
    }

    private static bool IsValidBlobMask(int mask)
    {
        bool north = (mask & MapAsset.AppearanceNorth) != 0;
        bool east = (mask & MapAsset.AppearanceEast) != 0;
        bool south = (mask & MapAsset.AppearanceSouth) != 0;
        bool west = (mask & MapAsset.AppearanceWest) != 0;

        if ((mask & MapAsset.AppearanceNorthEast) != 0 && (!north || !east))
            return false;
        if ((mask & MapAsset.AppearanceSouthEast) != 0 && (!south || !east))
            return false;
        if ((mask & MapAsset.AppearanceSouthWest) != 0 && (!south || !west))
            return false;
        if ((mask & MapAsset.AppearanceNorthWest) != 0 && (!north || !west))
            return false;

        return true;
    }
}
#endif
