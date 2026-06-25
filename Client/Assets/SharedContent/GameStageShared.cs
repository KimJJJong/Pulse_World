using System;
using System.Collections.Generic;
using System.Text;

namespace GameServer.InGame.Director.Data
{
    [Serializable]
    public class StageScenario
    {
        public string MapId = string.Empty;
        public string Description = string.Empty;
        public bool DisableAutoClearOnMonsterWipe;

        public RhythmSettingsData RhythmSettings = new RhythmSettingsData();

        public List<SpawnData> InitialSpawns = new List<SpawnData>();
        public List<SpawnObjectData> InitialObjects = new List<SpawnObjectData>();

        public List<EventData> Events = new List<EventData>();
    }

    [Serializable]
    public class RhythmSettingsData
    {
        public string SongKey = "DefaultSong";
        public int Bpm = 120;
        public int BaseBeatDivision = 1;
        public int ActionWindowMs = 100;
        public int StartDelayMs = 2000;
    }

    [Serializable]
    public class SpawnData
    {
        public int MonsterId;
        public int X;
        public int Y;
        public int Z;
        public string AI = "Default";
        public string Pattern = string.Empty;
        public string PatternId = string.Empty;
        public string PatternKey = string.Empty;
        public int GroupId = 0;
        public float Rotation = 0f;

        public string ResolvePatternKey()
        {
            if (!string.IsNullOrWhiteSpace(PatternKey))
                return PatternKey;

            if (!string.IsNullOrWhiteSpace(PatternId))
                return PatternId;

            if (!string.IsNullOrWhiteSpace(Pattern))
                return Pattern;

            if (!string.IsNullOrWhiteSpace(AI))
                return AI;

            return "Default";
        }
    }

    [Serializable]
    public class SpawnObjectData
    {
        public int EntityId;
        public int EntityType;
        public int X;
        public int Y;
        public int Z;
        public int GroupId;
        public int SizeX = 1;
        public int SizeY = 1;
        public float Rotation = 0f;
        public string Pattern = string.Empty;
    }

    [Serializable]
    public class EventData
    {
        public int EventId;
        public bool IsOneShot = true;

        public List<ConditionData> Conditions = new List<ConditionData>();
        public List<ActionData> Actions = new List<ActionData>();
    }

    [Serializable]
    public class ConditionData
    {
        public string Type = string.Empty;
        public int TargetId;
        public int SecondaryTargetId;
        public string TargetKey = string.Empty;
        public int Count;
        public bool UseParticipantCount;
        public bool ShowProgressUi = true;
        public bool ShowAreaOutline = true;
        public string ProgressLabel = string.Empty;
        public int ProgressDurationMs = 1200;
        public RectData Area;
    }

    [Serializable]
    public class ActionData
    {
        public string Type = string.Empty;
        public int ParamId;
        public int X;
        public int Y;
        public int Z;
        public string StringVal = string.Empty;
        public int GroupId;
        public int SizeX = 1;
        public int SizeY = 1;
        public string GuideTitle = string.Empty;
        public string GuideBody = string.Empty;
        public string GuideImageResource = string.Empty;
        public int DurationMs;
        public string VfxKey = string.Empty;
    }

    [Serializable]
    public class StageGuideData
    {
        public string Title = string.Empty;
        public string Body = string.Empty;
        public string ImageResource = string.Empty;
        public int DurationMs = 3500;
    }

    [Serializable]
    public class StageVfxData
    {
        public string VfxKey = string.Empty;
        public int X;
        public int Y;
        public int Z;
        public int DurationMs;
        public int TargetId;
        public int SecondaryTargetId;
    }

    [Serializable]
    public class StageAreaProgressData
    {
        public string Label = string.Empty;
        public int CurrentCount;
        public int RequiredCount;
        public int DurationMs = 1200;
        public bool ShowAreaOutline = true;
        public RectData Area;
    }

    [Serializable]
    public class StageTutorialPanelData
    {
        public bool Visible = true;
        public string PanelId = string.Empty;
        public string ImageResource = string.Empty;
        public int FadeMs = 220;
        public int Width = 900;
        public int AnchorPreset = 7;
        public int OffsetX = 24;
        public int OffsetY;
    }

    [Serializable]
    public class StageSceneObjectData
    {
        public string TargetKey = string.Empty;
        public int GroupId;
        public bool Visible = true;
        public int DurationMs = 650;
        public int DelayMs;
    }

    [Serializable]
    public class StageSummonPortalData
    {
        public string PortalKey = string.Empty;
        public bool Active = true;
        public int SpawnGroupId;
        public int MaxAlive = 2;
        public int IntervalBeats = 8;
        public int InitialDelayBeats = 1;
        public int SpawnX;
        public int SpawnY;
        public int SpawnZ;
        public string MonsterIdsCsv = string.Empty;
        public string PatternKey = string.Empty;
    }

    [Serializable]
    public class StageGateDoorData
    {
        public string TargetKey = string.Empty;
        public int GroupId;
        public bool Open = true;
        public int DurationMs = 900;
        public int AngleDegrees;
    }

