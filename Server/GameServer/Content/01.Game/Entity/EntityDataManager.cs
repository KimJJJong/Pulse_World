using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace GameServer.Content.Game.Entity
{
    public class EntityDataManager
    {
        public static EntityDataManager Instance { get; private set; } = new EntityDataManager();

        private Dictionary<int, EntityData> _entities = new Dictionary<int, EntityData>();

        public void Load()
        {
            // Path: Content/01.Game/Entity/Json/EntityData.json
            string relativePath = "Content/01.Game/Entity/Json/EntityData.json";
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            
            if (!File.Exists(path))
            {
                // Fallback debugging path
                path = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
            }
             
            if (!File.Exists(path))
            {
                // Try absolute path based on project structure if running from unexpected CWD
                // But let's verify existence first.
                // If not found, log error.
                Console.WriteLine($"[EntityDataManager] File not found: {path}");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var root = JsonConvert.DeserializeObject<EntityDataRoot>(json);
                if (root != null && root.Entities != null)
                {
                    _entities = root.Entities.ToDictionary(x => x.EntityId);
                    Console.WriteLine($"[EntityDataManager] Loaded {_entities.Count} entities from {path}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EntityDataManager] Load Error: {ex.Message}");
            }
        }

        public EntityData Get(int entityId)
        {
            if (_entities.TryGetValue(entityId, out var data))
                return data;
            return null;
        }
    }

    public class EntityDataRoot
    {
        public List<EntityData> Entities { get; set; }
    }

    public class EntityData
    {
        public int EntityId { get; set; }
        public string Name { get; set; }
        public int EntityType { get; set; }
        public int MaxHp { get; set; }
    }
}
