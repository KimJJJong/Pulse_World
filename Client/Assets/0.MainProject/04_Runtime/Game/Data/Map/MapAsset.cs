using System;
using UnityEngine;

[CreateAssetMenu(menuName = "RhythmRPG/Map/MapAsset")]
public sealed class MapAsset : ScriptableObject
{
    public const byte AppearanceNorth = 1 << 0;
    public const byte AppearanceEast = 1 << 1;
    public const byte AppearanceSouth = 1 << 2;
    public const byte AppearanceWest = 1 << 3;
    public const byte AppearanceNorthEast = 1 << 4;
    public const byte AppearanceSouthEast = 1 << 5;
    public const byte AppearanceSouthWest = 1 << 6;
    public const byte AppearanceNorthWest = 1 << 7;

    public int Width = 16;
    public int Height = 8;
    public AppearanceAutoTilePalette AppearancePalette;
    public TileCell[] Cells = new TileCell[0];
    public AppearanceTileCell[] AppearanceCells = new AppearanceTileCell[0];

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    public int Idx(int x, int y) => y * Width + x;

    private void OnValidate()
    {
        EnsureSize();
    }

    public void EnsureSize()
    {
        if (Width < 1) Width = 1;
        if (Height < 1) Height = 1;

        int need = Width * Height;
        Cells = EnsureArraySize(Cells, need);
        AppearanceCells = EnsureArraySize(AppearanceCells, need);
    }

    public TileCell Get(int x, int y)
    {
        EnsureSize(); // <- 안전장치 (에디터에서 값 꼬여도 방지)
        if (!InBounds(x, y)) return default;
        return Cells[Idx(x, y)];
    }

    public void Set(int x, int y, TileCell v)
    {
        EnsureSize();
        if (!InBounds(x, y)) return;
        Cells[Idx(x, y)] = v;
    }

    public AppearanceTileCell GetAppearance(int x, int y)
    {
        EnsureSize();
        if (!InBounds(x, y)) return default;
        return AppearanceCells[Idx(x, y)];
    }

    public void SetAppearance(int x, int y, AppearanceTileCell v)
    {
        EnsureSize();
        if (!InBounds(x, y)) return;
        AppearanceCells[Idx(x, y)] = v;
    }

    public void RebuildAppearanceAutoTiles()
    {
        EnsureSize();

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                RefreshAppearanceAutoTileAt(x, y);
            }
        }
    }

    public void RefreshAppearanceAutoTilesAround(int x, int y)
    {
        EnsureSize();

        for (int yy = y - 1; yy <= y + 1; yy++)
        {
            for (int xx = x - 1; xx <= x + 1; xx++)
            {
                RefreshAppearanceAutoTileAt(xx, yy);
            }
        }
    }

    public void RefreshAppearanceAutoTileAt(int x, int y)
    {
        EnsureSize();
        if (!InBounds(x, y)) return;

        int idx = Idx(x, y);
        var cell = AppearanceCells[idx];
        cell.Variant = CalculateAppearanceAutoMask(x, y, cell.Kind);
        AppearanceCells[idx] = cell;
    }

    private byte CalculateAppearanceAutoMask(int x, int y, AppearanceTileKind kind)
    {
        if (kind == AppearanceTileKind.None)
            return 0;

        bool north = HasSameAppearance(x, y - 1, kind);
        bool east = HasSameAppearance(x + 1, y, kind);
        bool south = HasSameAppearance(x, y + 1, kind);
        bool west = HasSameAppearance(x - 1, y, kind);

        byte mask = 0;
        if (north) mask |= AppearanceNorth;
        if (east) mask |= AppearanceEast;
        if (south) mask |= AppearanceSouth;
        if (west) mask |= AppearanceWest;

        if (north && east && HasSameAppearance(x + 1, y - 1, kind)) mask |= AppearanceNorthEast;
        if (south && east && HasSameAppearance(x + 1, y + 1, kind)) mask |= AppearanceSouthEast;
        if (south && west && HasSameAppearance(x - 1, y + 1, kind)) mask |= AppearanceSouthWest;
        if (north && west && HasSameAppearance(x - 1, y - 1, kind)) mask |= AppearanceNorthWest;

        return mask;
    }

    private bool HasSameAppearance(int x, int y, AppearanceTileKind kind)
    {
        if (!InBounds(x, y)) return false;
        return AppearanceCells[Idx(x, y)].Kind == kind;
    }

    private static T[] EnsureArraySize<T>(T[] cells, int need)
    {
        if (cells != null && cells.Length == need)
            return cells;

        var old = cells ?? new T[0];
        var newCells = new T[need];
        int copy = Mathf.Min(old.Length, newCells.Length);
        for (int i = 0; i < copy; i++)
            newCells[i] = old[i];

        return newCells;
    }
}


public sealed class MapJson
{
    public string appearancePalette;
    public int width;
    public int height;
    public Cell[] cells = Array.Empty<Cell>();

    [Serializable]
    public struct Cell
    {
        public byte k; // kind
        public byte v; // variant
        public byte a; // appearance kind
        public byte av; // appearance auto-tile variant
    }
}