    [Serializable]
    public class StageClearResultData
    {
        public string MapId = string.Empty;
        public string Title = "STAGE CLEAR";
        public string Subtitle = "Purification Complete";
        public string Rank = "S";
        public int ClearTimeMs;
        public int RhythmSyncPercent = 98;
        public int MaxCombo = 842;
        public int Misses = 4;
        public int EchoNodesRestored = 4;
        public int EchoNodesTotal = 4;
        public string CorruptionResidue = "Low";
        public int GateStabilityPercent = 92;
        public int HiddenEchoFound = 1;
        public bool FirstClearBonusActive = true;
        public string RouteUnlocked = "Deepwood Gate";
        public string NextArea = "Deepwood Gate";
        public int RecommendedLevel = 12;
        public string DangerRhythm = "Normal";
    }

    public enum StageVfxTargetMode
    {
        PositionMarker,
        ObjectPulseColor
    }

    public sealed class StageVfxDefinition
    {
        public StageVfxDefinition(string key, string displayName, StageVfxTargetMode targetMode, string description)
        {
            Key = key ?? string.Empty;
            DisplayName = displayName ?? Key;
            TargetMode = targetMode;
            Description = description ?? string.Empty;
        }

        public string Key { get; }
        public string DisplayName { get; }
        public StageVfxTargetMode TargetMode { get; }
        public string Description { get; }
    }

    public static class StageVfxKeys
    {
        public const string MarkerCyan = "MarkerCyan";
        public const string CrystalPulseRed = "CrystalPulseRed";
        public const string CrystalPulseEmerald = "CrystalPulseEmerald";
        public const string CrystalPulseBlue = "CrystalPulseBlue";
    }

    public static class StageVfxCatalog
    {
        private static readonly StageVfxDefinition[] Definitions =
        {
            new StageVfxDefinition(
                StageVfxKeys.MarkerCyan,
                "Marker Cyan",
                StageVfxTargetMode.PositionMarker,
                "Position 위치에 기존 cyan VFX 마커를 표시합니다."),
            new StageVfxDefinition(
                StageVfxKeys.CrystalPulseRed,
                "Crystal Pulse Red",
                StageVfxTargetMode.ObjectPulseColor,
                "Target Object Group/ID의 크리스탈 pulse 색을 붉은색으로 변경합니다."),
            new StageVfxDefinition(
                StageVfxKeys.CrystalPulseEmerald,
                "Crystal Pulse Emerald",
                StageVfxTargetMode.ObjectPulseColor,
                "Target Object Group/ID의 크리스탈 pulse 색을 에메랄드색으로 변경합니다."),
            new StageVfxDefinition(
                StageVfxKeys.CrystalPulseBlue,
                "Crystal Pulse Blue",
                StageVfxTargetMode.ObjectPulseColor,
                "Target Object Group/ID의 크리스탈 pulse 색을 푸른색으로 변경합니다.")
        };

        private static readonly Dictionary<string, StageVfxDefinition> Lookup = BuildLookup();

        public static IReadOnlyList<StageVfxDefinition> All => Definitions;

        public static bool TryGetDefinition(string key, out StageVfxDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                definition = null;
                return false;
            }

            return Lookup.TryGetValue(NormalizeKey(key), out definition);
        }

        public static string NormalizeKey(string key)
            => string.IsNullOrWhiteSpace(key)
                ? string.Empty
                : key.Replace("_", string.Empty).Replace("-", string.Empty).Trim().ToLowerInvariant();

        private static Dictionary<string, StageVfxDefinition> BuildLookup()
        {
            var lookup = new Dictionary<string, StageVfxDefinition>(StringComparer.Ordinal);
            foreach (var definition in Definitions)
                lookup[NormalizeKey(definition.Key)] = definition;

            RegisterAlias(lookup, StageVfxKeys.CrystalPulseBlue, "CrystalPulse");
            RegisterAlias(lookup, StageVfxKeys.CrystalPulseBlue, "CrystalPulseCyan");
            RegisterAlias(lookup, StageVfxKeys.CrystalPulseEmerald, "CrystalPulseGreen");
            RegisterAlias(lookup, StageVfxKeys.CrystalPulseEmerald, "EmeraldCrystalPulse");
            RegisterAlias(lookup, StageVfxKeys.CrystalPulseEmerald, "CrystalEmeraldPulse");
            RegisterAlias(lookup, StageVfxKeys.CrystalPulseRed, "CrystalRedPulse");
            RegisterAlias(lookup, StageVfxKeys.CrystalPulseRed, "RedCrystalPulse");
            return lookup;
        }

        private static void RegisterAlias(Dictionary<string, StageVfxDefinition> lookup, string canonicalKey, string alias)
        {
            if (TryFindCanonical(canonicalKey, out var definition))
                lookup[NormalizeKey(alias)] = definition;
        }

