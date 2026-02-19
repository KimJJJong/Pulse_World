using System;
using System.Collections.Generic;

namespace NetClient.Network.Http.Dtos
{
    [Serializable]
    public class InventoryResponse
    {
        public List<GameItemDto> items;
        public List<GameItemDto> equipments;
    }

    [Serializable]
    public class GameItemDto
    {
        public long id;
        public string ownerUid;
        public int templateId;
        public int amount;
        public int slotIndex;
        public int enhancementLevel;
        public bool isEquipped;
        public string baseStats;     // JSON string
        public string randomOptions; // JSON string
        public string acquiredAt;
    }
}
