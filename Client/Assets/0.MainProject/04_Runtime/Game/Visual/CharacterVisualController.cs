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
        
        private Dictionary<int, GameObject> _spawnedEquipments = new Dictionary<int, GameObject>();
        private bool _isLocalPlayer = false;

        public bool IsLocalPlayer => _isLocalPlayer;
        public CharacterContext CurrentContext => _currentContext;

        private void Awake()
        {
            _sockets = GetComponent<CharacterEquipSockets>();
        }

        private void Start()
        {
            // Start()에서 한 번 더 시도: Awake/OnEnable 시점에 InventoryManager가 아직 없었을 수 있음
            if (_isLocalPlayer)
            {
                SubscribeAndRefresh("Start");
            }
        }

        private void OnEnable()
        {
            if (_isLocalPlayer)
            {
                SubscribeAndRefresh("OnEnable");
            }
        }

        private void OnDisable()
        {
            var inventoryManager = InventoryManager.ExistingInstance;
            if (inventoryManager != null)
                inventoryManager.OnInventoryUpdated -= RefreshFromInventory;
        }

        /// <summary>
        /// InventoryManager 구독 + 현재 데이터가 이미 있으면 즉시 갱신.
        /// InventoryManager가 없을 경우 경고만 출력하고 나중에 이벤트로 받음.
        /// </summary>
        private void SubscribeAndRefresh(string caller)
        {
            if (InventoryManager.Instance == null)
            {
                Debug.LogWarning($"[CharacterVisualController] ({caller}) InventoryManager.Instance is NULL on '{gameObject.name}'. Will catch OnInventoryUpdated when it fires.");
                return;
            }

            // 중복 구독 방지
            InventoryManager.Instance.OnInventoryUpdated -= RefreshFromInventory;
            InventoryManager.Instance.OnInventoryUpdated += RefreshFromInventory;

            int count = InventoryManager.Instance.Equipments?.Count ?? 0;
            Debug.Log($"[CharacterVisualController] ({caller}) Subscribed on '{gameObject.name}'. InventoryManager equip count={count}");

            // 이미 로드된 데이터가 있으면 즉시 반영
            if (count > 0)
            {
                Debug.Log($"[CharacterVisualController] ({caller}) Inventory already loaded — refreshing immediately.");
                RefreshFromInventory();
            }
        }

        public void SetLocalPlayer(bool isLocal)
        {
            _isLocalPlayer = isLocal;
            Debug.Log($"[CharacterVisualController] SetLocalPlayer({isLocal}) on '{gameObject.name}'");

            if (_isLocalPlayer)
            {
                SubscribeAndRefresh("SetLocalPlayer");
            }
            else
            {
                var inventoryManager = InventoryManager.ExistingInstance;
                if (inventoryManager != null)
                    inventoryManager.OnInventoryUpdated -= RefreshFromInventory;
                ClearEquipments();
            }
        }

        public void RefreshLocalPlayerEquipmentNow()
        {
            if (!_isLocalPlayer)
            {
                SetLocalPlayer(true);
                return;
            }

            RefreshFromInventory();
        }

        private void RefreshFromInventory()
        {
            if (!_isLocalPlayer) return;
            if (InventoryManager.Instance == null)
            {
                Debug.LogWarning("[CharacterVisualController] RefreshFromInventory: InventoryManager is null.");
                return;
            }
            
            var myEquips = InventoryManager.Instance.Equipments;
            List<int> equippedTemplateIds = new List<int>();
            foreach (var e in myEquips)
            {
                if (e.IsEquipped) equippedTemplateIds.Add(e.TemplateId);
            }
            
            Debug.Log($"[CharacterVisualController] RefreshFromInventory on '{gameObject.name}'. " +
                      $"Total equips: {myEquips?.Count ?? 0}, IsEquipped: {equippedTemplateIds.Count}");

            foreach (var e in myEquips)
            {
                Debug.Log($"[CharacterVisualController]   InstanceId:{e.InstanceId} TemplateId:{e.TemplateId} IsEquipped:{e.IsEquipped}");
            }

            UpdateEquipments(equippedTemplateIds);
        }

        public void SetContext(CharacterContext context)
        {
            _currentContext = context;
            RefreshVisuals();
        }

        public void UpdateEquipments(List<int> equipmentIds)
        {
            ClearEquipments();

            if (equipmentIds == null || equipmentIds.Count == 0)
            {
                Debug.Log($"[CharacterVisualController] No equipped items on '{gameObject.name}'.");
                return;
            }

            Debug.Log($"[CharacterVisualController] Attaching {equipmentIds.Count} equipment(s) on '{gameObject.name}'.");

            foreach (var id in equipmentIds)
            {
                if (id <= 0)
                {
                    Debug.LogWarning($"[CharacterVisualController] Invalid TemplateId={id}, skipping.");
                    continue;
                }

                Debug.Log($"[CharacterVisualController] Loading prefab for TemplateId={id}");
                GameObject prefab = GameResourceManager.Instance.GetPrefab(id);
                if (prefab == null)
                {
                    Debug.LogWarning($"[CharacterVisualController] ❌ Prefab not found for TemplateId={id}. " +
                                     $"Check Equipment.json model_path and Resources folder structure.");
                    continue;
                }

                Transform targetSocket = GetTargetSocket(id);
                
                if (targetSocket != null)
                {
                    var instance = Instantiate(prefab, targetSocket);
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    _spawnedEquipments[id] = instance;
                    Debug.Log($"[CharacterVisualController] ✅ TemplateId={id} → socket='{targetSocket.name}' on '{gameObject.name}'.");
                }
                else
                {
                    Debug.LogWarning($"[CharacterVisualController] ❌ No socket for TemplateId={id} on '{gameObject.name}'. " +
                                     $"Check CharacterEquipSockets Inspector.");
                }
            }

            RefreshVisuals();
        }

        private Transform GetTargetSocket(int id)
        {
            // 1순위: ItemDataManager SlotEnum
            if (Client.Content.Item.ItemDataManager.Instance != null)
            {
                var tmpl = Client.Content.Item.ItemDataManager.Instance.GetEquipment(id);
                if (tmpl != null)
                {
                    var slot = tmpl.SlotEnum;
                    Debug.Log($"[CharacterVisualController] GetTargetSocket id={id} equip_slot='{tmpl.equip_slot}' SlotEnum={slot}");

                    if (slot != Client.Content.Item.EquipmentSlot.None)
                    {
                        Transform socket = _sockets.GetSocket(slot.ToString());
                        if (socket != null) return socket;
                        Debug.LogWarning($"[CharacterVisualController] Socket '{slot}' is null in CharacterEquipSockets. " +
                                         $"Assign the Transform in Inspector on '{gameObject.name}'.");
                    }
                    else
                    {
                        Debug.LogWarning($"[CharacterVisualController] TemplateId={id} SlotEnum=None (raw='{tmpl.equip_slot}'). " +
                                         $"Check equip_slot value in Equipment.json.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[CharacterVisualController] GetEquipment({id}) is null. " +
                                     $"Check ItemDataManager loaded and Equipment.json has this ID.");
                }
            }
            else
            {
                Debug.LogWarning($"[CharacterVisualController] ItemDataManager.Instance is null. Cannot resolve slot for id={id}.");
            }

            // 2순위: EntityIdDefine 범위 폴백
            if (EntityIdDefine.IsWeapon(id))   return _sockets.RightHandSocket;
            if (EntityIdDefine.IsHead(id))     return _sockets.HeadSocket;
            if (EntityIdDefine.IsBody(id))     return _sockets.BodySocket;
            if (EntityIdDefine.IsPants(id))    return _sockets.PantsSocket;
            if (EntityIdDefine.IsGloves(id))   return _sockets.GlovesSocket;
            if (EntityIdDefine.IsShoes(id))    return _sockets.ShoesSocket;

            Debug.LogWarning($"[CharacterVisualController] No socket resolved for id={id} via any method.");
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
                    obj.SetActive(_currentContext != CharacterContext.Town);
                else
                    obj.SetActive(true);
            }
        }
    }
}
