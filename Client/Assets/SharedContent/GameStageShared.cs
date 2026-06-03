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
        public const string GuidePrefix = "STAGE_GUIDE";
        public const string VfxPrefix = "STAGE_VFX";
        public const string StageClearPrefix = "STAGE_CLEAR";

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
    }

    [Serializable]
    public class RectData
    {
        public int X;
        public int Y;
        public int W;
        public int H;
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

        void SpawnMonster(SpawnData data);
        void SpawnObject(SpawnObjectData data);
        void BroadcastMessage(string msg);
        void ReturnToTown();
        void FinGame();
        void OpenGate(int x, int y);
        void SetObjectState(int targetId, int state);
        void ShowGuide(StageGuideData data);
        void PlayStageVfx(StageVfxData data);
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
                "SetObjectState" => new ActionSetObjectState(),
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
            public override bool Check(IStageActionHost host, GameEventContext context)
            {
                if (context.Type != EventType.Move)
                    return false;

                var r = _data.Area;
                if (r == null)
                    return false;

                return context.X >= r.X && context.X < r.X + r.W &&
                       context.Y >= r.Y && context.Y < r.Y + r.H;
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

        private sealed class ActionSetObjectState : EventAction
        {
            public override void Execute(IStageActionHost host)
            {
                host.SetObjectState(_data.ParamId, _data.GroupId);
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
