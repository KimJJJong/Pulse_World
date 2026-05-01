using GameShared.Data;
using ServerCore;
using System;
using System.Collections.Generic;

public sealed partial class P2PHostController
{
    private sealed class QueuedCombatCommand
    {
        public CS_ActionRequest Request;
        public int ActorId;
        public bool FromLocalSource;
        public bool BypassInputGuards;
        public bool InstantBroadcasted;
        public string SkillIdOverride = "";
        public long? ForcedExecuteBeat;
    }

    private sealed class PendingActionRequest
    {
        public CS_ActionRequest Request;
        public int ActorId;
        public bool FromLocalSource;
        public bool BypassInputGuards;
        public string SkillIdOverride = "";
        public long? ForcedExecuteBeat;
    }

    private sealed class BeatCommandScheduler
    {
        private readonly Dictionary<long, Dictionary<int, QueuedCombatCommand>> _byBeat = new();

        public void Enqueue(long beat, QueuedCombatCommand command)
        {
            if (!_byBeat.TryGetValue(beat, out var perActor))
            {
                perActor = new Dictionary<int, QueuedCombatCommand>(8);
                _byBeat[beat] = perActor;
            }

            int actorId = command?.ActorId ?? 0;
            perActor[actorId] = command;
        }

        public void PopActions(long beat, List<QueuedCombatCommand> output)
        {
            output.Clear();
            if (!_byBeat.TryGetValue(beat, out var perActor) || perActor.Count == 0)
                return;

            foreach (var command in perActor.Values)
                output.Add(command);

            output.Sort((a, b) =>
            {
                int kindA = a?.Request?.ActionKind ?? int.MaxValue;
                int kindB = b?.Request?.ActionKind ?? int.MaxValue;
                int cmp = kindA.CompareTo(kindB);
                if (cmp != 0)
                    return cmp;

                int actorA = a?.ActorId ?? int.MaxValue;
                int actorB = b?.ActorId ?? int.MaxValue;
                return actorA.CompareTo(actorB);
            });

            _byBeat.Remove(beat);
        }

        public void Clear()
        {
            _byBeat.Clear();
        }
    }

    private sealed class ScheduledSkill
    {
        public CS_ActionRequest Request;
        public int ActorId;
        public string SkillId = "";
        public NewSkillDef SkillDef;
        public long StartBeat;
        public long StartTick;
        public float Rotation;
        public bool FromLocalSource;
        public bool UseFallbackDamage;
        public int RemainingActionEvents;
        public int[] NextEventIndexByTrack = Array.Empty<int>();
    }

    private sealed class ScheduledEventEntry
    {
        public int TrackIndex;
        public int EventIndex;
        public int EventKey;
        public long TriggerBeat;
        public SkillEvent Event;
    }

    private struct PlayerCombatSnapshot
    {
        public int ActorId;
        public string Uid;
        public string NormalAttackSkillId;
        public string[] ActiveSkillSlots;
        public int TotalHp;
        public int TotalAtk;
        public int TotalDef;
        public int AppearanceId;
    }
}
