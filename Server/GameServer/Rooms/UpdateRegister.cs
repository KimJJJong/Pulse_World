//using System.Collections.Generic;
//using System.Linq;

//public static class UpdateRegistry
//{
//    private static readonly object _lock = new();

//    private static readonly HashSet<IUpdatable> _game = new();
//    private static readonly HashSet<IUpdatable> _town = new();

//    public static void RegisterGame(IUpdatable u)
//    {
//        lock (_lock) _game.Add(u);
//    }

//    public static void UnregisterGame(IUpdatable u)
//    {
//        lock (_lock) _game.Remove(u);
//    }

//    public static void RegisterTown(IUpdatable u)
//    {
//        lock (_lock) _town.Add(u);
//    }

//    public static void UnregisterTown(IUpdatable u)
//    {
//        lock (_lock) _town.Remove(u);
//    }

//    public static List<IUpdatable> GetGameSnapshot()
//    {
//        lock (_lock) return _game.ToList();
//    }

//    public static List<IUpdatable> GetTownSnapshot()
//    {
//        lock (_lock) return _town.ToList();
//    }
//}
