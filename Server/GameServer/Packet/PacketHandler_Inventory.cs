using Server;
using ServerCore;
using System;
using System.Threading.Tasks;
using GameServer.Content.Item;

partial class PacketHandler
{
    public static void CS_GetInventoryHandler(PacketSession session, IPacket packet)
    {
        ClientSession s = (ClientSession)session;
        CS_GetInventory req = (CS_GetInventory)packet;
        
        Console.WriteLine($"[CS_GetInventory] ActorId: {s.ActorId}");

        // Load and Send Inventory
        Task.Run(async () =>
        {
            try 
            {
                // Use Authenticated Uid, not SessionID
                string uid = s.Uid;
                if (string.IsNullOrEmpty(uid)) 
                {
                    Console.WriteLine("[CS_GetInventory] No Auth Uid");
                    return;
                }
                var invItems = await ServerServices.InventoryManager.LoadInventoryAsync(uid);
                
                var invPkt = new SC_Inventory();
                foreach(var item in invItems)
                {
                    var equipTmpl = ServerServices.ItemTemplates.GetEquipment(item.TemplateId);
                    if (equipTmpl != null)
                    {
                        invPkt.equipmentss.Add(new SC_Inventory.Equipments
                        {
                            InstanceId = item.InstanceId,
                            TemplateId = item.TemplateId,
                            SlotIndex = item.SlotIndex,
                            EnhancementLevel = item.EnhancementLevel,
                            IsEquipped = item.IsEquipped,
                            BaseStats = Newtonsoft.Json.JsonConvert.SerializeObject(item.BaseStats),
                            RandomOptions = Newtonsoft.Json.JsonConvert.SerializeObject(item.RandomOptions)
                        });
                    }
                    else
                    {
                        invPkt.itemss.Add(new SC_Inventory.Items
                        {
                            InstanceId = item.InstanceId,
                            TemplateId = item.TemplateId,
                            Amount = item.Amount,
                            SlotIndex = item.SlotIndex
                        });
                    }
                }
                s.Send(invPkt.Write());
                Console.WriteLine($"[CS_GetInventory] Sent {invPkt.itemss.Count} Items, {invPkt.equipmentss.Count} Equipments to {s.ActorId}");
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[CS_GetInventory] Error: {ex.Message}");
            }
        });
    }

    public static void CS_EquipItemHandler(PacketSession session, IPacket packet)
    {
        ClientSession s = (ClientSession)session;
        CS_EquipItem req = (CS_EquipItem)packet;

        Console.WriteLine($"[CS_EquipItem] ActorId: {s.ActorId} InstanceId: {req.InstanceId} Equip: {req.Equip}");

        // For now, logic is simple: Update Memory & DB
        // Validation needed (Ownership chk)

        Task.Run(async () =>
        {
            try
            {
                // Use Authenticated Uid, not SessionID
                string uid = s.Uid;
                if (string.IsNullOrEmpty(uid)) 
                {
                    Console.WriteLine("[CS_EquipItem] No Auth Uid");
                    return;
                }

                // 1. Load current (Optimized: Should cache in Session or InventoryManager?)
                // Current implementation loads from Redis every time.
                var items = await ServerServices.InventoryManager.LoadInventoryAsync(uid);
                var target = items.Find(x => x.InstanceId == req.InstanceId);
                
                bool success = false;
                if (target != null)
                {
                    // Update State
                    target.IsEquipped = req.Equip;

                    // TODO: Un-equip other items in same slot if needed? -> Later
                    
                    // Save back
                    await ServerServices.InventoryManager.SaveInventoryAsync(uid, items);
                    success = true;
                }

                var res = new SC_EquipResult
                {
                    Success = success,
                    InstanceId = req.InstanceId,
                    Equipped = req.Equip
                };
                s.Send(res.Write());
                Console.WriteLine($"[CS_EquipItem] Result: {success}");
            }
            catch(Exception ex)
            {
                 Console.WriteLine($"[CS_EquipItem] Error: {ex.Message}");
            }
        });
    }
    public static void CS_DestroyItemHandler(PacketSession session, IPacket packet)
    {
        ClientSession s = (ClientSession)session;
        CS_DestroyItem req = (CS_DestroyItem)packet;

        Console.WriteLine($"[CS_DestroyItem] ActorId: {s.ActorId} InstanceId: {req.InstanceId} Amount: {req.Amount}");

        Task.Run(async () =>
        {
            try
            {
                string uid = s.Uid;
                if (string.IsNullOrEmpty(uid)) return;

                var items = await ServerServices.InventoryManager.LoadInventoryAsync(uid);
                var target = items.Find(x => x.InstanceId == req.InstanceId);
                
                if (target != null)
                {
                    if (req.Amount >= target.Amount)
                    {
                        items.Remove(target);
                        Console.WriteLine($"[Destroy] Removed Item {target.InstanceId}");
                    }
                    else
                    {
                        target.Amount -= req.Amount;
                        Console.WriteLine($"[Destroy] Decreased Item {target.InstanceId} by {req.Amount} -> {target.Amount}");
                    }
                    
                    await ServerServices.InventoryManager.SaveInventoryAsync(uid, items);
                    
                    // Send Refresh
                     var reloadedItems = await ServerServices.InventoryManager.LoadInventoryAsync(uid);
                     var invPkt = new SC_Inventory();
                     foreach(var item in reloadedItems)
                     {
                        var equipTmpl = ServerServices.ItemTemplates.GetEquipment(item.TemplateId);
                        if (equipTmpl != null)
                        {
                            invPkt.equipmentss.Add(new SC_Inventory.Equipments
                            {
                                InstanceId = item.InstanceId,
                                TemplateId = item.TemplateId,
                                SlotIndex = item.SlotIndex,
                                EnhancementLevel = item.EnhancementLevel,
                                IsEquipped = item.IsEquipped,
                                BaseStats = Newtonsoft.Json.JsonConvert.SerializeObject(item.BaseStats),
                                RandomOptions = Newtonsoft.Json.JsonConvert.SerializeObject(item.RandomOptions)
                            });
                        }
                        else
                        {
                            invPkt.itemss.Add(new SC_Inventory.Items
                            {
                                InstanceId = item.InstanceId,
                                TemplateId = item.TemplateId,
                                Amount = item.Amount,
                                SlotIndex = item.SlotIndex
                            });
                        }
                     }
                     s.Send(invPkt.Write());
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[CS_DestroyItem] Error: {ex.Message}");
            }
        });
    }
}
