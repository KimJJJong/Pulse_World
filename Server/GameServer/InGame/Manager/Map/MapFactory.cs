namespace GameServer.InGame.Manager.Map;
public static class MapFactory
{
    public static Map2D CreatePrototypeMap()
    {
        var map = new Map2D(width: 16, height: 3);

        // 기본은 None (맵 밖/사용 안 함)
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                map.Set(x, y, TileKind.None);
            }
        }

        // 1번째 줄: [ ][ ][ ][ ][0][0][0][0][0][ ][ ][ ][ ][ ][ ][ ]
        int y0 = 0;

        for(int i =0 ; i< map.Width ; i++)
            map.Set(i, y0, TileKind.Floor);
        //map.Set(5, y0, TileKind.Floor);
        //map.Set(6, y0, TileKind.Floor);
        //map.Set(7, y0, TileKind.Floor);
        //map.Set(8, y0, TileKind.Floor);

        // 2번째 줄: [ ][ ][ ][ ][ ][ ][0][0][0][0][ ][ ][ ][ ][ ][ ]
        int y1 = 1;
        for (int i = 0; i < map.Width; i++)
            map.Set(i, y1, TileKind.Floor);
        //map.Set(6, y1, TileKind.Floor);
        //map.Set(7, y1, TileKind.Floor);
        //map.Set(8, y1, TileKind.Floor);
        //map.Set(9, y1, TileKind.Floor);

        // 3번째 줄은 전부 None (아무것도 안 씀)
        int y2 = 2;
        for (int i = 0; i < map.Width; i++)
            map.Set(i, y2, TileKind.Floor);
        return map;
    }
}