        private static bool TryFindCanonical(string canonicalKey, out StageVfxDefinition definition)
        {
            foreach (var item in Definitions)
            {
                if (string.Equals(item.Key, canonicalKey, StringComparison.Ordinal))
                {
                    definition = item;
                    return true;
                }
            }

            definition = null;
            return false;
        }
    }

    public static class StageSignalCodec
    {
        public const int GuideWarnCode = 6101;
        public const int VfxWarnCode = 6102;
        public const int StageClearWarnCode = 6103;
        public const int TutorialPanelWarnCode = 6104;
        public const int SceneObjectWarnCode = 6105;
        public const int GateDoorWarnCode = 6106;
        public const int AreaProgressWarnCode = 6107;
        public const string GuidePrefix = "STAGE_GUIDE";
        public const string VfxPrefix = "STAGE_VFX";
        public const string StageClearPrefix = "STAGE_CLEAR";
        public const string TutorialPanelPrefix = "STAGE_TUTORIAL_PANEL";
        public const string SceneObjectPrefix = "STAGE_SCENE_OBJECT";
        public const string GateDoorPrefix = "STAGE_GATE_DOOR";
        public const string AreaProgressPrefix = "STAGE_AREA_PROGRESS";

        public static string EncodeGuide(StageGuideData data)
        {
            data ??= new StageGuideData();
            return string.Join("\t",
                GuidePrefix,
                Math.Max(0, data.DurationMs),
                EncodeText(data.Title),
                EncodeText(data.Body),
                EncodeText(data.ImageResource));
        }

        public static string EncodeVfx(StageVfxData data)
        {
            data ??= new StageVfxData();
            return string.Join("\t",
                VfxPrefix,
                EncodeText(data.VfxKey),
                data.X,
                data.Y,
                data.Z,
                Math.Max(0, data.DurationMs),
                data.TargetId,
                data.SecondaryTargetId);
        }

        public static string EncodeTutorialPanel(StageTutorialPanelData data)
        {
            data ??= new StageTutorialPanelData();
            return string.Join("\t",
                TutorialPanelPrefix,
                data.Visible ? 1 : 0,
                EncodeText(data.PanelId),
                EncodeText(data.ImageResource),
                Math.Max(0, data.FadeMs),
                Math.Max(0, data.Width),
                data.AnchorPreset,
                data.OffsetX,
                data.OffsetY);
        }

        public static string EncodeSceneObject(StageSceneObjectData data)
        {
            data ??= new StageSceneObjectData();
            return string.Join("\t",
                SceneObjectPrefix,
                EncodeText(data.TargetKey),
                data.GroupId,
                data.Visible ? 1 : 0,
                Math.Max(0, data.DurationMs),
                Math.Max(0, data.DelayMs));
        }

        public static string EncodeGateDoor(StageGateDoorData data)
        {
            data ??= new StageGateDoorData();
            return string.Join("\t",
                GateDoorPrefix,
                EncodeText(data.TargetKey),
                data.GroupId,
                data.Open ? 1 : 0,
                Math.Max(0, data.DurationMs),
                data.AngleDegrees);
        }

        public static string EncodeStageClear(StageClearResultData data)
        {
            data ??= new StageClearResultData();
            return string.Join("\t",
                StageClearPrefix,
                EncodeText(data.MapId),
                EncodeText(data.Title),
                EncodeText(data.Subtitle),
                EncodeText(data.Rank),
                Math.Max(0, data.ClearTimeMs),
                data.RhythmSyncPercent,
                data.MaxCombo,
                data.Misses,
                data.EchoNodesRestored,
                data.EchoNodesTotal,
                EncodeText(data.CorruptionResidue),
                data.GateStabilityPercent,
                data.HiddenEchoFound,
                data.FirstClearBonusActive ? 1 : 0,
                EncodeText(data.RouteUnlocked),
                EncodeText(data.NextArea),
                data.RecommendedLevel,
                EncodeText(data.DangerRhythm));
        }

        public static string EncodeAreaProgress(StageAreaProgressData data)
        {
            data ??= new StageAreaProgressData();
            RectData area = data.Area ?? new RectData();
            return string.Join("\t",
                AreaProgressPrefix,
                EncodeText(data.Label),
                data.CurrentCount,
                data.RequiredCount,
                Math.Max(0, data.DurationMs),
                data.ShowAreaOutline ? 1 : 0,
                area.X,
                area.Y,
                area.W,
                area.H,
                EncodeText(EncodeAreaCells(area)));
        }

        public static bool TryDecodeGuide(string payload, out StageGuideData data)
        {
            data = null;
            if (string.IsNullOrEmpty(payload))
                return false;

            string[] parts = payload.Split('\t');
            if (parts.Length < 5 || !string.Equals(parts[0], GuidePrefix, StringComparison.Ordinal))
                return false;

            data = new StageGuideData
            {
                DurationMs = int.TryParse(parts[1], out int durationMs) ? durationMs : 3500,
                Title = DecodeText(parts[2]),
                Body = DecodeText(parts[3]),
                ImageResource = DecodeText(parts[4])
            };
            return true;
        }

        public static bool TryDecodeVfx(string payload, out StageVfxData data)
        {
            data = null;
            if (string.IsNullOrEmpty(payload))
                return false;

            string[] parts = payload.Split('\t');
            if (parts.Length < 6 || !string.Equals(parts[0], VfxPrefix, StringComparison.Ordinal))
                return false;

            data = new StageVfxData
            {
                VfxKey = DecodeText(parts[1]),
                X = int.TryParse(parts[2], out int x) ? x : 0,
                Y = int.TryParse(parts[3], out int y) ? y : 0,
                Z = int.TryParse(parts[4], out int z) ? z : 0,
                DurationMs = int.TryParse(parts[5], out int durationMs) ? durationMs : 0,
                TargetId = parts.Length > 6 && int.TryParse(parts[6], out int targetId) ? targetId : 0,
                SecondaryTargetId = parts.Length > 7 && int.TryParse(parts[7], out int secondaryTargetId) ? secondaryTargetId : 0
            };
            return true;
        }

        public static bool TryDecodeTutorialPanel(string payload, out StageTutorialPanelData data)
        {
            data = null;
            if (string.IsNullOrEmpty(payload))
                return false;

            string[] parts = payload.Split('\t');
            if (parts.Length < 9 || !string.Equals(parts[0], TutorialPanelPrefix, StringComparison.Ordinal))
                return false;

            data = new StageTutorialPanelData
            {
                Visible = int.TryParse(parts[1], out int visible) && visible != 0,
                PanelId = DecodeText(parts[2]),
                ImageResource = DecodeText(parts[3]),
                FadeMs = int.TryParse(parts[4], out int fadeMs) ? fadeMs : 220,
                Width = int.TryParse(parts[5], out int width) ? width : 900,
                AnchorPreset = int.TryParse(parts[6], out int anchorPreset) ? anchorPreset : 7,
                OffsetX = int.TryParse(parts[7], out int offsetX) ? offsetX : 24,
                OffsetY = int.TryParse(parts[8], out int offsetY) ? offsetY : 0
            };
            return true;
        }

        public static bool TryDecodeSceneObject(string payload, out StageSceneObjectData data)
        {
            data = null;
            if (string.IsNullOrEmpty(payload))
                return false;

            string[] parts = payload.Split('\t');
            if (parts.Length < 4 || !string.Equals(parts[0], SceneObjectPrefix, StringComparison.Ordinal))
                return false;

            data = new StageSceneObjectData
            {
                TargetKey = DecodeText(parts[1]),
                GroupId = int.TryParse(parts[2], out int groupId) ? groupId : 0,
                Visible = int.TryParse(parts[3], out int visible) && visible != 0,
                DurationMs = parts.Length > 4 && int.TryParse(parts[4], out int durationMs) ? durationMs : 650,
                DelayMs = parts.Length > 5 && int.TryParse(parts[5], out int delayMs) ? delayMs : 0
            };
            return true;
        }

        public static bool TryDecodeGateDoor(string payload, out StageGateDoorData data)
        {
            data = null;
            if (string.IsNullOrEmpty(payload))
                return false;

            string[] parts = payload.Split('\t');
            if (parts.Length < 5 || !string.Equals(parts[0], GateDoorPrefix, StringComparison.Ordinal))
                return false;

            data = new StageGateDoorData
            {
                TargetKey = DecodeText(parts[1]),
                GroupId = int.TryParse(parts[2], out int groupId) ? groupId : 0,
                Open = int.TryParse(parts[3], out int open) && open != 0,
                DurationMs = int.TryParse(parts[4], out int durationMs) ? durationMs : 900,
                AngleDegrees = parts.Length > 5 && int.TryParse(parts[5], out int angleDegrees) ? angleDegrees : 0
            };
            return true;
        }

        public static bool TryDecodeStageClear(string payload, out StageClearResultData data)
        {
            data = null;
            if (string.IsNullOrEmpty(payload))
                return false;

            string[] parts = payload.Split('\t');
            if (parts.Length < 19 || !string.Equals(parts[0], StageClearPrefix, StringComparison.Ordinal))
                return false;

            data = new StageClearResultData
            {
                MapId = DecodeText(parts[1]),
                Title = DecodeText(parts[2]),
                Subtitle = DecodeText(parts[3]),
                Rank = DecodeText(parts[4]),
                ClearTimeMs = int.TryParse(parts[5], out int clearTimeMs) ? clearTimeMs : 0,
                RhythmSyncPercent = int.TryParse(parts[6], out int rhythmSync) ? rhythmSync : 98,
                MaxCombo = int.TryParse(parts[7], out int maxCombo) ? maxCombo : 842,
                Misses = int.TryParse(parts[8], out int misses) ? misses : 4,
                EchoNodesRestored = int.TryParse(parts[9], out int restored) ? restored : 4,
                EchoNodesTotal = int.TryParse(parts[10], out int total) ? total : 4,
                CorruptionResidue = DecodeText(parts[11]),
                GateStabilityPercent = int.TryParse(parts[12], out int stability) ? stability : 92,
                HiddenEchoFound = int.TryParse(parts[13], out int hiddenEcho) ? hiddenEcho : 1,
                FirstClearBonusActive = int.TryParse(parts[14], out int firstClear) && firstClear != 0,
                RouteUnlocked = DecodeText(parts[15]),
                NextArea = DecodeText(parts[16]),
                RecommendedLevel = int.TryParse(parts[17], out int recommendedLevel) ? recommendedLevel : 12,
                DangerRhythm = DecodeText(parts[18])
            };
            return true;
        }

        public static bool TryDecodeAreaProgress(string payload, out StageAreaProgressData data)
        {
            data = null;
            if (string.IsNullOrEmpty(payload))
                return false;

            string[] parts = payload.Split('\t');
            if (parts.Length < 10 || !string.Equals(parts[0], AreaProgressPrefix, StringComparison.Ordinal))
                return false;

            var area = new RectData
            {
                X = int.TryParse(parts[6], out int x) ? x : 0,
                Y = int.TryParse(parts[7], out int y) ? y : 0,
                W = int.TryParse(parts[8], out int w) ? w : 0,
                H = int.TryParse(parts[9], out int h) ? h : 0
            };

            if (parts.Length > 10)
                DecodeAreaCells(DecodeText(parts[10]), area);

            data = new StageAreaProgressData
            {
                Label = DecodeText(parts[1]),
                CurrentCount = int.TryParse(parts[2], out int current) ? current : 0,
                RequiredCount = int.TryParse(parts[3], out int required) ? required : 0,
                DurationMs = int.TryParse(parts[4], out int durationMs) ? durationMs : 1200,
                ShowAreaOutline = int.TryParse(parts[5], out int showOutline) && showOutline != 0,
                Area = area
            };
            return true;
        }

        private static string EncodeText(string value)
            => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

        private static string DecodeText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string EncodeAreaCells(RectData area)
        {
            if (area?.Cells == null || area.Cells.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            for (int i = 0; i < area.Cells.Count; i++)
            {
                GridPointData cell = area.Cells[i];
                if (i > 0)
                    builder.Append(';');

                builder.Append(cell.X);
                builder.Append(',');
                builder.Append(cell.Y);
            }

            return builder.ToString();
        }

        private static void DecodeAreaCells(string encodedCells, RectData area)
        {
            if (area == null || string.IsNullOrWhiteSpace(encodedCells))
                return;

            area.Cells ??= new List<GridPointData>();
            area.Cells.Clear();

            string[] cells = encodedCells.Split(';');
            foreach (string cellText in cells)
            {
                if (string.IsNullOrWhiteSpace(cellText))
                    continue;

                string[] xy = cellText.Split(',');
                if (xy.Length != 2)
                    continue;

                if (int.TryParse(xy[0], out int x) && int.TryParse(xy[1], out int y))
                    area.Cells.Add(new GridPointData { X = x, Y = y });
            }
        }
    }

    [Serializable]
    public class RectData
    {
        public int X;
        public int Y;
        public int W;
        public int H;
        public string Shape = string.Empty;
        public List<GridPointData> Cells = new List<GridPointData>();
    }

    [Serializable]
    public class GridPointData
    {
        public int X;
        public int Y;
    }

    public static class StageAreaUtility
    {
        public static bool Contains(RectData area, int x, int y)
        {
            if (area == null)
                return false;

            if (area.Cells != null && area.Cells.Count > 0)
            {
                for (int i = 0; i < area.Cells.Count; i++)
                {
                    GridPointData cell = area.Cells[i];
                    if (cell != null && cell.X == x && cell.Y == y)
                        return true;
                }

                return false;
            }

            return x >= area.X && x < area.X + area.W
                   && y >= area.Y && y < area.Y + area.H;
        }
    }
}

