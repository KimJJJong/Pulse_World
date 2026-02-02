using UnityEngine;

namespace RhythmRPG.Editor.StageBuilder
{
    [CreateAssetMenu(fileName = "NewEntityDef", menuName = "RhythmRPG/Entity Definition", order = 2)]
    public class EntityDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int EntityId;        // Unique ID (e.g. 1001, 2001)
        public string EntityName;   // "Gate", "Chest"
        public EntityType Type;     // Monster, Object

        [Header("Visuals")]
        public GameObject Prefab;   // The model/visual to spawn on Client
        public RuntimeAnimatorController AnimatorController; // If needed separately

        [Header("Stats")]
        public int MaxHp = 10;
    }

    public enum EntityType
    {
        Player = 1,
        Monster = 2,
        Object = 3
    }
}
