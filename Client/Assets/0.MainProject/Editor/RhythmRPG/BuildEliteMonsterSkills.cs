#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using Client.Data;
using GameShared.Data;

namespace RhythmRPG.Editor
{
    public static class BuildEliteMonsterSkills
    {
        private const string DefaultSkillAssetFolder = "Assets/Resources/Data/NewSkills";
        private const string ServerSkillJsonRelativePath = "../Server/GameServer/Content/01.Game/Skill/Json";

        [MenuItem("RhythmRPG/Editors/Setup/Build Elite Monster Skills")]
        public static void Build()
        {
            EnsureFolder(DefaultSkillAssetFolder + "/Enemy");

            int createdCount = 0;

            // 1. Enemy_BlackKnight_PulseSlash
            var pulseSlash = CreateSkillAsset("Enemy_BlackKnight_PulseSlash", 960);
            var slashCells = new List<GridPoint> { new(0, -1), new(-1, -1), new(1, -1) };
            
            var slashWarnAction = new WarningAction { Shape = new CustomCellsShape { Cells = slashCells } };
            pulseSlash.Data.Tracks.Add(new SkillTrack {
                TrackName = "Telegraph",
                Events = new List<SkillEvent> { new() { TriggerTick = 0, DurationTicks = 480, Action = slashWarnAction } }
            });

            var slashDmgAction = new DamageAction {
                Shape = new CustomCellsShape { Cells = slashCells },
                Amount = 20,
                HitPlayers = true,
                HitMonsters = false
            };
            pulseSlash.Data.Tracks.Add(new SkillTrack {
                TrackName = "Impact",
                Events = new List<SkillEvent> { new() { TriggerTick = 480, DurationTicks = 480, Action = slashDmgAction } }
            });
            SaveAndExport(pulseSlash);
            createdCount++;

            // 2. Enemy_BlackKnight_ShieldBash
            var shieldBash = CreateSkillAsset("Enemy_BlackKnight_ShieldBash", 960);
            var bashCells = new List<GridPoint> { new(0, -1) };

            var bashWarnAction = new WarningAction { Shape = new CustomCellsShape { Cells = bashCells } };
            shieldBash.Data.Tracks.Add(new SkillTrack {
                TrackName = "Telegraph",
                Events = new List<SkillEvent> { new() { TriggerTick = 0, DurationTicks = 480, Action = bashWarnAction } }
            });

            var bashDmgAction = new DamageAction {
                Shape = new CustomCellsShape { Cells = bashCells },
                Amount = 15,
                HitPlayers = true,
                HitMonsters = false,
                StunDurationTicks = 480,
                KnockbackDistance = 1
            };
            shieldBash.Data.Tracks.Add(new SkillTrack {
                TrackName = "Impact",
                Events = new List<SkillEvent> { new() { TriggerTick = 480, DurationTicks = 480, Action = bashDmgAction } }
            });
            SaveAndExport(shieldBash);
            createdCount++;

            // 3. Enemy_BlackKnight_CrystallineLeap
            var crystallineLeap = CreateSkillAsset("Enemy_BlackKnight_CrystallineLeap", 1440);
            var leapCells = new List<GridPoint> {
                new(0, 0),
                new(0, -1), new(0, -2),
                new(0, 1), new(0, 2),
                new(-1, 0), new(-2, 0),
                new(1, 0), new(2, 0)
            };

            var leapWarnAction = new WarningAction { Shape = new CustomCellsShape { Cells = leapCells } };
            crystallineLeap.Data.Tracks.Add(new SkillTrack {
                TrackName = "Telegraph",
                Events = new List<SkillEvent> { new() { TriggerTick = 0, DurationTicks = 960, Action = leapWarnAction } }
            });

            var leapDmgAction = new DamageAction {
                Shape = new CustomCellsShape { Cells = leapCells },
                Amount = 30,
                HitPlayers = true,
                HitMonsters = false
            };
            crystallineLeap.Data.Tracks.Add(new SkillTrack {
                TrackName = "Impact",
                Events = new List<SkillEvent> { new() { TriggerTick = 960, DurationTicks = 480, Action = leapDmgAction } }
            });
            SaveAndExport(crystallineLeap);
            createdCount++;

            // 4. Enemy_BlackKnight_CrystalStorm
            var crystalStorm = CreateSkillAsset("Enemy_BlackKnight_CrystalStorm", 1440);
            
            var innerRing = new List<GridPoint> {
                new(-1, -1), new(0, -1), new(1, -1),
                new(-1, 0),              new(1, 0),
                new(-1, 1),  new(0, 1),  new(1, 1)
            };

            var outerRing = new List<GridPoint> {
                new(-2, -2), new(-1, -2), new(0, -2), new(1, -2), new(2, -2),
                new(-2, -1),                                      new(2, -1),
                new(-2, 0),                                       new(2, 0),
                new(-2, 1),                                       new(2, 1),
                new(-2, 2),  new(-1, 2),  new(0, 2),  new(1, 2),  new(2, 2)
            };

            var fullStorm = new List<GridPoint>();
            fullStorm.AddRange(innerRing);
            fullStorm.AddRange(outerRing);

            var stormWarnInner = new WarningAction { Shape = new CustomCellsShape { Cells = innerRing } };
            var stormWarnOuter = new WarningAction { Shape = new CustomCellsShape { Cells = outerRing } };
            crystalStorm.Data.Tracks.Add(new SkillTrack {
                TrackName = "Telegraph",
                Events = new List<SkillEvent> {
                    new() { TriggerTick = 0, DurationTicks = 480, Action = stormWarnInner },
                    new() { TriggerTick = 480, DurationTicks = 480, Action = stormWarnOuter }
                }
            });

            var stormDmgAction = new DamageAction {
                Shape = new CustomCellsShape { Cells = fullStorm },
                Amount = 35,
                HitPlayers = true,
                HitMonsters = false,
                KnockbackDistance = 2
            };
            crystalStorm.Data.Tracks.Add(new SkillTrack {
                TrackName = "Impact",
                Events = new List<SkillEvent> { new() { TriggerTick = 960, DurationTicks = 480, Action = stormDmgAction } }
            });
            SaveAndExport(crystalStorm);
            createdCount++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BuildEliteMonsterSkills] Successfully built and exported {createdCount} skills for the Elite BlackKnight!");
        }

        private static NewSkillSO CreateSkillAsset(string skillId, int totalDurationTicks)
        {
            var path = $"{DefaultSkillAssetFolder}/Enemy/{skillId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<NewSkillSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<NewSkillSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Data = new NewSkillDef {
                SkillId = skillId,
                TotalDurationTicks = totalDurationTicks,
                Tracks = new List<SkillTrack>()
            };

            return asset;
        }

        private static void SaveAndExport(NewSkillSO so)
        {
            EditorUtility.SetDirty(so);

            // Export to Server JSON
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string serverPath = Path.GetFullPath(
                Path.Combine(projectRoot, ServerSkillJsonRelativePath, $"{so.Data.SkillId}.json"));
            string directory = Path.GetDirectoryName(serverPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var settings = BatchDataExporter.GetJsonSettings();
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(so.Data, settings);
            File.WriteAllText(serverPath, json);
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
    }
}
#endif