namespace GameServer.InGame.Director.Core
{
    using GameServer.InGame.Director.Data;

    public struct GameEventContext
    {
        public EventType Type;
        public int SourceActorId;
        public int TargetId;
        public int X;
        public int Y;
        public long TimeMs;

        public GameEventContext(EventType type, int sourceActorId = 0, int targetId = 0, int x = 0, int y = 0, long timeMs = 0)
        {
            Type = type;
            SourceActorId = sourceActorId;
            TargetId = targetId;
            X = x;
            Y = y;
            TimeMs = timeMs;
        }
    }

    public enum EventType
    {
        None = 0,
        GameStart,
        Beat,
        Move,
        Dead,
        Interact,
        TimeTick
    }

    public interface IStageActionHost
    {
        int GetDeadMonsterCount(int groupId);
        long GetElapsedTimeMs();
        int GetObjectState(int targetId);
        int GetParticipantPlayerCount();
        int CountAlivePlayersInArea(RectData area);

        void SpawnMonster(SpawnData data);
        void SpawnObject(SpawnObjectData data);
        void BroadcastMessage(string msg);
        void ReturnToTown();
        void FinGame();
        void OpenGate(int x, int y);
        void SetObjectState(int targetId, int state);
        void RemoveEntityGroup(int groupId, int delayMs = 0);
        void SetSceneObjectActive(StageSceneObjectData data);
        void SetSummonPortalActive(StageSummonPortalData data);
        void SetGateDoorOpen(StageGateDoorData data);
        void ShowGuide(StageGuideData data);
        void ShowTutorialPanel(StageTutorialPanelData data);
        void HideTutorialPanel(StageTutorialPanelData data);
        void PlayStageVfx(StageVfxData data);
        void ShowAreaProgress(StageAreaProgressData data);
    }

