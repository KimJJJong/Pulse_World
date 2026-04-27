using UnityEngine;
using System.Collections.Generic;

public class ClientGameState : MonoBehaviour
{
    public static ClientGameState Instance { get; private set; }

    //tmp
    public int MapWidth { get; private set; }
    public int MapHeight { get; private set; }

    // 타일 정보 (TileKind int 그대로)
    private int[,] _tiles;

    // ActorId 목록 & 내 ActorId
    public int[] PlayerActorIds { get; private set; } = new int[0];
    public int MyActorId { get; private set; }
    private readonly Dictionary<int, string> _playerUids = new();

    // 엔티티 상태
    private readonly Dictionary<int, ClientEntityInfo> _entities = new();

    public IClientWorldView WorldView { get; set; }

    // UI
    public event System.Action<ClientEntityInfo> MyEntityChanged;

    void Awake()
    {
        Instance = this;
    }

    public bool IsMapGenerationComplete { get; private set; } = false;
    public float MapGenProgress { get; private set; } = 0f;

    #region 맵

    public void StartMapGeneration(MapAsset asset)
    {
        StartCoroutine(Co_CreateMapFromAsset(asset));
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
        yield return null;
        yield return null;

        CreateMap(asset.Width, asset.Height);
        yield return null;

        int totalTiles = asset.Width * asset.Height;
        int processedCount = 0;
        int tilesPerFrame = 200;

        for (int y = 0; y < asset.Height; y++)
        {
            for (int x = 0; x < asset.Width; x++)
            {
                var cell = asset.Get(x, y);
                SetTile(x, y, (int)cell.Kind);

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
        Debug.Log("[ClientGameState] Map Generation Complete (Async)");
    }

    public void CreateMap(int width, int height)
    {
        Debug.Log($"[ClientGameState] CreateMap: Size=({width}x{height}) - Resetting Tiles");
        MapWidth = width;
        MapHeight = height;
        _tiles = new int[width, height];

        if (WorldView == null)
            Debug.LogWarning("[ClientGameState] WorldView is null during CreateMap!");
        else
            WorldView.OnCreateMap(width, height);
    }

    public void SetTile(int x, int y, int tileKind)
    {
        if (_tiles == null) return;
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) return;

        _tiles[x, y] = tileKind;
        WorldView?.OnSetTile(x, y, tileKind);
    }

    public int GetTileKind(int x, int y)
    {
        if (_tiles == null) return (int)TileKind.None;
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) return (int)TileKind.None;
        return _tiles[x, y];
    }

    public bool IsWalkable(int x, int y)
    {
        var kind = (TileKind)GetTileKind(x, y);
        return kind == TileKind.Floor || kind == TileKind.Spawn;
    }

    public bool IsOccupied(int x, int y, int ignoreEntityId = -1)
    {
        foreach (var kv in _entities)
        {
            if (kv.Key == ignoreEntityId)
                continue;

            if (kv.Value.X == x && kv.Value.Y == y)
                return true;
        }

        return false;
    }

    public IEnumerable<ClientEntityInfo> EnumerateEntities()
        => _entities.Values;

    #endregion

    #region 플레이어/Actor

    public void SetPlayerActorIds(int[] actorIds) => PlayerActorIds = actorIds;
    public void SetMyActorId(int actorId) => MyActorId = actorId;

    public void SetPlayerRoster(IEnumerable<(int ActorId, string Uid)> roster)
    {
        _playerUids.Clear();

        if (roster == null)
            return;

        foreach (var (actorId, uid) in roster)
        {
            if (actorId <= 0)
                continue;

            _playerUids[actorId] = uid ?? "";
        }
    }

    public void ClearPlayerRoster() => _playerUids.Clear();

    public bool TryGetPlayerUid(int actorId, out string uid)
        => _playerUids.TryGetValue(actorId, out uid);

    public IEnumerable<(int ActorId, string Uid)> EnumeratePlayerRoster()
    {
        foreach (var kv in _playerUids)
            yield return (kv.Key, kv.Value);
    }

    #endregion

    #region 엔티티

    public bool TryGetEntity(int entityId, out ClientEntityInfo info)
        => _entities.TryGetValue(entityId, out info);

    public bool TryGetMyEntity(out ClientEntityInfo info)
        => _entities.TryGetValue(MyActorId, out info);

    public void ClearEntities()
    {
        Debug.Log($"[ClientGameState] ClearEntities. Count={_entities.Count}");
        _entities.Clear();
        WorldView?.OnClearEntities();
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

        _entities[info.EntityId] = info;

        if (WorldView == null)
            Debug.LogError($"[ClientGameState] WorldView is null! Entity renders will fail. ID={info.EntityId}");
        else
            WorldView.OnSpawnOrUpdateEntity(info);

        NotifyMyEntityChanged(info.EntityId);
    }

    public void UpdateEntityState(ClientEntityInfo info)
    {
        Debug.Log($"[UpdateEntityState] IN");
        _entities[info.EntityId] = info;
        WorldView?.OnSpawnOrUpdateEntity(info);
        NotifyMyEntityChanged(info.EntityId);
    }

    public bool RemoveEntity(int entityId)
    {
        if (!_entities.Remove(entityId)) return false;
        WorldView?.OnDespawnEntity(entityId);
        return true;
    }

    #endregion

    #region 리듬 액션 반영

    public void OnBeatAction(ClientBeatAction action)
    {
        if (!_entities.TryGetValue(action.ActorId, out var entity))
        {
            Debug.LogWarning($"[ClientGameState] OnBeatAction: Entity {action.ActorId} not found");
            return;
        }

        // [서버 권위] Move 패킷에서만 위치 갱신
        // Skill 패킷의 ToX/Y는 시전자 위치(이동 없음)이므로 위치 갱신에 쓰면 안 됨
        // 이동 스킬(Dash/Blink)은 BroadcastMoveResult → ActionKind.Move 패킷으로 별도 전달됨
        if (action.ActionKind == (int)ActionKind.Move)
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
        WorldView?.OnBeatAction(action, entity);
        NotifyMyEntityChanged(action.ActorId);
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
