using Server;
using ServerCore;
using System;
using System.Threading.Tasks;
using GameServer.Content.Item;
using System.Collections.Generic;

partial class PacketHandler
{
    public static void CS_CheatHandler(PacketSession session, IPacket packet)
    {
        ClientSession s = (ClientSession)session;
        CS_Cheat req = (CS_Cheat)packet;

        Console.WriteLine($"[CS_Cheat] ActorId: {s.ActorId} Code: {req.CheatCode} P1:{req.Param1} P2:{req.Param2}");

        switch(req.CheatCode)
        {
            case 1: // Add Item
                HandleCheat_AddItem(s, req.Param1, req.Param2);
                break;
            default:
                Console.WriteLine($"[CS_Cheat] Unknown Code: {req.CheatCode}");
                break;
        }
    }

    private static void HandleCheat_AddItem(ClientSession s, int templateId, int amount)
    {
        Task.Run(async () =>
        {
            try 
            {
                // Use Authenticated Uid, not SessionID
                string uid = s.Uid;
                if (string.IsNullOrEmpty(uid)) 
                {
                    Console.WriteLine("[Cheat] No Auth Uid");
                    return;
                }

                // 1. Load
                var items = await ServerServices.InventoryManager.LoadInventoryAsync(uid);

                // 2. Logic
                var tmpl = ServerServices.ItemTemplates.Get(templateId);
                if (tmpl == null)
                {
                    Console.WriteLine($"[Cheat] Invalid TemplateId: {templateId}");
                    return;
                }

                int remaining = amount;

                // A. Fill existing stacks (Non-Equipment)
                if (tmpl.Type != ItemType.Equipment)
                {
                    var partials = items.FindAll(x => x.TemplateId == templateId && x.Amount < tmpl.MaxStack);
                    foreach (var p in partials)
                    {
                        if (remaining <= 0) break;
                        int space = tmpl.MaxStack - p.Amount;
                        int add = Math.Min(space, remaining);
                        p.Amount += add;
                        p.IsDirty = true;
                        remaining -= add;
                    }
                }

                // B. Create new stacks for remaining
                while (remaining > 0)
                {
                    int quantity = 0;
                    // Check type by class, because JSON might not have "type": "Equipment"
                    if (tmpl is EquipmentTemplate)
                        quantity = 1; 
                    else
                    {
                        // Safety: MaxStack might be 0 if not defined. Default to 1.
                        int stackLimit = tmpl.MaxStack > 0 ? tmpl.MaxStack : 1;
                        quantity = Math.Min(remaining, stackLimit);
                    }
                    
                    if (quantity <= 0) quantity = 1; // Double safety to prevent infinite loop

                    var newItem = new ItemInstance
                    {
                        InstanceId = 0, // DB will assign
                        TemplateId = templateId,
                        SlotIndex = FindEmptySlot(items), // Find next empty
                        Amount = quantity
                    };

                    if (tmpl is EquipmentTemplate equipTmpl)
                    {
                        newItem.EnhancementLevel = 0;
                        newItem.IsEquipped = false;
                        
                        // Populate BaseStats
                        if (equipTmpl.Atk != 0) newItem.BaseStats["Atk"] = equipTmpl.Atk;
                        if (equipTmpl.Def != 0) newItem.BaseStats["Def"] = equipTmpl.Def;
                        if (equipTmpl.Hp != 0) newItem.BaseStats["Hp"] = equipTmpl.Hp;
                        if (equipTmpl.Str != 0) newItem.BaseStats["Str"] = equipTmpl.Str;
                        if (equipTmpl.Dex != 0) newItem.BaseStats["Dex"] = equipTmpl.Dex;
                        
                        // Populate Test RandomOptions
                        newItem.RandomOptions["Str"] = new Random().Next(1, 10);
                        newItem.RandomOptions["Dex"] = new Random().Next(1, 10);
                    }

                    items.Add(newItem);
                    remaining -= quantity;
                }

                // 3. Save (This regenerates IDs in current Repo implementation)
                await ServerServices.InventoryManager.SaveInventoryAsync(uid, items);

                // 4. Reload to get new IDs
                var reloadedItems = await ServerServices.InventoryManager.LoadInventoryAsync(uid);

                // 5. Send SC_Inventory
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
                Console.WriteLine($"[Cheat] Added Item {templateId}x{amount}. Sent refreshed inventory.");

            }
            catch(Exception ex)
            {
                Console.WriteLine($"[Cheat] Error: {ex.Message}");
            }
        });
    }

    private static int FindEmptySlot(List<ItemInstance> items)
    {
        // Simple search
        for (int i = 0; i < 100; i++)
        {
            if (!items.Exists(x => x.SlotIndex == i)) return i;
        }
        return -1;
    }
}
