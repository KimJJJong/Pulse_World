using UnityEngine;
using System.Collections.Generic;
using RhythmRPG.Managers;
using RhythmRPG.Data;

namespace RhythmRPG.Visual
{
    public enum CharacterContext
    {
        Home,   // Lobby: Show all fancy equipment
        Town,   // Town: Show armor/costume, but hide weapons (or put on back)
        Game    // Game: Show full battle gear
    }

    [RequireComponent(typeof(CharacterEquipSockets))]
    public class CharacterVisualController : MonoBehaviour
    {
        private CharacterEquipSockets _sockets;
        private CharacterContext _currentContext = CharacterContext.Game;
        
        // Currently equipped runtime objects
        private Dictionary<int, GameObject> _spawnedEquipments = new Dictionary<int, GameObject>();

        private void Awake()
        {
            _sockets = GetComponent<CharacterEquipSockets>();
        }

        public void SetContext(CharacterContext context)
        {
            _currentContext = context;
            RefreshVisuals();
        }

        /// <summary>
        /// Call this when inventory changes or initializing character.
        /// </summary>
        /// <param name="equipmentIds">List of Equipment IDs currently equipped</param>
        public void UpdateEquipments(List<int> equipmentIds)
        {
            // Clear current visuals
            ClearEquipments();

            if (equipmentIds == null) return;

            foreach (var id in equipmentIds)
            {
                // Skip if invalid ID
                if (id <= 0) continue;

                // Load Prefab
                GameObject prefab = GameResourceManager.Instance.GetPrefab(id);
                if (prefab == null) continue;

                // Determine Socket
                // TODO: Need a way to know WHICH socket from ID.
                // For now, simple rule based on ID range.
                Transform targetSocket = GetTargetSocket(id);
                
                if (targetSocket != null)
                {
                    var instance = Instantiate(prefab, targetSocket);
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    
                    _spawnedEquipments[id] = instance;
                }
            }

            RefreshVisuals();
        }

        private Transform GetTargetSocket(int id)
        {
            if (EntityIdDefine.IsWeapon(id)) return _sockets.RightHandSocket;
            
            if (EntityIdDefine.IsHead(id)) return _sockets.HeadSocket;
            if (EntityIdDefine.IsBody(id)) return _sockets.BodySocket;
            if (EntityIdDefine.IsPants(id)) return _sockets.PantsSocket;
            if (EntityIdDefine.IsGloves(id)) return _sockets.GlovesSocket;
            if (EntityIdDefine.IsShoes(id)) return _sockets.ShoesSocket;

            return null;
        }

        private void ClearEquipments()
        {
            foreach (var kv in _spawnedEquipments)
            {
                if (kv.Value != null) Destroy(kv.Value);
            }
            _spawnedEquipments.Clear();
        }

        private void RefreshVisuals()
        {
            foreach (var kv in _spawnedEquipments)
            {
                int id = kv.Key;
                GameObject obj = kv.Value;
                
                if (obj == null) continue;

                if (EntityIdDefine.IsWeapon(id))
                {
                    // Town: Hide weapons or move to back
                    if (_currentContext == CharacterContext.Town)
                    {
                        obj.SetActive(false); // Or move to BackSocket if implemented
                    }
                    else
                    {
                        obj.SetActive(true);
                    }
                }
                else
                {
                    // Armor/Accessory: Always show?
                    obj.SetActive(true);
                }
            }
        }
    }
}
