using System;
using System.Collections.Generic;

namespace GameServer.InGame.Manager.Entity
{
    public class EntityIdGenerator
    {
        // Define Ranges
        private const int PLAYER_START = 0;
        private const int PLAYER_END = 99;

        private const int MONSTER_START = 100;
        private const int MONSTER_END = 999;
        
        // 1000~1999 Pet (Reserved, not used yet)

        private const int PROJECTILE_START = 2000;
        private const int PROJECTILE_END = 2999;

        private const int OBJECT_START = 3000;
        private const int OBJECT_END = 3999;

        // Active Pools (Queue for simple reuse, or just counter if we don't care about fragmentation order)
        // Ideally: Keep a "Next Id" counter. If released, push to "Free Stack".
        // Priority: Free Stack > Next Id.
        
        private class RangePool
        {
            public int Start;
            public int End;
            public int Current;
            public Stack<int> FreeIds = new Stack<int>();

            public RangePool(int start, int end)
            {
                Start = start;
                End = end;
                Current = start;
            }

            public int Rent()
            {
                if (FreeIds.Count > 0)
                    return FreeIds.Pop();

                if (Current > End)
                {
                    // Exhausted
                    throw new Exception($"Entity ID Range Exhausted! Range: {Start}-{End}");
                }

                return Current++;
            }

            public void Release(int id)
            {
                // Simple validation
                if (id < Start || id > End)
                    return; // Not my range

                // Prevent double release? (Scanning stack is slow)
                // For now trust the caller or use HashSet if critical.
                FreeIds.Push(id); 
            }
        }

        private RangePool _playerPool = new RangePool(PLAYER_START, PLAYER_END);
        private RangePool _monsterPool = new RangePool(MONSTER_START, MONSTER_END);
        private RangePool _objectPool = new RangePool(OBJECT_START, OBJECT_END);
        private RangePool _projectilePool = new RangePool(PROJECTILE_START, PROJECTILE_END); // If needed

        public int Generate(EntityType type)
        {
            switch (type)
            {
                case EntityType.Player: return _playerPool.Rent();
                case EntityType.Monster: return _monsterPool.Rent();
                case EntityType.Object: return _objectPool.Rent();
                default: throw new Exception($"Unknown Entity Type for ID Gen: {type}");
            }
        }

        public void Release(int id)
        {
            if (id >= PLAYER_START && id <= PLAYER_END) _playerPool.Release(id);
            else if (id >= MONSTER_START && id <= MONSTER_END) _monsterPool.Release(id);
            else if (id >= OBJECT_START && id <= OBJECT_END) _objectPool.Release(id);
            else if (id >= PROJECTILE_START && id <= PROJECTILE_END) _projectilePool.Release(id);
            // else ignore
        }
    }
}
