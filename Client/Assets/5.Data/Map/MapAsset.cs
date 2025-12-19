using System;
using UnityEngine;

[CreateAssetMenu(menuName = "RhythmRPG/Map/MapAsset")]
public sealed class MapAsset : ScriptableObject
{
    public int Width = 16;
    public int Height = 8;
    public TileCell[] Cells = new TileCell[0];

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
        if (Cells == null || Cells.Length != need)
        {
            var old = Cells ?? new TileCell[0];
            var newCells = new TileCell[need]; // 기본값(None,0)

            int copy = Mathf.Min(old.Length, newCells.Length);
            for (int i = 0; i < copy; i++)
                newCells[i] = old[i];

            Cells = newCells;
        }
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
}


public sealed class MapJson
{
    public int width;
    public int height;
    public Cell[] cells = Array.Empty<Cell>();

    [Serializable]
    public struct Cell
    {
        public byte k; // kind
        public byte v; // variant
    }
}