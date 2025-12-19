using GameServer.Content.Map;
using System;
using System.Collections.Generic;

public static class MapDatabase
{
    private static Dictionary<string, MapContent> _maps = new();
    private static Dictionary<string, Map2D> _contentMap = new();
    public static void LoadFrom(Dictionary<string, MapContent> maps)
    {
        _contentMap.Clear();
        foreach (var m in maps)
        {
            MapContent mapSet = m.Value;
            Map2D map = new Map2D(width: mapSet.Width, height: mapSet.Height);

            int Length = mapSet.Width * mapSet.Height;
            Console.WriteLine($"[CreateMap] Length : {Length} || Height : {map.Height} || Width : {map.Width}");
            for (int y = 0; y < mapSet.Height; y++)
            {
                for (int x = 0; x < mapSet.Width; x++)
                {
                    if (mapSet.Kind[y * mapSet.Width + x] == TileKind.Spawn)
                        map.SetSpawnPoint(x, y);

                    map.Set(x, y, mapSet.Kind[y * mapSet.Width + x]);
                    Console.Write((int)mapSet.Kind[y * mapSet.Width + x]);
                }
                Console.WriteLine();
            }
            _contentMap[m.Key] = map;
        }


        _maps = maps;
    }

    public static Map2D Get(string mapId)
        => _contentMap[mapId];

    public static bool TryGet(string mapId, out Map2D map)
        => _contentMap.TryGetValue(mapId, out map!);
}
