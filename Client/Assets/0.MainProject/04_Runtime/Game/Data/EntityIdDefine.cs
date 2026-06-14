using UnityEngine;

namespace RhythmRPG.Data
{
    public static class EntityIdDefine
    {
        // 0 ~ 9: Reserved / System
        public const int SYSTEM_MIN = 0;
        public const int SYSTEM_MAX = 9;

        // 10 ~ 999: Player (Playable Characters/Skins)
        public const int PLAYER_MIN = 10;
        public const int PLAYER_MAX = 999;

        // 1,000 ~ 99,999: Monster (Normal, Elite, Boss)
        public const int MONSTER_MIN = 1000;
        public const int MONSTER_MAX = 99999;

        // 100,000 ~ 199,999: Weapon
        public const int WEAPON_MIN = 100000;
        public const int WEAPON_MAX = 199999;

        // 200,000 ~ 299,999: Worn gear
        // 200,000 ~ 209,999: Hat
        // 210,000 ~ 219,999: Body (Armor)
        // 220,000 ~ 229,999: Pants (Legs)
        // 230,000 ~ 239,999: Gloves
        // 240,000 ~ 249,999: Shoes
        // 250,000 ~ 299,999: Etc/Expansion
        public const int HAT_MIN = 200000;
        public const int BODY_MIN = 210000;
        public const int PANTS_MIN = 220000;
        public const int GLOVES_MIN = 230000;
        public const int SHOES_MIN = 240000;

        public const int ARMOR_MIN = 200000;
        public const int ARMOR_MAX = 299999; 

        // 300,000 ~ 399,999: Accessory (Rings, Necklaces, etc)
        public const int ACCESSORY_MIN = 300000;
        public const int ACCESSORY_MAX = 399999;

        // 400,000 ~ 499,999: Consumable
        public const int CONSUMABLE_MIN = 400000;
        public const int CONSUMABLE_MAX = 499999;

        // 500,000 ~ : Material / Etc
        public const int MATERIAL_MIN = 500000;

        public static bool IsPlayer(int id) => id >= PLAYER_MIN && id <= PLAYER_MAX;
        public static bool IsMonster(int id) => id >= MONSTER_MIN && id <= MONSTER_MAX;
        public static bool IsWeapon(int id) => id >= WEAPON_MIN && id <= WEAPON_MAX;
        public static bool IsArmor(int id) => id >= ARMOR_MIN && id <= ARMOR_MAX;


        public static bool IsHat(int id) => id >= HAT_MIN && id < BODY_MIN;
        public static bool IsHead(int id) => IsHat(id);
        public static bool IsBody(int id) => id >= BODY_MIN && id < PANTS_MIN;
        public static bool IsPants(int id) => id >= PANTS_MIN && id < GLOVES_MIN;
        public static bool IsGloves(int id) => id >= GLOVES_MIN && id < SHOES_MIN;
        public static bool IsShoes(int id) => id >= SHOES_MIN && id <= 249999;
        
        public static bool IsAccessory(int id) => id >= ACCESSORY_MIN && id <= ACCESSORY_MAX;
        public static bool IsConsumable(int id) => id >= CONSUMABLE_MIN && id <= CONSUMABLE_MAX;
        public static bool IsMaterial(int id) => id >= MATERIAL_MIN;

        /// <summary>
        /// ID를 기반으로 리소스 로드 경로(폴더명)를 반환합니다.
        /// 예: 10 -> "Entities/Player"
        /// </summary>
        public static string GetResourceFolderPath(int id)
        {
            if (IsPlayer(id)) return "Entities/Player";
            if (IsMonster(id)) return "Entities/Monster";
            if (IsWeapon(id)) return "Entities/Weapon";
            if (IsAccessory(id)) return "Entities/Accessory";
            if (IsArmor(id)) return "Entities/Armor";
            if (IsConsumable(id)) return "Entities/Consumable";
            if (IsMaterial(id)) return "Entities/Material";
            
            return "Entities/Etc";
        }
    }
}
