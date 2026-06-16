using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class ClientGameState : MonoBehaviour
{
    private const int StageSummonMossyStoneRingAppearanceId = 503;
    private const int StageSummonRunicStoneGateAppearanceId = 504;

    public static ClientGameState Instance { get; private set; }

    //tmp
    public int MapWidth { get; private set; }
    public int MapHeight { get; private set; }

    // 타일 정보 (TileKind int 그대로)
    private int[,] _tiles;
    private AppearanceTileCell[,] _appearanceTiles;

    // ActorId 목록 & 내 ActorId
    public int[] PlayerActorIds { get; private set; } = new int[0];
    public int MyActorId { get; private set; }
    private readonly Dictionary<int, string> _playerUids = new();
    private readonly Dictionary<int, string> _playerRawDisplayNames = new();
    private readonly Dictionary<int, string> _playerDisplayNames = new();

    // 엔티티 상태
    private readonly Dictionary<int, ClientEntityInfo> _entities = new();
    public int EntityCount => _entities.Count;

    public IClientWorldView WorldView { get; set; }

    // UI
    public event System.Action<ClientEntityInfo> MyEntityChanged;
    public event System.Action PartyStateChanged;
    public event System.Action<int, int> MapCreated;
    public event System.Action<int, int, int> TileChanged;
    public event System.Action<ClientEntityInfo> EntityChanged;
    public event System.Action<int> EntityRemoved;
    public event System.Action EntitiesCleared;

    void Awake()
    {
        Instance = this;
    }

    public bool IsMapGenerationComplete { get; private set; } = false;
    public float MapGenProgress { get; private set; } = 0f;
    [SerializeField, Min(64)] private int _mapTilesPerFrame = 1000;

    #region 맵

    public void StartMapGeneration(MapAsset asset)
    {
        StartCoroutine(Co_CreateMapFromAsset(asset));
    }

    public void StartMapGeneration(MapJson mapJson)
    {
        StartCoroutine(Co_CreateMapFromJson(mapJson));
    }

    public System.Collections.IEnumerator Co_CreateMapFromAsset(MapAsset asset)
    {
        IsMapGenerationComplete = false;
        MapGenProgress = 0f;

        if (asset == null)
        {
            Debug.LogError("[ClientGameState] CreateMapFromAsset failed: asset is null");
            IsMapGenerationComplete = true;
            yield break;
        }

        asset.EnsureSize();

        yield return null;

        int totalTiles = asset.Width * asset.Height;
        var mapVisualBatch = WorldView as IClientWorldViewMapUpdateBatch;
        mapVisualBatch?.BeginMapVisualUpdate(totalTiles);

        CreateMap(asset.Width, asset.Height);
        WorldView?.OnSetAppearancePalette(asset.AppearancePalette);
        yield return null;

        int processedCount = 0;
        int tilesPerFrame = Mathf.Max(64, _mapTilesPerFrame);

        for (int y = 0; y < asset.Height; y++)
        {
            for (int x = 0; x < asset.Width; x++)
            {
                var cell = asset.Get(x, y);
                SetTile(x, y, (int)cell.Kind);
                var appearance = asset.GetAppearance(x, y);
                SetAppearanceTile(x, y, (int)appearance.Kind, appearance.Variant);

                processedCount++;
                if (processedCount % tilesPerFrame == 0)
                {
                    MapGenProgress = (float)processedCount / totalTiles;
                    yield return null;
                }
            }
        }

        MapGenProgress = 1.0f;
        IsMapGenerationComplete = true;
        mapVisualBatch?.EndMapVisualUpdate();
        Debug.Log("[ClientGameState] Map Generation Complete (Async)");
    }

    public System.Collections.IEnumerator Co_CreateMapFromJson(MapJson mapJson)
    {
        IsMapGenerationComplete = false;
        MapGenProgress = 0f;

        if (mapJson == null || mapJson.width <= 0 || mapJson.height <= 0)
        {
            Debug.LogError("[ClientGameState] CreateMapFromJson failed: invalid json");
            IsMapGenerationComplete = true;
            yield break;
        }

        yield return null;

        int totalTiles = mapJson.width * mapJson.height;
        var mapVisualBatch = WorldView as IClientWorldViewMapUpdateBatch;
        mapVisualBatch?.BeginMapVisualUpdate(totalTiles);

        CreateMap(mapJson.width, mapJson.height);
        bool hasAppearanceData = HasAppearanceData(mapJson);
        if (hasAppearanceData)
            WorldView?.OnSetAppearancePalette(LoadAppearancePalette(mapJson.appearancePalette));
        yield return null;

        int processedCount = 0;
        int tilesPerFrame = Mathf.Max(64, _mapTilesPerFrame);

        for (int y = 0; y < mapJson.height; y++)
        {
            for (int x = 0; x < mapJson.width; x++)
            {
                int idx = y * mapJson.width + x;
                MapJson.Cell cell = default;
                bool hasCell = mapJson.cells != null && idx >= 0 && idx < mapJson.cells.Length;
                if (hasCell)
                    cell = mapJson.cells[idx];

                int tileKind = hasCell ? cell.k : (int)TileKind.None;
                SetTile(x, y, tileKind);
                if (hasAppearanceData)
                    SetAppearanceTile(x, y, cell.a, cell.av);

                processedCount++;
                if (processedCount % tilesPerFrame == 0)
                {
                    MapGenProgress = (float)processedCount / totalTiles;
                    yield return null;
                }
            }
        }

        MapGenProgress = 1.0f;
        IsMapGenerationComplete = true;
        mapVisualBatch?.EndMapVisualUpdate();
        Debug.Log("[ClientGameState] Map Generation Complete (Server Json)");
    }

    public void CreateMap(int width, int height)
    {
        Debug.Log($"[ClientGameState] CreateMap: Size=({width}x{height}) - Resetting Tiles");
        MapWidth = width;
        MapHeight = height;
        _tiles = new int[width, height];
        _appearanceTiles = new AppearanceTileCell[width, height];

        if (WorldView == null)
            Debug.LogWarning("[ClientGameState] WorldView is null during CreateMap!");
        else
            WorldView.OnCreateMap(width, height);

        MapCreated?.Invoke(width, height);
    }

    private AppearanceAutoTilePalette LoadAppearancePalette(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
            return null;

        var palette = Resources.Load<AppearanceAutoTilePalette>(resourcePath);
        if (palette == null)
            Debug.LogWarning($"[ClientGameState] Appearance palette not found: Resources/{resourcePath}");

        return palette;
    }

    private bool HasAppearanceData(MapJson mapJson)
    {
        if (mapJson == null)
            return false;

        if (!string.IsNullOrEmpty(mapJson.appearancePalette))
            return true;

        if (mapJson.cells == null)
            return false;

        for (int i = 0; i < mapJson.cells.Length; i++)
        {
            if (mapJson.cells[i].a != 0 || mapJson.cells[i].av != 0)
                return true;
        }

        return false;
    }

    public void SetTile(int x, int y, int tileKind)
    {
        if (_tiles == null) return;
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) return;

        _tiles[x, y] = tileKind;
        WorldView?.OnSetTile(x, y, tileKind);
        TileChanged?.Invoke(x, y, tileKind);
    }

    public void SetAppearanceTile(int x, int y, int appearanceKind, int appearanceVariant)
    {
        if (_appearanceTiles == null) return;
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) return;

        _appearanceTiles[x, y] = new AppearanceTileCell
        {
            Kind = (AppearanceTileKind)appearanceKind,
            Variant = (byte)appearanceVariant
        };

        WorldView?.OnSetAppearanceTile(x, y, appearanceKind, appearanceVariant);
    }

    public int GetTileKind(int x, int y)
    {
        if (_tiles == null) return (int)TileKind.None;
        if (!IsInsideMap(x, y)) return (int)TileKind.None;
        return _tiles[x, y];
    }

    public bool IsInsideMap(int x, int y)
    {
        return MapWidth > 0
               && MapHeight > 0
               && x >= 0
               && y >= 0
               && x < MapWidth
               && y < MapHeight;
    }

    public bool TryClampToMap(int x, int y, out int clampedX, out int clampedY)
    {
        clampedX = x;
        clampedY = y;

        if (MapWidth <= 0 || MapHeight <= 0)
            return false;

        clampedX = Mathf.Clamp(x, 0, MapWidth - 1);
        clampedY = Mathf.Clamp(y, 0, MapHeight - 1);
        return true;
    }

    public bool IsWalkable(int x, int y)
    {
        var kind = (TileKind)GetTileKind(x, y);
        return kind == TileKind.Floor || kind == TileKind.Spawn;
    }

    public bool IsValidTownMoveCell(int x, int y)
    {
        return IsInsideMap(x, y) && IsWalkable(x, y);
    }

    public bool TryResolveTownMoveCell(int preferredX, int preferredY, out int resolvedX, out int resolvedY)
    {
        resolvedX = preferredX;
        resolvedY = preferredY;

        if (IsValidTownMoveCell(preferredX, preferredY))
            return true;

        if (_tiles == null || MapWidth <= 0 || MapHeight <= 0)
            return false;

        bool found = false;
        int bestDistance = int.MaxValue;
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                if (!IsValidTownMoveCell(x, y))
                    continue;

                int dx = x - preferredX;
                int dy = y - preferredY;
                int distance = dx * dx + dy * dy;
                if (distance >= bestDistance)
                    continue;

                found = true;
                bestDistance = distance;
                resolvedX = x;
                resolvedY = y;
            }
        }

        return found;
    }

    public static bool EntityOccupiesCell(ClientEntityInfo entity, int x, int y)
    {
        int sizeX = Mathf.Max(1, entity.SizeX);
        int sizeY = Mathf.Max(1, entity.SizeY);
        return x >= entity.X && x < entity.X + sizeX
               && y >= entity.Y && y < entity.Y + sizeY;
    }

    public static bool IsPassThroughObjectAppearance(int appearanceId)
    {
        return appearanceId == StageSummonMossyStoneRingAppearanceId
               || appearanceId == StageSummonRunicStoneGateAppearanceId;
    }

    public static bool EntityBlocksMovement(ClientEntityInfo entity)
    {
        if (entity.Hp <= 0)
            return false;

        if (entity.EntityType == (int)EntityType.Object
            && IsPassThroughObjectAppearance(entity.AppearanceId))
        {
            return false;
        }

        return true;
    }

    public bool IsBlockingObjectAt(int x, int y, int ignoreEntityId = -1)
    {
        foreach (var kv in _entities)
        {
            if (kv.Key == ignoreEntityId)
                continue;

            var entity = kv.Value;
            if (entity.EntityType != (int)EntityType.Object || !EntityBlocksMovement(entity))
                continue;

            if (EntityOccupiesCell(entity, x, y))
                return true;
        }

        return false;
    }

    public bool IsWalkableForPathing(int x, int y)
    {
        return IsWalkable(x, y) && !IsBlockingObjectAt(x, y);
    }

    public bool IsOccupied(int x, int y, int ignoreEntityId = -1)
    {
        foreach (var kv in _entities)
        {
            if (kv.Key == ignoreEntityId)
                continue;

            if (!EntityBlocksMovement(kv.Value))
                continue;

            if (EntityOccupiesCell(kv.Value, x, y))
                return true;
        }

        return false;
    }

    public IEnumerable<ClientEntityInfo> EnumerateEntities()
        => _entities.Values;

    #endregion

    #region 플레이어/Actor

    public void SetPlayerActorIds(int[] actorIds)
    {
        PlayerActorIds = actorIds ?? new int[0];
        if (P2PDebugConfig.LogOverheadEnabled)
            Debug.Log($"[P2PPlayerSync] SetPlayerActorIds actors={string.Join(",", PlayerActorIds)}");

        PartyStateChanged?.Invoke();
    }

    public void SetMyActorId(int actorId)
    {
        MyActorId = actorId;
        RebuildPlayerDisplayNames();
        string uid = TryGetPlayerUid(actorId, out var resolvedUid) ? resolvedUid : "-";
        string displayName = TryGetPlayerDisplayName(actorId, out var resolvedName) ? resolvedName : "-";
        Debug.Log($"[P2PPlayerSync] SetMyActorId actor={actorId} uid={uid} name={displayName}");

        if (_entities.TryGetValue(actorId, out var entity))
            WorldView?.OnSpawnOrUpdateEntity(entity);

        PartyStateChanged?.Invoke();
    }

    public void SetPlayerRoster(IEnumerable<(int ActorId, string Uid)> roster)
        => SetPlayerRoster(roster?.Select(x => (x.ActorId, x.Uid, DisplayName: "")));

    public void SetPlayerRoster(IEnumerable<(int ActorId, string Uid, string DisplayName)> roster)
    {
        _playerUids.Clear();
        _playerRawDisplayNames.Clear();
        _playerDisplayNames.Clear();

        if (roster == null)
        {
            PartyStateChanged?.Invoke();
            return;
        }

        foreach (var (actorId, uid, displayName) in roster)
        {
            if (actorId <= 0)
                continue;

            _playerUids[actorId] = uid ?? "";
            _playerRawDisplayNames[actorId] = displayName ?? "";
        }

        RebuildPlayerDisplayNames();

        if (P2PDebugConfig.LogOverheadEnabled)
        {
            var entries = _playerUids
                .OrderBy(x => x.Key)
                .Select(x =>
                {
                    string uid = string.IsNullOrWhiteSpace(x.Value) ? "-" : x.Value;
                    string name = _playerDisplayNames.TryGetValue(x.Key, out var resolvedName) && !string.IsNullOrWhiteSpace(resolvedName)
                        ? resolvedName
                        : "-";
                    return $"{x.Key}:{uid}/{name}";
                })
                .ToArray();
            Debug.Log($"[P2PPlayerSync] SetPlayerRoster actors={string.Join(",", entries)}");
        }

        foreach (var duplicateUid in _playerUids
                     .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                     .GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Select(x => x.Key).Distinct().Count() > 1))
        {
            Debug.LogWarning(
                $"[P2PPlayerSync] Duplicate uid mapped to multiple actors uid={duplicateUid.Key} " +
                $"actors={string.Join(",", duplicateUid.Select(x => x.Key).OrderBy(x => x))}");
        }

        PartyStateChanged?.Invoke();
    }

    public void ClearPlayerRoster()
    {
        _playerUids.Clear();
        _playerRawDisplayNames.Clear();
        _playerDisplayNames.Clear();
        PartyStateChanged?.Invoke();
    }

    public bool TryGetPlayerUid(int actorId, out string uid)
        => _playerUids.TryGetValue(actorId, out uid);

    public bool TryGetPlayerDisplayName(int actorId, out string displayName)
        => _playerDisplayNames.TryGetValue(actorId, out displayName);

    public IEnumerable<(int ActorId, string Uid)> EnumeratePlayerRoster()
    {
        foreach (var kv in _playerUids)
            yield return (kv.Key, kv.Value);
    }

    public IEnumerable<(int ActorId, string Uid, string DisplayName)> EnumeratePlayerRosterWithDisplayNames()
    {
        foreach (var kv in _playerUids)
        {
            _playerDisplayNames.TryGetValue(kv.Key, out var displayName);
            yield return (kv.Key, kv.Value, displayName ?? "");
        }
    }

    private void RebuildPlayerDisplayNames()
    {
        _playerDisplayNames.Clear();

        int guestIndex = 1;
        foreach (var roster in _playerUids.OrderBy(x => x.Key))
        {
            int actorId = roster.Key;
            string uid = roster.Value ?? "";
            _playerRawDisplayNames.TryGetValue(actorId, out var rawDisplayName);

            string displayName = ResolvePlayerDisplayName(actorId, uid, rawDisplayName);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = $"Guest_{guestIndex:00}";
                guestIndex++;
            }

            _playerDisplayNames[actorId] = displayName;
        }
    }

    private string ResolvePlayerDisplayName(int actorId, string uid, string rawDisplayName)
    {
        if (actorId == MyActorId)
        {
            string steamName = AppBootstrap.Instance?.Root?.SteamPlatform?.DisplayName ?? "";
            if (!string.IsNullOrWhiteSpace(steamName))
                return steamName.Trim();
        }

        string normalizedName = NormalizePlayerDisplayName(rawDisplayName, uid);
        if (!string.IsNullOrWhiteSpace(normalizedName))
            return normalizedName;

        var participants = SessionContext.Instance?.LastMatchManifest?.Participants;
        if (participants != null)
        {
            var participant = participants.FirstOrDefault(x =>
                x != null
                && (x.ActorId == actorId
                    || (!string.IsNullOrWhiteSpace(uid)
                        && string.Equals(x.Uid, uid, StringComparison.OrdinalIgnoreCase))));
            normalizedName = NormalizePlayerDisplayName(participant?.DisplayName, uid);
            if (!string.IsNullOrWhiteSpace(normalizedName))
                return normalizedName;
        }

        return "";
    }

    private static string NormalizePlayerDisplayName(string displayName, string uid)
    {
        string clean = (displayName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(clean))
            return "";

        if (!string.IsNullOrWhiteSpace(uid) && string.Equals(clean, uid, StringComparison.OrdinalIgnoreCase))
            return "";

        if (string.Equals(clean, "Guest", StringComparison.OrdinalIgnoreCase)
            || string.Equals(clean, "Unknown", StringComparison.OrdinalIgnoreCase)
            || string.Equals(clean, "NullName", StringComparison.OrdinalIgnoreCase)
            || string.Equals(clean, "-", StringComparison.Ordinal))
        {
            return "";
        }

        return clean;
    }

    #endregion

    #region 엔티티

    public bool TryGetEntity(int entityId, out ClientEntityInfo info)
        => _entities.TryGetValue(entityId, out info);

    public bool TryGetMyEntity(out ClientEntityInfo info)
        => _entities.TryGetValue(MyActorId, out info);

    public void ApplyLocalPlayerAppearance(int appearanceId)
        => ApplyPlayerAppearance(MyActorId, appearanceId);

    public void ApplyPlayerAppearance(int actorId, int appearanceId)
    {
        if (actorId <= 0 || appearanceId < 0)
            return;

        if (!_entities.TryGetValue(actorId, out var info))
            return;

        if (info.EntityType != (int)EntityType.Player || info.AppearanceId == appearanceId)
            return;

        info.AppearanceId = appearanceId;
        UpdateEntityState(info, true);
    }

    public void ClearEntities()
    {
        Debug.Log($"[ClientGameState] ClearEntities. Count={_entities.Count}");
        _entities.Clear();
        WorldView?.OnClearEntities();
        EntitiesCleared?.Invoke();
        PartyStateChanged?.Invoke();
    }

    public void SpawnOrUpdateEntity(ClientEntityInfo info)
    {
        if (MapWidth > 0 && MapHeight > 0)
        {
            if (info.X < 0 || info.X >= MapWidth || info.Y < 0 || info.Y >= MapHeight)
                Debug.LogError($"[ClientGameState] Spawn Fail: Out of bounds. ID={info.EntityId} Pos=({info.X},{info.Y}) MapSize=({MapWidth}x{MapHeight})");
        }
        else
        {
            Debug.LogWarning($"[ClientGameState] Spawn Warning: Map size not set yet. ID={info.EntityId}");
        }

        info = ResolveMaxHp(info);
        _entities[info.EntityId] = info;

        if (P2PDebugConfig.LogOverheadEnabled && info.EntityType == (int)EntityType.Player)
        {
            string uid = TryGetPlayerUid(info.EntityId, out var resolvedUid) ? resolvedUid : "-";
            Debug.Log(
                $"[P2PPlayerSync] EntityStateUpsert actor={info.EntityId} uid={uid} pos=({info.X},{info.Y}) " +
                $"hp={info.Hp} app={info.AppearanceId} myActor={MyActorId}");
        }

        if (WorldView == null)
            Debug.LogError($"[ClientGameState] WorldView is null! Entity renders will fail. ID={info.EntityId}");
        else
            WorldView.OnSpawnOrUpdateEntity(info);

        EntityChanged?.Invoke(info);
        NotifyMyEntityChanged(info.EntityId);
    }

    public void UpdateEntityState(ClientEntityInfo info, bool refreshWorldView = true)
    {
        info = ResolveMaxHp(info);
        _entities[info.EntityId] = info;
        if (refreshWorldView)
            WorldView?.OnSpawnOrUpdateEntity(info);
        EntityChanged?.Invoke(info);
        NotifyMyEntityChanged(info.EntityId);
    }

    public bool RemoveEntity(int entityId)
    {
        if (!_entities.Remove(entityId)) return false;
        WorldView?.OnDespawnEntity(entityId);
        EntityRemoved?.Invoke(entityId);
        PartyStateChanged?.Invoke();
        return true;
    }

    private ClientEntityInfo ResolveMaxHp(ClientEntityInfo info)
    {
        if (_entities.TryGetValue(info.EntityId, out var previous))
        {
            if (info.MaxHp <= 0 && previous.MaxHp > 0)
                info.MaxHp = previous.MaxHp;
            if (info.SizeX <= 0 && previous.SizeX > 0)
                info.SizeX = previous.SizeX;
            if (info.SizeY <= 0 && previous.SizeY > 0)
                info.SizeY = previous.SizeY;
        }

        // Current packets do not carry MaxHp. Keep the first positive HP snapshot as the HUD baseline.
        if (info.MaxHp <= 0)
            info.MaxHp = Mathf.Max(0, info.Hp);
        if (info.SizeX <= 0)
            info.SizeX = 1;
        if (info.SizeY <= 0)
            info.SizeY = 1;

        return info;
    }

    #endregion

    #region 리듬 액션 반영

    public void OnBeatAction(ClientBeatAction action)
    {
        ApplyBeatAction(action, enforceTownMoveGrid: false);
    }

    public void OnTownBeatAction(ClientBeatAction action)
    {
        ApplyBeatAction(action, enforceTownMoveGrid: true);
    }

    private void ApplyBeatAction(ClientBeatAction action, bool enforceTownMoveGrid)
    {
        if (!_entities.TryGetValue(action.ActorId, out var entity))
        {
            Debug.LogWarning($"[ClientGameState] OnBeatAction: Entity {action.ActorId} not found");
            return;
        }

        // [서버 권위] Move 패킷에서만 위치 갱신
        // Skill 패킷의 ToX/Y는 시전자 위치(이동 없음)이므로 위치 갱신에 쓰면 안 됨
        // 이동 스킬(Dash/Blink)은 BroadcastMoveResult → ActionKind.Move 패킷으로 별도 전달됨
        bool isMoveAction = action.ActionKind == (int)ActionKind.Move;
        if (isMoveAction)
        {
            bool fromInside = IsMoveTargetValid(action.FromX, action.FromY, enforceTownMoveGrid);
            bool toInside = IsMoveTargetValid(action.ToX, action.ToY, enforceTownMoveGrid);

            if (!toInside)
            {
                if (P2PDebugConfig.TraceInput || P2PDebugConfig.LogOverheadEnabled)
                {
                    Debug.LogWarning(
                        $"[ClientGameState] Block out-of-map move actor={action.ActorId} accepted={action.Accepted} " +
                        $"from=({action.FromX},{action.FromY}) to=({action.ToX},{action.ToY}) map=({MapWidth}x{MapHeight})");
                }

                ResolveSafeMapCell(entity, enforceTownMoveGrid, out int safeX, out int safeY);
                action.Accepted = false;
                action.FromX = safeX;
                action.FromY = safeY;
                action.ToX = safeX;
                action.ToY = safeY;
            }
            else if (!fromInside)
            {
                ResolveSafeMapCell(entity, enforceTownMoveGrid, out int safeX, out int safeY);
                action.FromX = safeX;
                action.FromY = safeY;
            }
        }

        if (isMoveAction && action.Accepted && !action.HasHpUpdate && entity.X == action.ToX && entity.Y == action.ToY)
        {
            entity.Rotation = action.Rotation;
            _entities[action.ActorId] = entity;
            EntityChanged?.Invoke(entity);
            NotifyMyEntityChanged(action.ActorId);
            return;
        }

        if (isMoveAction)
        {
            if (action.Accepted)
            {
                entity.X = action.ToX;
                entity.Y = action.ToY;
            }
            else
            {
                // 서버 거부 시 원위치 복원
                entity.X = action.FromX;
                entity.Y = action.FromY;
            }
        }

        entity.Rotation = action.Rotation;

        if (action.HasHpUpdate)
            entity.Hp = action.NewHp;

        _entities[action.ActorId] = entity;
        if (P2PDebugConfig.LogOverheadEnabled
            && action.ActionKind == (int)ActionKind.Move
            && ShouldTracePlayerBeatAction(action.ActorId))
        {
            string uid = TryGetPlayerUid(action.ActorId, out var resolvedUid) ? resolvedUid : "-";
            Debug.Log(
                $"[P2PPlayerSync] ApplyBeatMove actor={action.ActorId} uid={uid} accepted={action.Accepted} " +
                $"from=({action.FromX},{action.FromY}) to=({action.ToX},{action.ToY}) now=({entity.X},{entity.Y}) myActor={MyActorId}");
        }
        WorldView?.OnBeatAction(action, entity);
        EntityChanged?.Invoke(entity);
        NotifyMyEntityChanged(action.ActorId);
    }

    private bool IsMoveTargetValid(int x, int y, bool enforceTownMoveGrid)
    {
        return enforceTownMoveGrid ? IsValidTownMoveCell(x, y) : IsInsideMap(x, y);
    }

    private void ResolveSafeMapCell(ClientEntityInfo entity, bool enforceTownMoveGrid, out int safeX, out int safeY)
    {
        if (IsMoveTargetValid(entity.X, entity.Y, enforceTownMoveGrid))
        {
            safeX = entity.X;
            safeY = entity.Y;
            return;
        }

        if (enforceTownMoveGrid && TryResolveTownMoveCell(entity.X, entity.Y, out safeX, out safeY))
            return;

        if (TryClampToMap(entity.X, entity.Y, out safeX, out safeY))
            return;

        safeX = entity.X;
        safeY = entity.Y;
    }

    private bool ShouldTracePlayerBeatAction(int actorId)
    {
        if (actorId <= 0)
            return false;

        if (_playerUids.ContainsKey(actorId))
            return true;

        return PlayerActorIds != null && Array.IndexOf(PlayerActorIds, actorId) >= 0;
    }

    #endregion

    #region UI

    private void NotifyMyEntityChanged(int entityId)
    {
        if (entityId != MyActorId) return;

        if (_entities.TryGetValue(MyActorId, out var entity))
            MyEntityChanged?.Invoke(entity);
        else
            Debug.LogWarning($"[NotifyMyEntityChanged] MyActorId matches {entityId}, but Entity NOT in dict!");
    }

    #endregion

    public void OnInitGameCompleted()
    {
        WorldView?.OnInitGameCompleted();
    }
}

public struct ClientEntityInfo
{
    public int EntityId;
    public int EntityType;
    public int AppearanceId;
    public int X;
    public int Y;
    public float Rotation;
    public int Hp;
    public int MaxHp;
    public int GroupId;
    public int SizeX;
    public int SizeY;
}

public struct ClientBeatAction
{
    public long BeatIndex;
    public int ActorId;
    public int ActionKind;
    public int FromX;
    public int FromY;
    public int ToX;
    public int ToY;
    public float Rotation;
    public bool Accepted;

    public bool HasHpUpdate;
    public int NewHp;

    public int DiffMs;
}