    public abstract class EventCondition
    {
        protected ConditionData _data = new();

        public void Init(ConditionData data)
        {
            _data = data ?? new ConditionData();
        }

        public abstract bool Check(IStageActionHost host, GameEventContext context);
    }

    public abstract class EventAction
    {
        protected ActionData _data = new();

        public void Init(ActionData data)
        {
            _data = data ?? new ActionData();
        }

        public abstract void Execute(IStageActionHost host);
    }

    public sealed class StageRuntimeEngine
    {
        private sealed class RuntimeEvent
        {
            public EventData Data;
            public List<EventCondition> Conditions = new();
            public List<EventAction> Actions = new();
        }

        private StageScenario _scenario;
        private readonly HashSet<int> _executedEventIds = new();
        private readonly List<RuntimeEvent> _runtimeEvents = new();

        public void Reset()
        {
            _scenario = null;
            _executedEventIds.Clear();
            _runtimeEvents.Clear();
        }

        public void LoadScenario(StageScenario scenario)
        {
            Reset();
            _scenario = scenario;

            if (_scenario == null)
                return;

            foreach (var evtData in _scenario.Events ?? new List<EventData>())
            {
                if (evtData == null)
                    continue;

                var rtEvent = new RuntimeEvent { Data = evtData };

                foreach (var condData in evtData.Conditions ?? new List<ConditionData>())
                {
                    var condition = CreateCondition(condData);
                    if (condition != null)
                        rtEvent.Conditions.Add(condition);
                }

                foreach (var actData in evtData.Actions ?? new List<ActionData>())
                {
                    var action = CreateAction(actData);
                    if (action != null)
                        rtEvent.Actions.Add(action);
                }

                _runtimeEvents.Add(rtEvent);
            }
        }

