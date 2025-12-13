using UnityEngine;
using System.Collections.Generic;

public class ClientGameState : MonoBehaviour
{
    public static ClientGameState Instance { get; private set; }

    //tmp
    public int MySide { get;  set; }
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

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    #region 맵

    public void CreateMap(int width, int height)
    {
        MapWidth = width;
        MapHeight = height;
        _tiles = new int[width, height];

        WorldView?.OnCreateMap(width, height);
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
        _entities.Clear();
        WorldView?.OnClearEntities();
    }

    public void SpawnOrUpdateEntity(ClientEntityInfo info)
    {
        _entities[info.EntityId] = info;
        WorldView?.OnSpawnOrUpdateEntity(info);
    }

    #endregion

    #region 리듬 액션 반영

    public void OnBeatAction(ClientBeatAction action)
    {
        //if (action.Accepted)
        //{
        //    Debug.Log($"[ OnBeatAction ] action.Accepted: [{action.Accepted}]");
        //    return;
        //}

        if (!_entities.TryGetValue(action.ActorId, out var entity))
        {
            Debug.Log("!_entities.TryGetValue(action.ActorId, out var entity)");
            return;
        }

        // 서버가 FromX/Y, ToX/Y를 줬으니 그대로 신뢰
        entity.X = action.ToX;
        entity.Y = action.ToY;
        _entities[action.ActorId] = entity;

        WorldView?.OnBeatAction(action, entity);
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
}
