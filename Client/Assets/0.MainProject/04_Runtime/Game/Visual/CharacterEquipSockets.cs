using UnityEngine;

namespace RhythmRPG.Visual
{
    public class CharacterEquipSockets : MonoBehaviour
    {
        [Header("Weapon Sockets")]
        public Transform RightHandSocket;
        public Transform LeftHandSocket;
        public Transform BackSocket;     // For sheathed weapons

        [Header("Equipment Sockets")]
        public Transform HeadSocket;
        public Transform BodySocket;
        public Transform PantsSocket;
        public Transform GlovesSocket;
        public Transform ShoesSocket;
        public Transform AccessorySocket;
        
        // Helper to get socket by type (simplified)
        public Transform GetSocket(string slotName)
        {
            switch (slotName)
            {
                case "Weapon": return RightHandSocket;
                case "OffHand": return LeftHandSocket;
                case "Hat":
                case "Head": return HeadSocket;
                case "Body": return BodySocket;
                case "Pants": 
                case "Legs": return PantsSocket;
                case "Gloves": return GlovesSocket;
                case "Shoes": 
                case "Boots": return ShoesSocket;
                case "Accessory": return AccessorySocket != null ? AccessorySocket : HeadSocket;
                case "Back": return BackSocket;
                default: return RightHandSocket;
            }
        }
    }
}