        public void NotifyEvent(GameEventContext context, IStageActionHost host)
        {
            if (_scenario == null || host == null)
                return;

            var initialScenario = _scenario;

            for (int i = 0; i < _runtimeEvents.Count; i++)
            {
                if (_scenario != initialScenario || _scenario == null)
                    break;

                var evt = _runtimeEvents[i];
                if (evt.Data == null)
                    continue;

                if (evt.Data.IsOneShot && _executedEventIds.Contains(evt.Data.EventId))
                    continue;

                bool allMet = true;
                foreach (var cond in evt.Conditions)
                {
                    if (!cond.Check(host, context))
                    {
                        allMet = false;
                        break;
                    }
                }

                if (!allMet)
                    continue;

                foreach (var action in evt.Actions)
                    action.Execute(host);

                if (_scenario != initialScenario || _scenario == null)
                    break;

                if (evt.Data.IsOneShot)
                    _executedEventIds.Add(evt.Data.EventId);
            }
        }

        private static EventCondition CreateCondition(ConditionData data)
        {
            if (data == null)
                return null;

            EventCondition cond = data.Type switch
            {
                "MonsterAllDead" => new ConditionMonsterAllDead(),
                "AreaEnter" => new ConditionAreaEnter(),
                "AreaExit" => new ConditionAreaExit(),
                "AreaPlayerCount" => new ConditionAreaPlayerCount(),
                "AreaHoldBeats" => new ConditionAreaHoldBeats(),
                "TimeElapsed" => new ConditionTimeElapsed(),
                "ObjectInteracted" => new ConditionObjectInteracted(),
                "ObjectPairInteracted" => new ConditionObjectPairInteracted(),
                "ObjectStateEquals" => new ConditionObjectStateEquals(),
                _ => null
            };

            cond?.Init(data);
            return cond;
        }

        private static EventAction CreateAction(ActionData data)
        {
            if (data == null)
                return null;

            EventAction act = data.Type switch
            {
                "SpawnMonster" => new ActionSpawnMonster(),
                "SpawnObject" => new ActionSpawnObject(),
                "Broadcast" => new ActionBroadcast(),
                "ReturnToTown" => new ActionReturnToTown(),
                "FinGame" => new ActionFinGame(),
                "OpenGate" => new ActionOpenGate(),
                "ShowGuide" => new ActionShowGuide(),
                "ShowTutorialPanel" => new ActionShowTutorialPanel(),
                "HideTutorialPanel" => new ActionHideTutorialPanel(),
                "SetObjectState" => new ActionSetObjectState(),
                "RemoveEntityGroup" => new ActionRemoveEntityGroup(),
                "SetSceneObjectActive" => new ActionSetSceneObjectActive(),
                "SetSummonPortalActive" => new ActionSetSummonPortalActive(),
                "SetGateDoorOpen" => new ActionSetGateDoorOpen(),
                "PlayVfx" => new ActionPlayVfx(),
                _ => null
            };

            act?.Init(data);
            return act;
        }

        private sealed class ConditionMonsterAllDead : EventCondition
        {
            public override bool Check(IStageActionHost host, GameEventContext context)
            {
                int requiredGroupId = _data.TargetId;
                int requiredCount = _data.Count;
                return host.GetDeadMonsterCount(requiredGroupId) >= requiredCount;
            }
        }

        private sealed class ConditionAreaEnter : EventCondition
        {
            private readonly Dictionary<int, bool> _wasInsideByActor = new();

            public override bool Check(IStageActionHost host, GameEventContext context)
            {
                if (context.Type != EventType.Move)
                    return false;

                var r = _data.Area;
                if (r == null)
                    return false;

                int actorKey = context.SourceActorId;
                bool isInside = StageAreaUtility.Contains(r, context.X, context.Y);
                bool hadPrevious = _wasInsideByActor.TryGetValue(actorKey, out bool wasInside);
                _wasInsideByActor[actorKey] = isInside;
                return isInside && (!hadPrevious || !wasInside);
            }
        }

        private sealed class ConditionAreaExit : EventCondition
        {
            private readonly Dictionary<int, bool> _wasInsideByActor = new();

            public override bool Check(IStageActionHost host, GameEventContext context)
            {
                if (context.Type != EventType.Move)
                    return false;

                var r = _data.Area;
                if (r == null)
                    return false;

                int actorKey = context.SourceActorId;
                bool isInside = StageAreaUtility.Contains(r, context.X, context.Y);
                bool hadPrevious = _wasInsideByActor.TryGetValue(actorKey, out bool wasInside);
                _wasInsideByActor[actorKey] = isInside;
                return hadPrevious && wasInside && !isInside;
            }
        }

        private sealed class ConditionAreaPlayerCount : EventCondition
        {
            private int _lastCurrentCount = int.MinValue;
            private int _lastRequiredCount = int.MinValue;
            private bool _wasSatisfied;

