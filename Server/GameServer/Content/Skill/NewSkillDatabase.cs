using System.Collections.Generic;
using GameShared.Data;

namespace GameServer.Content.Skill
{
    public static class NewSkillDatabase
    {
        private static readonly Dictionary<string, NewSkillDef> _map = new();

        public static void LoadFrom(List<NewSkillDef> skills)
        {
            _map.Clear();
            foreach (var s in skills)
            {
                if (s != null && !string.IsNullOrEmpty(s.SkillId))
                    _map[s.SkillId] = s;
            }
        }

        public static bool TryGet(string skillId, out NewSkillDef def)
        {
            return _map.TryGetValue(skillId, out def);
        }

        // Test Stub
        public static void Add(NewSkillDef def)
        {
            if (def != null) _map[def.SkillId] = def;
        }
    }
}
