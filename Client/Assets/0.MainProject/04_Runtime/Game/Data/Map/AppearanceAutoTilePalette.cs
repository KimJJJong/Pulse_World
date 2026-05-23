using System;
using UnityEngine;

[CreateAssetMenu(menuName = "RhythmRPG/Map/Appearance Auto Tile Palette")]
public sealed class AppearanceAutoTilePalette : ScriptableObject
{
    public AppearanceAutoTileDefinition[] Tiles = Array.Empty<AppearanceAutoTileDefinition>();

    public bool TryGetDefinition(AppearanceTileKind kind, out AppearanceAutoTileDefinition definition)
    {
        if (Tiles != null)
        {
            for (int i = 0; i < Tiles.Length; i++)
            {
                var tile = Tiles[i];
                if (tile != null && tile.Kind == kind)
                {
                    definition = tile;
                    return true;
                }
            }
        }

        definition = null;
        return false;
    }

    public bool TryGetMaterial(AppearanceTileKind kind, byte mask, out Material material)
    {
        if (TryGetDefinition(kind, out var definition))
        {
            material = definition.GetMaterial(mask);
            return material != null;
        }

        material = null;
        return false;
    }

    public bool TryGetPrefab(AppearanceTileKind kind, byte mask, out GameObject prefab)
    {
        if (TryGetDefinition(kind, out var definition))
        {
            prefab = definition.GetPrefab(mask);
            return prefab != null;
        }

        prefab = null;
        return false;
    }

    public Color GetPreviewColor(AppearanceTileKind kind)
    {
        if (TryGetDefinition(kind, out var definition))
            return definition.PreviewColor;

        return GetBuiltInPreviewColor(kind);
    }

    public static Color GetBuiltInPreviewColor(AppearanceTileKind kind)
    {
        switch (kind)
        {
            case AppearanceTileKind.GrassBorder: return new Color(0.25f, 0.68f, 0.30f, 1f);
            case AppearanceTileKind.StonePath: return new Color(0.58f, 0.58f, 0.55f, 1f);
            case AppearanceTileKind.BrickBorder: return new Color(0.64f, 0.28f, 0.20f, 1f);
            case AppearanceTileKind.WaterEdge: return new Color(0.18f, 0.55f, 0.86f, 1f);
            case AppearanceTileKind.WoodDeck: return new Color(0.58f, 0.38f, 0.18f, 1f);
            case AppearanceTileKind.Carpet: return new Color(0.68f, 0.18f, 0.36f, 1f);
            case AppearanceTileKind.FlowerBed: return new Color(0.86f, 0.52f, 0.18f, 1f);
            case AppearanceTileKind.CustomA: return new Color(0.46f, 0.33f, 0.78f, 1f);
            case AppearanceTileKind.CustomB: return new Color(0.15f, 0.70f, 0.62f, 1f);
            default: return Color.clear;
        }
    }

    public static AppearanceAutoTileDefinition[] CreateDefaultDefinitions()
    {
        return new[]
        {
            CreateDefaultDefinition(AppearanceTileKind.GrassBorder, "Grass Border"),
            CreateDefaultDefinition(AppearanceTileKind.StonePath, "Stone Path"),
            CreateDefaultDefinition(AppearanceTileKind.BrickBorder, "Brick Border"),
            CreateDefaultDefinition(AppearanceTileKind.WaterEdge, "Water Edge"),
            CreateDefaultDefinition(AppearanceTileKind.WoodDeck, "Wood Deck"),
            CreateDefaultDefinition(AppearanceTileKind.Carpet, "Carpet"),
            CreateDefaultDefinition(AppearanceTileKind.FlowerBed, "Flower Bed"),
            CreateDefaultDefinition(AppearanceTileKind.CustomA, "Custom A"),
            CreateDefaultDefinition(AppearanceTileKind.CustomB, "Custom B"),
        };
    }

    private static AppearanceAutoTileDefinition CreateDefaultDefinition(AppearanceTileKind kind, string label)
    {
        return new AppearanceAutoTileDefinition
        {
            Kind = kind,
            DisplayName = label,
            PreviewColor = GetBuiltInPreviewColor(kind),
            Variants = Array.Empty<AppearanceAutoTileVariant>()
        };
    }
}

[Serializable]
public sealed class AppearanceAutoTileDefinition
{
    public AppearanceTileKind Kind = AppearanceTileKind.StonePath;
    public string DisplayName = "Appearance Tile";
    public Color PreviewColor = Color.white;
    public Texture2D SetupSourceTexture;
    [Min(1)] public int SetupColumns = 8;
    [Min(1)] public int SetupRows = 8;
    [Min(0)] public int SetupTileWidth;
    [Min(0)] public int SetupTileHeight;
    [Min(0)] public int SetupMarginX;
    [Min(0)] public int SetupMarginY;
    [Min(0)] public int SetupSpacingX;
    [Min(0)] public int SetupSpacingY;
    public AppearanceAutoTileSourceCellCrop[] SetupSourceCellCrops = Array.Empty<AppearanceAutoTileSourceCellCrop>();
    public Material DefaultMaterial;
    public GameObject DefaultPrefab;
    public AppearanceAutoTileVariant[] Variants = Array.Empty<AppearanceAutoTileVariant>();

    public Material GetMaterial(byte mask)
    {
        if (Variants != null)
        {
            for (int i = 0; i < Variants.Length; i++)
            {
                var variant = Variants[i];
                if (variant != null && variant.Mask == mask && variant.Material != null)
                    return variant.Material;
            }
        }

        return DefaultMaterial;
    }

    public GameObject GetPrefab(byte mask)
    {
        if (Variants != null)
        {
            for (int i = 0; i < Variants.Length; i++)
            {
                var variant = Variants[i];
                if (variant != null && variant.Mask == mask && variant.Prefab != null)
                    return variant.Prefab;
            }
        }

        return DefaultPrefab;
    }
}

[Serializable]
public sealed class AppearanceAutoTileSourceCellCrop
{
    [Min(0)] public int SourceCell;
    [Min(0)] public int TrimLeft;
    [Min(0)] public int TrimRight;
    [Min(0)] public int TrimTop;
    [Min(0)] public int TrimBottom;
}

[Serializable]
public sealed class AppearanceAutoTileVariant
{
    [Range(0, 255)]
    public int Mask;
    public Material Material;
    public GameObject Prefab;
}
