using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class MinimapMapGraphic : MaskableGraphic
{
    public enum MarkerKind
    {
        LocalPlayer,
        Ally,
        Enemy,
        Object
    }

    public struct EntityMarker
    {
        public int EntityId;
        public MarkerKind Kind;
        public int X;
        public int Y;
        public float Rotation;
    }

    [Header("Map")]
    [SerializeField, Min(0f)] private float _padding = 4f;
    [SerializeField] private Color _backgroundColor = new Color(0.005f, 0.018f, 0.024f, 0.96f);
    [SerializeField] private Color _walkableCellColor = new Color(0.34f, 0.48f, 0.55f, 0.42f);
    [SerializeField] private Color _spawnCellColor = new Color(0.32f, 0.82f, 0.9f, 0.52f);
    [SerializeField] private Color _gridColor = new Color(0.58f, 0.76f, 0.82f, 0.22f);

    [Header("Focus")]
    [SerializeField, Min(1f)] private float _focusZoomScale = 1.55f;
    [SerializeField, Min(4f)] private float _focusedCellMinSize = 12f;
    [SerializeField, Min(1f)] private float _largeMapFollowThreshold = 14f;
    [SerializeField] private bool _followPlayerOnLargeMaps = true;

    [Header("Markers")]
    [SerializeField] private Color _localPlayerColor = new Color(0.12f, 0.95f, 1f, 1f);
    [SerializeField] private Color _allyColor = new Color(0.18f, 0.48f, 1f, 1f);
    [SerializeField] private Color _enemyColor = new Color(1f, 0.18f, 0.16f, 1f);
    [SerializeField] private Color _objectColor = new Color(1f, 0.78f, 0.24f, 1f);
    [SerializeField] private Color _markerOutlineColor = new Color(0.01f, 0.02f, 0.025f, 0.92f);
    [SerializeField, Min(0.1f)] private float _markerScale = 0.82f;
    [SerializeField, Min(1f)] private float _markerMinSize = 6f;
    [SerializeField, Min(1f)] private float _markerMaxSize = 18f;

    private readonly List<EntityMarker> _markers = new List<EntityMarker>(32);
    private int _width;
    private int _height;
    private int _focusX;
    private int _focusY;
    private byte[] _visibleTiles = new byte[0];
    private bool _hasFocus;
    private bool _meshDirty;
    private float _nextMeshRefreshTime;

    private const byte HiddenTile = 0;
    private const byte WalkableTile = 1;
    private const byte SpawnTile = 2;
    private const float MinMeshRefreshInterval = 0.04f;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    private void LateUpdate()
    {
        if (!_meshDirty || Time.unscaledTime < _nextMeshRefreshTime)
            return;

        _meshDirty = false;
        _nextMeshRefreshTime = Time.unscaledTime + MinMeshRefreshInterval;
        SetVerticesDirty();
    }

    public void SetMapSize(int width, int height)
    {
        _width = Mathf.Max(0, width);
        _height = Mathf.Max(0, height);
        int tileCount = _width * _height;

        if (_visibleTiles == null || _visibleTiles.Length != tileCount)
            _visibleTiles = new byte[tileCount];
        else if (tileCount > 0)
            System.Array.Clear(_visibleTiles, 0, _visibleTiles.Length);

        MarkMeshDirty(true);
    }

    public void SetTile(int x, int y, int tileKind)
    {
        if (x < 0 || y < 0 || x >= _width || y >= _height || _visibleTiles == null)
            return;

        int index = y * _width + x;
        byte value = ResolveVisibleTile(tileKind);
        if (_visibleTiles[index] == value)
            return;

        _visibleTiles[index] = value;
        MarkMeshDirty(false);
    }

    public void SetFocus(int x, int y, bool hasFocus)
    {
        if (_focusX == x && _focusY == y && _hasFocus == hasFocus)
            return;

        _focusX = x;
        _focusY = y;
        _hasFocus = hasFocus;
        MarkMeshDirty(false);
    }

    public void SetZoomScale(float zoomScale)
    {
        float clampedScale = Mathf.Max(1f, zoomScale);
        if (Mathf.Approximately(_focusZoomScale, clampedScale))
            return;

        _focusZoomScale = clampedScale;
        MarkMeshDirty(true);
    }

    public void SetMarkers(IReadOnlyList<EntityMarker> markers)
    {
        _markers.Clear();
        if (markers != null)
        {
            for (int i = 0; i < markers.Count; i++)
                _markers.Add(markers[i]);
        }

        MarkMeshDirty(true);
    }

    public void ClearMarkers()
    {
        if (_markers.Count == 0)
            return;

        _markers.Clear();
        MarkMeshDirty(true);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        AddQuad(vh, rect.xMin, rect.yMin, rect.xMax, rect.yMax, _backgroundColor);

        if (_width <= 0 || _height <= 0 || _visibleTiles == null)
            return;

        float visibleWidth = Mathf.Max(0f, rect.width - _padding * 2f);
        float visibleHeight = Mathf.Max(0f, rect.height - _padding * 2f);
        if (visibleWidth <= 0f || visibleHeight <= 0f)
            return;

        Rect contentRect = new Rect(rect.xMin + _padding, rect.yMin + _padding, visibleWidth, visibleHeight);
        float fitCellSize = Mathf.Min(visibleWidth / _width, visibleHeight / _height);
        float cellSize = Mathf.Max(fitCellSize * Mathf.Max(1f, _focusZoomScale), _focusedCellMinSize);
        float mapWidth = cellSize * _width;
        float mapHeight = cellSize * _height;
        bool mapExceedsViewport = mapWidth > visibleWidth + 0.01f || mapHeight > visibleHeight + 0.01f;
        bool largeMap = Mathf.Max(_width, _height) >= _largeMapFollowThreshold || mapExceedsViewport;
        bool followFocus = _hasFocus && _followPlayerOnLargeMaps && largeMap && mapExceedsViewport;
        float originX = followFocus
            ? contentRect.center.x - (_focusX + 0.5f) * cellSize
            : contentRect.xMin + (visibleWidth - mapWidth) * 0.5f;
        float originY = followFocus
            ? contentRect.center.y - (_focusY + 0.5f) * cellSize
            : contentRect.yMin + (visibleHeight - mapHeight) * 0.5f;

        originX = ClampMapOrigin(originX, contentRect.xMin, contentRect.xMax, mapWidth);
        originY = ClampMapOrigin(originY, contentRect.yMin, contentRect.yMax, mapHeight);

        DrawCells(vh, contentRect, originX, originY, cellSize);
        DrawGrid(vh, contentRect, originX, originY, cellSize);
        DrawMarkers(vh, contentRect, originX, originY, cellSize, MarkerKind.Object);
        DrawMarkers(vh, contentRect, originX, originY, cellSize, MarkerKind.Enemy);
        DrawMarkers(vh, contentRect, originX, originY, cellSize, MarkerKind.Ally);
        DrawMarkers(vh, contentRect, originX, originY, cellSize, MarkerKind.LocalPlayer);
    }

    private void DrawCells(VertexHelper vh, Rect clipRect, float originX, float originY, float cellSize)
    {
        if (!TryGetVisibleTileBounds(clipRect, originX, originY, cellSize, out int minTileX, out int maxTileX, out int minTileY, out int maxTileY))
            return;

        float inset = Mathf.Min(1.2f, cellSize * 0.08f);
        for (int y = minTileY; y < maxTileY; y++)
        {
            for (int x = minTileX; x < maxTileX; x++)
            {
                int index = y * _width + x;
                byte tile = _visibleTiles[index];
                if (tile == HiddenTile)
                    continue;

                float minX = originX + x * cellSize + inset;
                float minY = originY + y * cellSize + inset;
                float maxX = originX + (x + 1) * cellSize - inset;
                float maxY = originY + (y + 1) * cellSize - inset;
                AddQuad(vh, minX, minY, maxX, maxY, tile == SpawnTile ? _spawnCellColor : _walkableCellColor);
            }
        }
    }

    private void DrawGrid(VertexHelper vh, Rect clipRect, float originX, float originY, float cellSize)
    {
        if (!TryGetVisibleTileBounds(clipRect, originX, originY, cellSize, out int minTileX, out int maxTileX, out int minTileY, out int maxTileY))
            return;

        Color gridColor = _gridColor;
        if (cellSize < 4f)
            gridColor.a *= Mathf.InverseLerp(1.4f, 4f, cellSize);

        if (gridColor.a <= 0.01f)
            return;

        float lineWidth = Mathf.Clamp(cellSize * 0.065f, 0.35f, 1.2f);
        float half = lineWidth * 0.5f;
        for (int y = minTileY; y < maxTileY; y++)
        {
            for (int x = minTileX; x < maxTileX; x++)
            {
                int index = y * _width + x;
                if (_visibleTiles[index] == HiddenTile)
                    continue;

                float minX = originX + x * cellSize;
                float minY = originY + y * cellSize;
                float maxX = minX + cellSize;
                float maxY = minY + cellSize;
                AddQuad(vh, minX - half, minY - half, maxX + half, minY + half, gridColor);
                AddQuad(vh, minX - half, maxY - half, maxX + half, maxY + half, gridColor);
                AddQuad(vh, minX - half, minY - half, minX + half, maxY + half, gridColor);
                AddQuad(vh, maxX - half, minY - half, maxX + half, maxY + half, gridColor);
            }
        }
    }

    private void DrawMarkers(VertexHelper vh, Rect clipRect, float originX, float originY, float cellSize, MarkerKind pass)
    {
        float baseSize = Mathf.Clamp(cellSize * _markerScale, _markerMinSize, _markerMaxSize);
        float cullRadius = Mathf.Max(_markerMaxSize + 4f, baseSize * 1.5f);

        for (int i = 0; i < _markers.Count; i++)
        {
            EntityMarker marker = _markers[i];
            if (marker.Kind != pass || marker.X < 0 || marker.Y < 0 || marker.X >= _width || marker.Y >= _height)
                continue;

            Vector2 center = new Vector2(
                originX + (marker.X + 0.5f) * cellSize,
                originY + (marker.Y + 0.5f) * cellSize);

            if (center.x + cullRadius < clipRect.xMin || center.x - cullRadius > clipRect.xMax
                || center.y + cullRadius < clipRect.yMin || center.y - cullRadius > clipRect.yMax)
                continue;

            float size = marker.Kind == MarkerKind.LocalPlayer ? baseSize * 1.28f : baseSize;
            Color color = GetMarkerColor(marker.Kind);
            switch (marker.Kind)
            {
                case MarkerKind.LocalPlayer:
                case MarkerKind.Ally:
                    AddTriangle(vh, center, size + 2.4f, -marker.Rotation, _markerOutlineColor);
                    AddTriangle(vh, center, size, -marker.Rotation, color);
                    break;
                case MarkerKind.Enemy:
                    AddDiamond(vh, center, size + 2.2f, _markerOutlineColor);
                    AddDiamond(vh, center, size, color);
                    break;
                default:
                    AddSquare(vh, center, size + 1.8f, _markerOutlineColor);
                    AddSquare(vh, center, size, color);
                    break;
            }
        }
    }

    private bool TryGetVisibleTileBounds(Rect clipRect, float originX, float originY, float cellSize, out int minX, out int maxX, out int minY, out int maxY)
    {
        minX = Mathf.Clamp(Mathf.FloorToInt((clipRect.xMin - originX) / cellSize) - 1, 0, _width);
        maxX = Mathf.Clamp(Mathf.CeilToInt((clipRect.xMax - originX) / cellSize) + 1, 0, _width);
        minY = Mathf.Clamp(Mathf.FloorToInt((clipRect.yMin - originY) / cellSize) - 1, 0, _height);
        maxY = Mathf.Clamp(Mathf.CeilToInt((clipRect.yMax - originY) / cellSize) + 1, 0, _height);
        return minX < maxX && minY < maxY;
    }

    private Color GetMarkerColor(MarkerKind kind)
    {
        switch (kind)
        {
            case MarkerKind.LocalPlayer: return _localPlayerColor;
            case MarkerKind.Ally: return _allyColor;
            case MarkerKind.Enemy: return _enemyColor;
            default: return _objectColor;
        }
    }

    private static byte ResolveVisibleTile(int tileKind)
    {
        TileKind kind = (TileKind)tileKind;
        if (kind == TileKind.Spawn)
            return SpawnTile;
        if (kind == TileKind.Floor)
            return WalkableTile;
        return HiddenTile;
    }

    private static float ClampMapOrigin(float origin, float minVisible, float maxVisible, float mapSize)
    {
        float visibleSize = maxVisible - minVisible;
        if (mapSize <= visibleSize)
            return minVisible + (visibleSize - mapSize) * 0.5f;

        return Mathf.Clamp(origin, maxVisible - mapSize, minVisible);
    }

    private void MarkMeshDirty(bool immediate)
    {
        _meshDirty = true;
        if (!immediate || !isActiveAndEnabled)
            return;

        _meshDirty = false;
        _nextMeshRefreshTime = Time.unscaledTime + MinMeshRefreshInterval;
        SetVerticesDirty();
    }

    private static void AddQuad(VertexHelper vh, float minX, float minY, float maxX, float maxY, Color color)
    {
        if (maxX <= minX || maxY <= minY)
            return;

        int start = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = new Vector3(minX, minY, 0f);
        vh.AddVert(vertex);
        vertex.position = new Vector3(minX, maxY, 0f);
        vh.AddVert(vertex);
        vertex.position = new Vector3(maxX, maxY, 0f);
        vh.AddVert(vertex);
        vertex.position = new Vector3(maxX, minY, 0f);
        vh.AddVert(vertex);

        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private static void AddTriangle(VertexHelper vh, Vector2 center, float size, float rotationDegrees, Color color)
    {
        float half = size * 0.5f;
        Vector2 tip = center + Rotate(new Vector2(0f, half), rotationDegrees);
        Vector2 left = center + Rotate(new Vector2(-half * 0.58f, -half * 0.45f), rotationDegrees);
        Vector2 right = center + Rotate(new Vector2(half * 0.58f, -half * 0.45f), rotationDegrees);
        AddTriangleRaw(vh, tip, left, right, color);
    }

    private static void AddDiamond(VertexHelper vh, Vector2 center, float size, Color color)
    {
        float half = size * 0.5f;
        int start = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = center + new Vector2(0f, half);
        vh.AddVert(vertex);
        vertex.position = center + new Vector2(half, 0f);
        vh.AddVert(vertex);
        vertex.position = center + new Vector2(0f, -half);
        vh.AddVert(vertex);
        vertex.position = center + new Vector2(-half, 0f);
        vh.AddVert(vertex);

        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private static void AddSquare(VertexHelper vh, Vector2 center, float size, Color color)
    {
        float half = size * 0.5f;
        AddQuad(vh, center.x - half, center.y - half, center.x + half, center.y + half, color);
    }

    private static void AddTriangleRaw(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int start = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = a;
        vh.AddVert(vertex);
        vertex.position = b;
        vh.AddVert(vertex);
        vertex.position = c;
        vh.AddVert(vertex);
        vh.AddTriangle(start, start + 1, start + 2);
    }

    private static Vector2 Rotate(Vector2 value, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
    }
}
