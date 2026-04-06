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

    // 엔티티 상태
    private readonly Dictionary<int, ClientEntityInfo> _entities = new();

    // TODO: 뷰(유닛 프리팹) 매핑이 필요하면 여기서 관리하거나, 별도 ViewManager 사용
    public IClientWorldView WorldView { get; set; } // 선택 사항: 바인딩해도 되고 안 해도 됨

    // UI ?
    public event System.Action<ClientEntityInfo> MyEntityChanged;

    void Awake()
    {

        Instance = this;
    }

    public bool IsMapGenerationComplete { get; private set; } = false;
    public float MapGenProgress { get; private set; } = 0f;

    #region 맵
    // 기존 동기 메서드 -> Coroutine 호출용 래퍼로 사용하거나, 외부에서 StartCoroutine(Co_...) 호출
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
            IsMapGenerationComplete = true; // 실패해도 완료 처리는 해야 무한 로딩 방지
            yield break;
        }

        asset.EnsureSize();

        // 씬 타일 오브젝트(BoardView 자식들)의 Awake/Start 완료를 보장하기 위해 대기.
        // Unity 씬 로드 후: 같은 프레임에 Awake, 다음 프레임에 Start가 실행된다.
        // TryBindTilesFromScene은 씬 자식 오브젝트를 이름으로 탐색하므로
        // 최소 2프레임(+안전마진 1) 대기 후 호출해야 한다.
        yield return null; // 프레임 1: Awake 완료
        yield return null; // 프레임 2: Start 완료
        yield return null; // 프레임 3: 안전 마진

        // 1) 맵 초기화 (내부에서 TryBindTilesFromScene 호출)
        CreateMap(asset.Width, asset.Height);
        yield return null; // 바인딩 후 한 프레임 양보

        // 2) 타일 채우기 (비동기 분산)
        int totalTiles = asset.Width * asset.Height;
        int processedCount = 0;
        int tilesPerFrame = 200; // 한 프레임당 처리할 타일 수 (조절 가능)

        for (int y = 0; y < asset.Height; y++)
        {
            for (int x = 0; x < asset.Width; x++)
            {
                var cell = asset.Get(x, y);
                SetTile(x, y, (int)cell.Kind);

                processedCount++;
                
                // 진행률 갱신
                if (processedCount % tilesPerFrame == 0)
                {
                    MapGenProgress = (float)processedCount / totalTiles;
                    yield return null; // 프레임 양보
                }
            }
        }

        MapGenProgress = 1.0f;
        IsMapGenerationComplete = true;
        Debug.Log("[ClientGameState] Map Generation Complete (Async)");
    }

    // (기존 동기 메서드는 하위 호환성 위해 남겨두거나 삭제. 여기서는 덮어씌웠으므로 삭제됨)
    // 필요한 경우 오버로딩: public bool CreateMapFromAssetSync(MapAsset asset) { ... }

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
        if (_tiles == null)
            return;

        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
            return;

        _tiles[x, y] = tileKind;
        WorldView?.OnSetTile(x, y, tileKind);
    }

    #endregion

    #region 플레이어/Actor

    public void SetPlayerActorIds(int[] actorIds)
    {
        PlayerActorIds = actorIds;
    }

    public void SetMyActorId(int actorId)
    {
        MyActorId = actorId;
    }

    #endregion

    #region 엔티티
    public bool TryGetEntity(int entityId, out ClientEntityInfo info)
    {
        return _entities.TryGetValue(entityId, out info);
    }
    public bool TryGetMyEntity(out ClientEntityInfo info)
    {
        info = default;
        //if (MyActorId < 0)
        //{
        //    Debug.LogWarning($"MyActorId : [{MyActorId}]");
        //    return false;
        //}

        return _entities.TryGetValue(MyActorId, out info);
    }

    public void ClearEntities()
    {
        Debug.Log($"[ClientGameState] ClearEntities. Count={_entities.Count}");
        _entities.Clear();
        WorldView?.OnClearEntities();
    }

    public void SpawnOrUpdateEntity(ClientEntityInfo info)
    {
        // 1) 맵 범위 체크 (Debug)
        if (MapWidth > 0 && MapHeight > 0)
        {
            if (info.X < 0 || info.X >= MapWidth || info.Y < 0 || info.Y >= MapHeight)
            {
                Debug.LogError($"[ClientGameState] Spawn Fail: Out of bounds. ID={info.EntityId} Pos=({info.X},{info.Y}) MapSize=({MapWidth}x{MapHeight})");
                // 그래도 데이터는 넣을지, 리턴할지 결정. 일단 경고 후 진행.
            }
        }
        else
        {
            Debug.LogWarning($"[ClientGameState] Spawn Warning: Map size not set yet. ID={info.EntityId}");
        }

        _entities[info.EntityId] = info;

        // 2) WorldView 연결 체크
        if (WorldView == null)
        {
            Debug.LogError($"[ClientGameState] WorldView is null! Entity renders will fail. ID={info.EntityId}");
        }
        else
        {
            // Debug.Log($"[ClientGameState] Spawning in View: ID={info.EntityId} Pos=({info.X},{info.Y})");
            WorldView.OnSpawnOrUpdateEntity(info);
        }

        // State Change
        NotifyMyEntityChanged(info.EntityId);
    }
    public void UpdateEntityState(ClientEntityInfo info)
    {
        Debug.Log($"[UpdateEntityState] IN");

        _entities[info.EntityId] = info;

        // 뷰가 업데이트 전용을 가지고 있으면 그걸로 분리하는 게 베스트
        WorldView?.OnSpawnOrUpdateEntity(info);

        // 내 캐릭터면 HUD 갱신 이벤트
        NotifyMyEntityChanged(info.EntityId);
    }


    public bool RemoveEntity(int entityId)
    {
        if (!_entities.Remove(entityId))
            return false;

        WorldView?.OnDespawnEntity(entityId);
        return true;
    }


    #endregion

    #region 리듬 액션 반영

    public void OnBeatAction(ClientBeatAction action)
    {


        if (!_entities.TryGetValue(action.ActorId, out var entity))
        {
            Debug.LogWarning($"!_entities.TryGetValue(action.ActorId, out var entity) || [action.ActionId = {action.ActorId}] || entity = [{entity}] || action = {action} ");
            return;
        }

        // 서버가 FromX/Y, ToX/Y를 줬으니 그대로 신뢰
        entity.X = action.ToX;
        entity.Y = action.ToY;

        if (action.HasHpUpdate)
            entity.Hp = action.NewHp;


        _entities[action.ActorId] = entity;
        // State Change
        WorldView?.OnBeatAction(action, entity);
        NotifyMyEntityChanged(action.ActorId);



    }

    #endregion

    #region UI
    private void NotifyMyEntityChanged(int entityId)
    {
        if (entityId != MyActorId) 
        {
            // 내 것이 아님 (정상)
            return;
        }

        if (_entities.TryGetValue(MyActorId, out var entity))
        {
            if (MyEntityChanged == null)
            {
                //Debug.LogWarning($"[NotifyMyEntityChanged] Found MyActor {MyActorId} but NO SUBSCRIBER!");
            }
            else
            {
                 // Debug.Log($"[NotifyMyEntityChanged] Invoking... Hp={entity.Hp}");
                 MyEntityChanged.Invoke(entity);
            }
        }
        else
        {
            Debug.LogWarning($"[NotifyMyEntityChanged] MyActorId matches {entityId}, but Entity NOT in dict!");
        }
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
    public int AppearanceId; // Renamed from ModelId
    public int X;
    public int Y;
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
    public bool Accepted;

    public bool HasHpUpdate;
    public int NewHp;

    public int DiffMs;
}
