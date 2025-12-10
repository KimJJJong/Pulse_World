public static class TileConverter
{
    public const int Width = 8;
    public const int Height = 12;

    // -> Cache
    //public static (int x, int y) ClientToServer(int sessionId, int clientX, int clientY, int player0)
    //{
    //    bool isPlayer0 = (sessionId == player0);
    //    return isPlayer0
    //        ? (clientX, clientY)
    //        : ((Width - 1) - clientX, (Height - 1) - clientY);
    //}

    //public static (int x, int y) ServerToClient(int sessionId, int serverX, int serverY, int player0)
    //{
    //    bool isPlayer0 = (sessionId == player0);
    //    return isPlayer0
    //        ? (serverX, serverY)
    //        : ((Width - 1) - serverX, (Height - 1) - serverY);
    //}
}

public static class PositionConverter
{
    public const float Width = 8;
    public const float Height = 12;

    public static (float x, float y) ClientToServer(int sessionId, float clientX, float clientY, int player0)
    {
        bool isPlayer0 = sessionId == player0;
        return isPlayer0
            ? (clientX, clientY)
            : ((Width - 1) - clientX, (Height - 1) - clientY);
    }

    public static (float x, float y) ServerToClient(int targetSession, float serverX, float serverY, int player0)
    {
        bool isPlayer0 = targetSession == player0;
        return isPlayer0
            ? (serverX, serverY)
            : ((Width - 1) - serverX, (Height - 1) - serverY);
    }
}
