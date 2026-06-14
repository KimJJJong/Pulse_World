#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Data;
using GameShared.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace RhythmRPG.Editor
{
    public static class BuildPlayerEquipmentBalance
    {
        private const string SkillAssetFolder = "Assets/Resources/Data/NewSkills";
        private const string ClientEquipmentJsonPath = "Assets/Resources/Data/Json/Equipments.json";
        private const string ClientEntityJsonPath = "Assets/Resources/Data/EntityData.json";
        private const string ClientStageJsonPath = "Assets/Resources/Data/Stage/Game_Forest_01.json";
        private const string ClientStageAssetPath = "Assets/Resources/Data/StageAssets/Game_Forest_01.asset";
        private const string ServerSkillJsonRelativePath = "../Server/GameServer/Content/01.Game/Skill/Json";
        private const string ServerEquipmentJsonRelativePath = "../Server/GameServer/Content/Data/Json/Equipments.json";
        private const string ServerEntityJsonRelativePath = "../Server/GameServer/Content/01.Game/Entity/Json/EntityData.json";
        private const string ServerForestStageJsonRelativePath = "../Server/GameServer/Content/01.Game/Stage/Json/Game_Forest_01.json";
        private const string ForestStageSongKey = "Game_Synthwave_01";
        private const int SubBeatTicks = 120;

        [MenuItem("RhythmRPG/Editors/Setup/Build Player Equipment Balance")]
        public static void Build()
        {
            EnsureFolder(SkillAssetFolder);

            var skills = new[]
            {
                BuildSwordAttack(),
                BuildNoviceSword(),
                BuildIronSwordAttack(),
                BuildIronSwordSkill(),
                BuildAxeAttack(),
                BuildNoviceAxe(),
                BuildBowAttack(),
                BuildBowSkill(),
                BuildDaggerAttack(),
                BuildNoviceDagger(),
                BuildHelmetSkill(),
                BuildArmorSkill(),
                BuildMoveSkill(),
                BuildBackstepSkill()
            };

            foreach (var skill in skills)
                SaveAndExport(skill);

            WriteEquipmentJson();
            WriteEntityJson(ClientEntityJsonPath);
            WriteEntityJson(GetServerPath(ServerEntityJsonRelativePath));
            WriteForestStageSongKey();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BuildPlayerEquipmentBalance] Built {skills.Length} player/equipment skills, equipment tables, stage rhythm key, and entity combat stats.");
        }

        private static NewSkillSO BuildSwordAttack()
        {
            var cells = Cells((0, -1));
            return Skill(
                "SwordAttack",
                480,
                Track("Telegraph", Warning(0, 240, cells)),
                Track("Impact", Damage(240, 240, cells, 8)),
                Track("Control", InputLock(0, 480)),
                RhythmSound("Greatsword", Hit(0, 0.8f), Hit(240), Hit(360, 0.7f)));
        }

        private static NewSkillSO BuildNoviceSword()
        {
            var cells = Cells((-1, -1), (0, -1), (1, -1), (0, -2));
            return Skill(
                "NoviceSword",
                960,
                Track("Telegraph", Warning(0, 480, cells)),
                Track("Impact", Damage(480, 240, cells, 16)),
                Track("Control", InputLock(0, 960)),
                RhythmSound(
                    "Greatsword",
                    Hit(0, 0.75f), Hit(240, 0.9f), Hit(360, 0.65f),
                    Hit(480, 0.95f), Hit(720), Hit(840, 0.75f)));
        }

        private static NewSkillSO BuildIronSwordAttack()
        {
            var cells = Cells((0, -1), (0, -2));
            return Skill(
                "IronSwordAttack",
                720,
                Track("Telegraph", Warning(0, 360, cells)),
                Track("Impact", Damage(360, 240, cells, 10)),
                Track("Control", InputLock(0, 720)),
                RhythmSound("Greatsword", Hit(0, 0.8f), Hit(240, 0.9f), Hit(360), Hit(600, 0.75f)));
        }

        private static NewSkillSO BuildIronSwordSkill()
        {
            var cells = Cells((-1, -1), (0, -1), (1, -1), (-1, -2), (0, -2), (1, -2));
            return Skill(
                "IronSwordSkill",
                1440,
                Track("Telegraph", Warning(0, 720, cells)),
                Track("Impact", Damage(720, 240, cells, 20, knockback: 1)),
                Track("Control", InputLock(0, 1440)),
                RhythmSound(
                    "Greatsword",
                    Hit(0, 0.7f), Hit(240, 0.85f), Hit(360, 0.65f),
                    Hit(480, 0.85f), Hit(720), Hit(840, 0.75f),
                    Hit(960, 0.9f), Hit(1200), Hit(1320, 0.85f)));
        }

        private static NewSkillSO BuildAxeAttack()
        {
            var cells = Cells((-1, -1), (0, -1), (1, -1));
            return Skill(
                "AxeAttack",
                960,
                Track("Telegraph", Warning(0, 480, cells)),
                Track("Impact", Damage(480, 240, cells, 14)),
                Track("Control", InputLock(0, 960)),
                RhythmSound("Greatsword", Hit(0, 0.75f), Hit(360, 0.95f), Hit(480), Hit(600, 0.7f), Hit(840, 0.9f)));
        }

        private static NewSkillSO BuildNoviceAxe()
        {
            var cells = Cells((-1, -1), (0, -1), (1, -1), (-1, -2), (0, -2), (1, -2));
            return Skill(
                "NoviceAxe",
                1440,
                Track("Telegraph", Warning(0, 720, cells)),
                Track("Impact", Damage(720, 240, cells, 26, knockback: 1)),
                Track("Control", InputLock(0, 1440)),
                RhythmSound(
                    "Greatsword",
                    Hit(0, 0.7f), Hit(360, 0.9f),
                    Hit(480, 0.85f), Hit(720), Hit(840, 0.75f),
                    Hit(960), Hit(1080, 0.7f), Hit(1320, 0.95f)));
        }

        private static NewSkillSO BuildBowAttack()
        {
            var cells = Line(6);
            return Skill(
                "BowAttack",
                960,
                Track("Telegraph", Warning(0, 480, cells)),
                Track("Impact", Damage(480, 240, cells, 7)),
                Track("Control", InputLock(0, 960)),
                RhythmSound(
                    "Bow",
                    Hit(0, 0.7f), Hit(120, 0.8f), Hit(240, 0.9f), Hit(360),
                    Hit(480, 0.8f), Hit(600, 0.9f), Hit(720, 0.75f)));
        }

        private static NewSkillSO BuildBowSkill()
        {
            var cells = WideLine(5);
            return Skill(
                "BowSkill",
                1440,
                Track("Telegraph", Warning(0, 720, cells)),
                Track("Impact", Damage(720, 240, cells, 18)),
                Track("Control", InputLock(0, 1440)),
                RhythmSound(
                    "Bow",
                    Hit(0, 0.65f), Hit(120, 0.75f), Hit(240, 0.9f), Hit(360),
                    Hit(480, 0.7f), Hit(600, 0.85f), Hit(720), Hit(840, 0.8f),
                    Hit(960, 0.75f), Hit(1080, 0.9f), Hit(1200), Hit(1320, 0.85f)));
        }

        private static NewSkillSO BuildDaggerAttack()
        {
            var cells = Cells((0, -1));
            return Skill(
                "DaggerAttack",
                480,
                Track("Telegraph", Warning(0, 180, cells)),
                Track("Impact", Damage(180, 180, cells, 5)),
                Track("Control", InputLock(0, 420)),
                RhythmSound("Dagger", Hit(0, 0.75f), Hit(120), Hit(240, 0.9f), Hit(360, 0.7f)));
        }

        private static NewSkillSO BuildNoviceDagger()
        {
            var cells = Cells((0, -1), (0, -2));
            return Skill(
                "NoviceDagger",
                960,
                Track("Approach", Move(240, 240, MoveType.Dash, 2, 0, -1)),
                Track("Telegraph", Warning(240, 240, cells)),
                Track("Impact", Damage(480, 240, cells, 14, stun: 240)),
                Track("Control", InputLock(0, 960)),
                RhythmSound(
                    "Dagger",
                    Hit(0, 0.7f), Hit(120, 0.9f), Hit(240), Hit(360, 0.8f),
                    Hit(480), Hit(600, 0.85f), Hit(720, 0.95f), Hit(840, 0.75f)));
        }

        private static NewSkillSO BuildHelmetSkill()
        {
            return Skill(
                "HelmetSkill",
                1440,
                Track("Telegraph", Warning(0, 720, Diamond(2))),
                Track("Impact", Damage(720, 240, Diamond(2), 14, stun: 240)),
                Track("Control", InputLock(0, 1440)),
                RhythmSound(
                    "Parry",
                    Hit(0, 0.65f), Hit(240, 0.85f), Hit(360, 0.75f),
                    Hit(480, 0.8f), Hit(720), Hit(840, 0.75f),
                    Hit(960, 0.9f), Hit(1200), Hit(1320, 0.8f)));
        }

        private static NewSkillSO BuildArmorSkill()
        {
            var cells = Cells((0, -1), (-1, 0), (1, 0), (0, 1));
            return Skill(
                "ArmorSkill",
                960,
                Track("Telegraph", Warning(0, 480, cells)),
                Track("Impact", Damage(480, 240, cells, 10, knockback: 1)),
                Track("Control", InputLock(0, 960)),
                RhythmSound("Parry", Hit(0, 0.75f), Hit(240, 0.85f), Hit(360, 0.7f), Hit(480), Hit(720, 0.85f), Hit(840, 0.75f)));
        }

        private static NewSkillSO BuildMoveSkill()
        {
            return Skill(
                "MoveSkill",
                960,
                Track("Move", Move(240, 240, MoveType.Dash, 2, 0, -1)),
                Track("Control", InputLock(0, 720)),
                RhythmSound("Staff", Hit(0, 0.65f), Hit(120, 0.8f), Hit(240), Hit(480, 0.75f), Hit(600, 0.9f)));
        }

        private static NewSkillSO BuildBackstepSkill()
        {
            return Skill(
                "BackstepSkill",
                960,
                Track("Move", Move(240, 240, MoveType.Dash, 2, 0, 1)),
                Track("Control", InputLock(0, 720)),
                RhythmSound("Staff", Hit(0, 0.75f), Hit(120, 0.9f), Hit(360, 0.65f), Hit(480), Hit(600, 0.8f)));
        }

        private static NewSkillSO Skill(string skillId, int durationTicks, params SkillTrack[] tracks)
        {
            var assetPath = $"{SkillAssetFolder}/{skillId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<NewSkillSO>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<NewSkillSO>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            asset.Data = new NewSkillDef
            {
                SkillId = skillId,
                TotalDurationTicks = durationTicks,
                Tracks = tracks.ToList()
            };

            return asset;
        }

        private static SkillTrack Track(string name, params SkillEvent[] events)
        {
            return new SkillTrack
            {
                TrackName = name,
                Events = events.ToList()
            };
        }

        private static SkillTrack RhythmSound(string eventPath, params (int tick, float volume)[] hits)
        {
            return Track("Sound", hits.Select(hit => Sound(hit.tick, SubBeatTicks, eventPath, hit.volume)).ToArray());
        }

        private static (int tick, float volume) Hit(int tick, float volume = 1.0f)
        {
            return (tick, volume);
        }

        private static SkillEvent Warning(int tick, int duration, IShapeDef shape)
        {
            return Event(tick, duration, new WarningAction
            {
                Shape = CloneShape(shape),
                ColorSteps = new List<WarningColorStep>
                {
                    new WarningColorStep { DurationTicks = duration, ColorHex = "#E8D36A" }
                }
            });
        }

        private static SkillEvent Damage(int tick, int duration, IShapeDef shape, int amount, int stun = 0, int knockback = 0)
        {
            return Event(tick, duration, new DamageAction
            {
                Shape = CloneShape(shape),
                Amount = amount,
                HitPlayers = false,
                HitMonsters = true,
                StunDurationTicks = stun,
                KnockbackDistance = knockback,
                RecalculateTargets = false
            });
        }

        private static SkillEvent Move(int tick, int duration, MoveType moveType, int distance, int directionX, int directionY)
        {
            return Event(tick, duration, new MoveAction
            {
                MoveType = moveType,
                Distance = distance,
                DirectionX = directionX,
                DirectionY = directionY,
                StopOnObstacle = true
            });
        }

        private static SkillEvent InputLock(int tick, int duration)
        {
            return Event(tick, duration, new InputLockAction());
        }

        private static SkillEvent Sound(int tick, int duration, string eventPath, float volume = 1.0f)
        {
            return Event(tick, duration, new SoundAction
            {
                FmodEventPath = eventPath,
                Volume = volume,
                UseOwnerPerspective = true
            });
        }

        private static SkillEvent Event(int tick, int duration, BaseAction action)
        {
            return new SkillEvent
            {
                TriggerTick = tick,
                DurationTicks = duration,
                Action = action
            };
        }

        private static IShapeDef Cells(params (int x, int y)[] cells)
        {
            return new CustomCellsShape
            {
                Cells = cells.Select(c => new GridPoint(c.x, c.y)).ToList(),
                CasterSize = 1,
                RotateWithCaster = true
            };
        }

        private static IShapeDef Line(int length)
        {
            var cells = new List<(int x, int y)>();
            for (int i = 1; i <= length; i++)
                cells.Add((0, -i));

            return Cells(cells.ToArray());
        }

        private static IShapeDef WideLine(int length)
        {
            var cells = new List<(int x, int y)>();
            for (int y = 2; y <= length + 1; y++)
            {
                cells.Add((-1, -y));
                cells.Add((0, -y));
                cells.Add((1, -y));
            }

            return Cells(cells.ToArray());
        }

        private static IShapeDef Diamond(int radius)
        {
            return new DiamondShape
            {
                Radius = radius,
                CasterSize = 1,
                RotateWithCaster = true
            };
        }

        private static IShapeDef CloneShape(IShapeDef shape)
        {
            if (shape is CustomCellsShape custom)
            {
                return new CustomCellsShape
                {
                    Cells = custom.Cells.Select(p => new GridPoint(p.X, p.Y)).ToList(),
                    CasterSize = custom.CasterSize,
                    RotateWithCaster = custom.RotateWithCaster
                };
            }

            if (shape is DiamondShape diamond)
            {
                return new DiamondShape
                {
                    Radius = diamond.Radius,
                    CasterSize = diamond.CasterSize,
                    RotateWithCaster = diamond.RotateWithCaster
                };
            }

            if (shape is RectShape rect)
            {
                return new RectShape
                {
                    Width = rect.Width,
                    Height = rect.Height,
                    CasterSize = rect.CasterSize,
                    RotateWithCaster = rect.RotateWithCaster
                };
            }

            return null;
        }

        private static void SaveAndExport(NewSkillSO so)
        {
            EditorUtility.SetDirty(so);

            string serverPath = Path.Combine(GetServerPath(ServerSkillJsonRelativePath), $"{so.Data.SkillId}.json");
            string directory = Path.GetDirectoryName(serverPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = JsonConvert.SerializeObject(so.Data, BatchDataExporter.GetJsonSettings());
            File.WriteAllText(serverPath, json);
        }

        private static void WriteEquipmentJson()
        {
            var rows = new List<EquipmentRow>
            {
                new EquipmentRow
                {
                    Id = 100001,
                    Name = "Novice Sword",
                    Grade = "Common",
                    EquipSlot = "Weapon",
                    BaseAtk = 12,
                    BaseDef = 0,
                    BaseHp = 0,
                    BaseStr = 0,
                    BaseDex = 0,
                    MaxEnhance = 5,
                    SellPrice = 50,
                    ModelPath = "Prefabs/Weapon/Sword001",
                    IconPath = "Icons/W_Sword001",
                    Description = "Balanced starter blade with a quick slash and a reliable cleave.",
                    NormalAttackSkillId = "SwordAttack",
                    SkillId = "NoviceSword",
                    AppearanceId = 10
                },
                new EquipmentRow
                {
                    Id = 100002,
                    Name = "Iron Sword",
                    Grade = "Uncommon",
                    EquipSlot = "Weapon",
                    BaseAtk = 18,
                    BaseDef = 0,
                    BaseHp = 0,
                    BaseStr = 3,
                    BaseDex = 0,
                    MaxEnhance = 10,
                    SellPrice = 200,
                    ModelPath = "Prefabs/Weapon/Sword002",
                    IconPath = "Icons/W_Sword002",
                    Description = "Heavier sword with longer reach and a committed wide slash.",
                    NormalAttackSkillId = "IronSwordAttack",
                    SkillId = "IronSwordSkill",
                    AppearanceId = 10
                },
                new EquipmentRow
                {
                    Id = 100011,
                    Name = "Novice Axe",
                    Grade = "Common",
                    EquipSlot = "Weapon",
                    BaseAtk = 22,
                    BaseDef = 0,
                    BaseHp = 0,
                    BaseStr = 5,
                    BaseDex = 0,
                    MaxEnhance = 10,
                    SellPrice = 200,
                    ModelPath = "Prefabs/Weapon/Axe001",
                    IconPath = "Icons/W_Axe001",
                    Description = "Slow, heavy cleave that rewards timing and positioning.",
                    NormalAttackSkillId = "AxeAttack",
                    SkillId = "NoviceAxe",
                    AppearanceId = 10
                },
                new EquipmentRow
                {
                    Id = 100021,
                    Name = "Novice Bow",
                    Grade = "Common",
                    EquipSlot = "Weapon",
                    BaseAtk = 10,
                    BaseDef = 0,
                    BaseHp = 0,
                    BaseStr = 0,
                    BaseDex = 5,
                    MaxEnhance = 10,
                    SellPrice = 200,
                    ModelPath = "Prefabs/Weapon/Bow001",
                    IconPath = "Icons/W_Bow001",
                    Description = "Lower burst, safer range, and a wide lane special shot.",
                    NormalAttackSkillId = "BowAttack",
                    SkillId = "BowSkill",
                    AppearanceId = 11
                },
                new EquipmentRow
                {
                    Id = 100031,
                    Name = "Novice Dagger",
                    Grade = "Common",
                    EquipSlot = "Weapon",
                    BaseAtk = 8,
                    BaseDef = 0,
                    BaseHp = 0,
                    BaseStr = 0,
                    BaseDex = 7,
                    MaxEnhance = 10,
                    SellPrice = 200,
                    ModelPath = "Prefabs/Weapon/Dagger001",
                    IconPath = "Icons/W_Dagger001",
                    Description = "Fast close-range weapon with a short dash stab special.",
                    NormalAttackSkillId = "DaggerAttack",
                    SkillId = "NoviceDagger",
                    AppearanceId = 11
                },
                new EquipmentRow
                {
                    Id = 200001,
                    Name = "Leather Helmet",
                    Grade = "Common",
                    EquipSlot = "Head",
                    BaseAtk = 0,
                    BaseDef = 4,
                    BaseHp = 15,
                    BaseStr = 0,
                    BaseDex = 0,
                    MaxEnhance = 5,
                    SellPrice = 30,
                    ModelPath = "Prefabs/Armor/Helm001",
                    IconPath = "Icons/A_Helm001",
                    Description = "Adds survivability and a short-range stunning pulse.",
                    SkillId = "HelmetSkill"
                },
                new EquipmentRow
                {
                    Id = 210001,
                    Name = "Leather Armor",
                    Grade = "Common",
                    EquipSlot = "Armor",
                    BaseAtk = 0,
                    BaseDef = 8,
                    BaseHp = 35,
                    BaseStr = 0,
                    BaseDex = 2,
                    MaxEnhance = 5,
                    SellPrice = 40,
                    ModelPath = "Prefabs/Armor/Body001",
                    IconPath = "Icons/A_Body001",
                    Description = "Core defensive piece with a knockback guard burst.",
                    SkillId = "ArmorSkill"
                },
                new EquipmentRow
                {
                    Id = 240001,
                    Name = "Speed Boots",
                    Grade = "Rare",
                    EquipSlot = "Shoes",
                    BaseAtk = 0,
                    BaseDef = 2,
                    BaseHp = 10,
                    BaseStr = 0,
                    BaseDex = 10,
                    MaxEnhance = 10,
                    SellPrice = 500,
                    ModelPath = "Prefabs/Armor/Boots002",
                    IconPath = "Icons/A_Boots002",
                    Description = "Light boots that trade defense for a controllable dash.",
                    SkillId = "MoveSkill"
                },
                new EquipmentRow
                {
                    Id = 240002,
                    Name = "Retreat Boots",
                    Grade = "Rare",
                    EquipSlot = "Shoes",
                    BaseAtk = 0,
                    BaseDef = 1,
                    BaseHp = 8,
                    BaseStr = 0,
                    BaseDex = 12,
                    MaxEnhance = 10,
                    SellPrice = 500,
                    ModelPath = "Prefabs/Armor/Boots002",
                    IconPath = "Icons/A_Boots002",
                    Description = "Evasive boots with a quick backward disengage.",
                    SkillId = "BackstepSkill"
                }
            };

            var settings = BatchDataExporter.GetJsonSettings();
            string json = JsonConvert.SerializeObject(rows, settings);
            File.WriteAllText(ClientEquipmentJsonPath, json);
            File.WriteAllText(GetServerPath(ServerEquipmentJsonRelativePath), json);
        }

        private static void WriteEntityJson(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[BuildPlayerEquipmentBalance] EntityData not found: {path}");
                return;
            }

            var stats = GetEntityStats();
            var root = JObject.Parse(File.ReadAllText(path));
            var entities = root["Entities"] as JArray;
            if (entities == null)
                return;

            foreach (var entity in entities.OfType<JObject>())
            {
                int id = entity.Value<int>("EntityId");
                if (!stats.TryGetValue(id, out var stat))
                    continue;

                entity["MaxHp"] = stat.hp;
                entity["Atk"] = stat.atk;
                entity["Def"] = stat.def;
            }

            File.WriteAllText(path, root.ToString(Formatting.Indented));
        }

        private static void WriteForestStageSongKey()
        {
            WriteStageJsonSongKey(ClientStageJsonPath, ForestStageSongKey);
            WriteStageJsonSongKey(GetServerPath(ServerForestStageJsonRelativePath), ForestStageSongKey);
            WriteStageAssetSongKey(ClientStageAssetPath, ForestStageSongKey);
        }

        private static void WriteStageJsonSongKey(string path, string songKey)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[BuildPlayerEquipmentBalance] Stage JSON not found: {path}");
                return;
            }

            string json = File.ReadAllText(path);
            if (!TryReplaceFirstMatch(json, "(\"SongKey\"\\s*:\\s*\")[^\"]*(\")", songKey, out string updated))
            {
                Debug.LogWarning($"[BuildPlayerEquipmentBalance] SongKey not found in stage JSON: {path}");
                return;
            }

            if (updated != json)
                File.WriteAllText(path, updated);
        }

        private static void WriteStageAssetSongKey(string assetPath, string songKey)
        {
            if (!File.Exists(assetPath))
            {
                Debug.LogWarning($"[BuildPlayerEquipmentBalance] Stage asset not found: {assetPath}");
                return;
            }

            string yaml = File.ReadAllText(assetPath);
            if (!TryReplaceFirstMatch(yaml, @"(^\s*SongKey:\s*).*$", songKey, out string updated, RegexOptions.Multiline))
            {
                Debug.LogWarning($"[BuildPlayerEquipmentBalance] SongKey not found in stage asset: {assetPath}");
                return;
            }

            if (updated != yaml)
                File.WriteAllText(assetPath, updated);
        }

        private static bool TryReplaceFirstMatch(
            string source,
            string pattern,
            string value,
            out string updated,
            RegexOptions options = RegexOptions.None)
        {
            var match = Regex.Match(source, pattern, options);
            if (!match.Success || match.Groups.Count < 2)
            {
                updated = source;
                return false;
            }

            int start = match.Groups[1].Index + match.Groups[1].Length;
            int end = match.Groups.Count > 2 ? match.Groups[2].Index : match.Index + match.Length;
            updated = source.Substring(0, start) + value + source.Substring(end);
            return true;
        }

        private static Dictionary<int, (int hp, int atk, int def)> GetEntityStats()
        {
            return new Dictionary<int, (int hp, int atk, int def)>
            {
                { 10, (140, 0, 0) },
                { 11, (140, 0, 0) },
                { 12, (140, 0, 0) },
                { 500, (999999, 0, 0) },
                { 501, (999999, 0, 0) },
                { 502, (999999, 0, 0) },
                { 503, (999999, 0, 0) },
                { 504, (999999, 0, 0) },
                { 999, (90, 0, 0) },
                { 1000, (55, 8, 1) },
                { 1001, (55, 8, 1) },
                { 1010, (45, 7, 0) },
                { 1011, (280, 28, 6) },
                { 1012, (130, 18, 2) },
                { 1013, (180, 20, 8) },
                { 1014, (85, 12, 3) },
                { 1015, (110, 16, 4) },
                { 1016, (75, 12, 2) },
                { 1017, (60, 9, 1) },
                { 1018, (70, 11, 1) },
                { 1019, (120, 10, 7) },
                { 1020, (115, 17, 3) },
                { 1021, (260, 22, 8) },
                { 1022, (95, 14, 4) },
                { 1023, (90, 13, 5) },
                { 1024, (80, 15, 1) },
                { 1025, (105, 16, 4) },
                { 1026, (70, 18, 1) },
                { 1027, (95, 18, 0) },
                { 1028, (130, 20, 3) },
                { 1029, (100, 14, 5) },
                { 1030, (65, 13, 0) },
                { 1031, (140, 18, 6) },
                { 1032, (100, 12, 5) },
                { 1033, (180, 24, 5) },
                { 1034, (320, 30, 10) },
                { 1035, (95, 15, 3) },
                { 1036, (75, 13, 2) },
                { 1037, (65, 9, 1) },
                { 1038, (125, 19, 2) },
                { 1039, (110, 17, 3) },
                { 1040, (80, 16, 1) }
            };
        }

        private static string GetServerPath(string relativePath)
        {
            string clientRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(clientRoot, relativePath));
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        private sealed class EquipmentRow
        {
            [JsonProperty("id")] public int Id { get; set; }
            [JsonProperty("name")] public string Name { get; set; } = "";
            [JsonProperty("type")] public string Type { get; set; } = "Equipment";
            [JsonProperty("equip_slot")] public string EquipSlot { get; set; } = "";
            [JsonProperty("grade")] public string Grade { get; set; } = "Common";
            [JsonProperty("max_stack")] public int MaxStack { get; set; } = 1;
            [JsonProperty("base_atk")] public int BaseAtk { get; set; }
            [JsonProperty("base_def")] public int BaseDef { get; set; }
            [JsonProperty("base_hp")] public int BaseHp { get; set; }
            [JsonProperty("base_str")] public int BaseStr { get; set; }
            [JsonProperty("base_dex")] public int BaseDex { get; set; }
            [JsonProperty("max_enhance")] public int MaxEnhance { get; set; }
            [JsonProperty("sell_price")] public int SellPrice { get; set; }
            [JsonProperty("model_path")] public string ModelPath { get; set; } = "";
            [JsonProperty("icon_path")] public string IconPath { get; set; } = "";
            [JsonProperty("description")] public string Description { get; set; } = "";
            [JsonProperty("normal_attack_skill_id")] public string NormalAttackSkillId { get; set; } = "";
            [JsonProperty("skill_id")] public string SkillId { get; set; } = "";
            [JsonProperty("appearance_id")] public int AppearanceId { get; set; }
        }
    }
}
#endif
