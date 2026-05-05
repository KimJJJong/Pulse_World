using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MapAsset을 "이름"으로 찾기 위한 레지스트리.
/// - 인스펙터에 MapAsset 목록을 넣어두고
/// - 서버에서 온 MapName으로 해당 MapAsset을 찾아 사용한다.
/// 
/// ※ 주의: "다른 시스템(엔티티/리듬/패킷)"에는 영향 없음.
/// 오직 맵 생성/타일 채우기만 담당.
/// </summary>
public sealed class MapRegistry : MonoBehaviour
{
    private const string MapResourcesPath = "Data/Map";

    public static MapRegistry Instance { get; private set; }

    [Header("Maps")]
    [Tooltip("게임에서 사용할 MapAsset들을 모두 넣어둔다. (서버 MapName과 일치해야 함)")]
    public MapAsset[] Maps = Array.Empty<MapAsset>();

    private readonly Dictionary<string, MapAsset> _byName = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Rebuild();
    }

    /// <summary>
    /// 인스펙터/런타임에서 Maps가 바뀌었을 때 다시 빌드할 수 있게 해둠.
    /// </summary>
    public void Rebuild()
    {
        _byName.Clear();

        var uniqueMaps = new List<MapAsset>();
        AppendUniqueMaps(uniqueMaps, Maps);

        var resourceMaps = Resources.LoadAll<MapAsset>(MapResourcesPath);
        AppendUniqueMaps(uniqueMaps, resourceMaps);

        foreach (var m in uniqueMaps)
        {
            RegisterMap(m);
        }

        if ((Maps == null || Maps.Length == 0) && uniqueMaps.Count > 0)
        {
            Maps = uniqueMaps.ToArray();
        }

        Debug.Log($"[MapRegistry] Loaded maps: {_byName.Count} (Resources: {resourceMaps.Length})");
    }

    private static void AppendUniqueMaps(List<MapAsset> buffer, IEnumerable<MapAsset> source)
    {
        if (source == null)
        {
            return;
        }

        foreach (var map in source)
        {
            if (map == null || buffer.Contains(map))
            {
                continue;
            }

            buffer.Add(map);
        }
    }

    private void RegisterMap(MapAsset map)
    {
        // 핵심: 서버 MapName은 "MapAsset.name"을 쓰는 것으로 통일하자.
        // (예: MapAsset 파일 이름을 "Map"으로 만들면 서버에서 MapName="Map")
        var key = map.name;

        if (_byName.ContainsKey(key))
        {
            Debug.LogWarning($"[MapRegistry] Duplicate map name: {key} (last one wins)");
            _byName[key] = map;
        }
        else
        {
            _byName.Add(key, map);
        }
    }

    public bool TryGet(string mapName, out MapAsset map)
        => _byName.TryGetValue(mapName, out map);

    public MapAsset GetOrNull(string mapName)
        => _byName.TryGetValue(mapName, out var m) ? m : null;
}