            public override bool Check(IStageActionHost host, GameEventContext context)
            {
                if (context.Type != EventType.GameStart
                    && context.Type != EventType.Move
                    && context.Type != EventType.TimeTick)
                {
                    return false;
                }

                RectData area = _data.Area;
                if (area == null)
                    return false;

                int requiredCount = ResolveRequiredCount(host);
                int currentCount = host.CountAlivePlayersInArea(area);
                bool satisfied = currentCount >= requiredCount;

                if (_data.ShowProgressUi
                    && (currentCount != _lastCurrentCount || requiredCount != _lastRequiredCount))
                {
                    host.ShowAreaProgress(new StageAreaProgressData
                    {
                        Label = string.IsNullOrWhiteSpace(_data.ProgressLabel) ? "Area" : _data.ProgressLabel,
                        CurrentCount = currentCount,
                        RequiredCount = requiredCount,
                        DurationMs = _data.ProgressDurationMs > 0 ? _data.ProgressDurationMs : 1200,
                        ShowAreaOutline = _data.ShowAreaOutline && !satisfied,
                        Area = area
                    });
                }

                _lastCurrentCount = currentCount;
                _lastRequiredCount = requiredCount;

                bool justSatisfied = satisfied && !_wasSatisfied;
                _wasSatisfied = satisfied;
                return justSatisfied;
            }

            private int ResolveRequiredCount(IStageActionHost host)
            {
                if (_data.UseParticipantCount)
                    return Math.Max(1, host.GetParticipantPlayerCount());

                return Math.Max(1, _data.Count);
            }
        }

        private sealed class ConditionAreaHoldBeats : EventCondition
        {
            private int _heldBeats;
            private int _lastCurrent = int.MinValue;
            private int _lastRequired = int.MinValue;
            private bool _wasInside;
            private bool _completed;
            private bool _effectVisible;

            public override bool Check(IStageActionHost host, GameEventContext context)
            {
                if (context.Type != EventType.GameStart
                    && context.Type != EventType.Move
                    && context.Type != EventType.Beat
                    && context.Type != EventType.TimeTick)
                {
                    return false;
                }

                RectData area = _data.Area;
                if (area == null)
                    return false;

                int requiredBeats = Math.Max(1, _data.Count);
                int requiredPlayers = ResolveRequiredPlayers(host);
                int currentPlayers = host.CountAlivePlayersInArea(area);
                bool inside = currentPlayers >= requiredPlayers;

                if (!_completed && inside && context.Type == EventType.Beat)
                    _heldBeats = Math.Min(requiredBeats, _heldBeats + 1);

                _completed = _heldBeats >= requiredBeats;
                bool desiredEffectVisible = _completed || inside;
                if (desiredEffectVisible != _effectVisible || inside != _wasInside)
                {
                    SetLinkedEffect(host, desiredEffectVisible);
                    _effectVisible = desiredEffectVisible;
                }

                if (_data.ShowProgressUi
                    && (_heldBeats != _lastCurrent || requiredBeats != _lastRequired || inside != _wasInside || _completed))
                {
                    host.ShowAreaProgress(new StageAreaProgressData
                    {
                        Label = string.IsNullOrWhiteSpace(_data.ProgressLabel) ? "Hold" : _data.ProgressLabel,
                        CurrentCount = _heldBeats,
                        RequiredCount = requiredBeats,
                        DurationMs = _data.ProgressDurationMs > 0 ? _data.ProgressDurationMs : 1200,
                        ShowAreaOutline = _data.ShowAreaOutline && !_completed,
                        Area = area
                    });
                }

                _lastCurrent = _heldBeats;
                _lastRequired = requiredBeats;
                _wasInside = inside;
                return _completed;
            }

            private int ResolveRequiredPlayers(IStageActionHost host)
            {
                if (_data.UseParticipantCount)
                    return Math.Max(1, host.GetParticipantPlayerCount());

                return Math.Max(1, _data.TargetId > 0 ? _data.TargetId : 1);
            }

            private void SetLinkedEffect(IStageActionHost host, bool visible)
            {
                if (string.IsNullOrWhiteSpace(_data.TargetKey) && _data.SecondaryTargetId <= 0)
                    return;

                host.SetSceneObjectActive(new StageSceneObjectData
                {
                    TargetKey = _data.TargetKey ?? string.Empty,
                    GroupId = _data.SecondaryTargetId,
                    Visible = visible,
                    DurationMs = 220
                });
            }
        }

        private sealed class ConditionTimeElapsed : EventCondition
        {
            public override bool Check(IStageActionHost host, GameEventContext context)
                => host.GetElapsedTimeMs() >= _data.Count;
        }

        private sealed class ConditionObjectInteracted : EventCondition
        {
            private int _hitCount;

            public override bool Check(IStageActionHost host, GameEventContext context)
            {
                if (context.Type != EventType.Interact || !MatchesTarget(context.TargetId, _data.TargetId))
                    return false;

                _hitCount++;
                int requiredCount = _data.Count <= 0 ? 1 : _data.Count;
                return _hitCount >= requiredCount;
            }
        }

        private sealed class ConditionObjectPairInteracted : EventCondition
        {
            private readonly HashSet<int> _interactedTargets = new();

            public override bool Check(IStageActionHost host, GameEventContext context)
            {
                if (context.Type != EventType.Interact)
                    return false;

                if (MatchesTarget(context.TargetId, _data.TargetId)
                    || MatchesTarget(context.TargetId, _data.SecondaryTargetId))
                {
                    _interactedTargets.Add(context.TargetId);
                }

                return _data.TargetId > 0
                       && _data.SecondaryTargetId > 0
                       && _interactedTargets.Contains(_data.TargetId)
                       && _interactedTargets.Contains(_data.SecondaryTargetId);
            }
        }

        private sealed class ConditionObjectStateEquals : EventCondition
        {
            public override bool Check(IStageActionHost host, GameEventContext context)
            {
                if (_data.TargetId <= 0)
                    return false;

                return host.GetObjectState(_data.TargetId) == _data.Count;
            }
        }

        private static bool MatchesTarget(int actualTargetId, int configuredTargetId)
            => configuredTargetId <= 0 || actualTargetId == configuredTargetId;

        private sealed class ActionSpawnMonster : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                var spawnData = new SpawnData
                {
                    MonsterId = _data.ParamId,
                    X = _data.X,
                    Y = _data.Y,
                    Z = _data.Z,
                    AI = _data.StringVal,
                    GroupId = _data.GroupId
                };
                host.SpawnMonster(spawnData);
            }
        }

        private sealed class ActionSpawnObject : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                var spawnData = new SpawnObjectData
                {
                    EntityId = _data.ParamId,
                    EntityType = 3,
                    X = _data.X,
                    Y = _data.Y,
                    Z = _data.Z,
                    GroupId = _data.GroupId,
                    SizeX = Math.Max(1, _data.SizeX),
                    SizeY = Math.Max(1, _data.SizeY),
                    Pattern = _data.StringVal
                };
                host.SpawnObject(spawnData);
            }
        }

        private sealed class ActionBroadcast : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                host.BroadcastMessage(_data.StringVal);
            }
        }

        private sealed class ActionReturnToTown : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                host.ReturnToTown();
            }
        }

        private sealed class ActionFinGame : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                host.FinGame();
            }
        }

        private sealed class ActionOpenGate : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                host.OpenGate(_data.X, _data.Z);
            }
        }

        private sealed class ActionShowGuide : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                host.ShowGuide(new StageGuideData
                {
                    Title = _data.GuideTitle ?? string.Empty,
                    Body = FirstNonEmpty(_data.GuideBody, _data.StringVal),
                    ImageResource = _data.GuideImageResource ?? string.Empty,
                    DurationMs = _data.DurationMs > 0 ? _data.DurationMs : 3500
                });
            }
        }

        private sealed class ActionShowTutorialPanel : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                host.ShowTutorialPanel(new StageTutorialPanelData
                {
                    Visible = true,
                    PanelId = FirstNonEmpty(_data.StringVal, _data.GuideTitle),
                    ImageResource = _data.GuideImageResource ?? string.Empty,
                    FadeMs = _data.DurationMs > 0 ? _data.DurationMs : 220,
                    Width = _data.ParamId > 0 ? _data.ParamId : 900,
                    AnchorPreset = _data.GroupId,
                    OffsetX = _data.X,
                    OffsetY = _data.Y
                });
            }
        }

        private sealed class ActionHideTutorialPanel : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                host.HideTutorialPanel(new StageTutorialPanelData
                {
                    Visible = false,
                    PanelId = FirstNonEmpty(_data.StringVal, _data.GuideTitle),
                    FadeMs = _data.DurationMs > 0 ? _data.DurationMs : 180
                });
            }
        }

        private sealed class ActionSetObjectState : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                host.SetObjectState(_data.ParamId, _data.GroupId);
            }
        }

        private sealed class ActionRemoveEntityGroup : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                int groupId = _data.ParamId > 0 ? _data.ParamId : _data.GroupId;
                host.RemoveEntityGroup(groupId, Math.Max(0, _data.X));
            }
        }

        private sealed class ActionSetSceneObjectActive : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                host.SetSceneObjectActive(new StageSceneObjectData
                {
                    TargetKey = _data.StringVal ?? string.Empty,
                    GroupId = _data.ParamId,
                    Visible = _data.GroupId != 0,
                    DurationMs = _data.DurationMs > 0 ? _data.DurationMs : 650,
                    DelayMs = _data.X > 0 ? _data.X : 0
                });
            }
        }

        private sealed class ActionSetSummonPortalActive : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                host.SetSummonPortalActive(new StageSummonPortalData
                {
                    PortalKey = _data.StringVal ?? string.Empty,
                    Active = _data.GroupId != 0,
                    SpawnGroupId = _data.ParamId,
                    MaxAlive = Math.Max(1, _data.SizeX),
                    IntervalBeats = Math.Max(1, _data.DurationMs),
                    InitialDelayBeats = Math.Max(0, _data.SizeY),
                    SpawnX = _data.X,
                    SpawnY = _data.Y,
                    SpawnZ = _data.Z,
                    MonsterIdsCsv = _data.GuideTitle ?? string.Empty,
                    PatternKey = _data.GuideBody ?? string.Empty
                });
            }
        }

        private sealed class ActionSetGateDoorOpen : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                host.SetGateDoorOpen(new StageGateDoorData
                {
                    TargetKey = _data.StringVal ?? string.Empty,
                    GroupId = _data.ParamId,
                    Open = _data.GroupId != 0,
                    DurationMs = _data.DurationMs > 0 ? _data.DurationMs : 900,
                    AngleDegrees = _data.X
                });
            }
        }

        private sealed class ActionPlayVfx : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                host.PlayStageVfx(new StageVfxData
                {
                    VfxKey = FirstNonEmpty(_data.VfxKey, _data.StringVal),
                    X = _data.X,
                    Y = _data.Y,
                    Z = _data.Z,
                    DurationMs = _data.DurationMs,
                    TargetId = _data.ParamId,
                    SecondaryTargetId = _data.GroupId
                });
            }
        }

        private static string FirstNonEmpty(string preferred, string fallback)
            => string.IsNullOrWhiteSpace(preferred) ? (fallback ?? string.Empty) : preferred;
    }
}
