using System;
using System.Collections.Generic;
using System.Linq;
using RhythmRPG.Editor.StageBuilder;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RhythmRPG.Editor
{
    public static class EntityAnimationProfileGenerator
    {
        private const string DataSearchRoot = "Assets/Resources/Data";
        private const string ProfileFolder = "Assets/Resources/Data/EntityAnimationProfiles";

        private static readonly string[] IdlePriority =
        {
            "IdleBattle", "IdleNormal", "Idle_B", "Idle", "IdleNormal 0", "pushed"
        };

        private static readonly string[] MovePriority =
        {
            "WalkFWD", "FlyFWD", "Run", "Walking_A", "FlyFWDFast", "Walk"
        };

        private static readonly string[] AttackPriority =
        {
            "Attack01", "Attack02", "Attack03", "Attack", "Melee_2H_Attack_Slice"
        };

        private static readonly string[] HitPriority =
        {
            "GetHit", "Hit_A", "Hit", "pushed"
        };

        private static readonly string[] DeathPriority =
        {
            "Die", "Death_A", "Death", "died"
        };

        private static readonly string[] GenericAttackSkillIds =
        {
            "Attack",
            "Skill"
        };

        private static readonly string[] EnemyAttackSkillIds =
        {
            "Enemy_Attack_Front",
            "Enemy_Attack_Cross",
            "Warning_Damage_Test"
        };

        private static readonly string[] EvilMageAttackSkillIds =
        {
            "Enemy_EvilMage_Attack"
        };

        [MenuItem("Tools/RhythmRPG/Generate Entity Animation Profiles")]
        public static void Generate()
        {
            EnsureFolder(ProfileFolder);

            string[] definitionGuids = AssetDatabase.FindAssets("t:EntityDefinitionSO", new[] { DataSearchRoot });
            int created = 0;
            int updated = 0;
            int skipped = 0;

            foreach (string guid in definitionGuids)
            {
                string definitionPath = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<EntityDefinitionSO>(definitionPath);
                if (definition == null || definition.Prefab == null)
                {
                    skipped++;
                    continue;
                }

                RuntimeAnimatorController controller = ResolveController(definition);
                if (controller == null)
                {
                    skipped++;
                    continue;
                }

                List<StateClipInfo> states = CollectStateClips(controller);
                if (states.Count == 0)
                {
                    skipped++;
                    continue;
                }

                StateClipInfo idle = PickBestState(states, IdlePriority, IsIdleName);
                StateClipInfo move = PickBestState(states, MovePriority, IsMoveName);
                StateClipInfo attack = PickBestState(states, AttackPriority, IsAttackName);
                StateClipInfo hit = PickBestState(states, HitPriority, IsHitName);
                StateClipInfo death = PickBestState(states, DeathPriority, IsDeathName);

                if (!idle.IsValid && !move.IsValid && !attack.IsValid && !hit.IsValid && !death.IsValid)
                {
                    skipped++;
                    continue;
                }

                string profilePath = $"{ProfileFolder}/Entity_{definition.EntityId}.asset";
                var profile = AssetDatabase.LoadAssetAtPath<EntityAnimationProfile>(profilePath);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<EntityAnimationProfile>();
                    AssetDatabase.CreateAsset(profile, profilePath);
                    created++;
                }
                else
                {
                    updated++;
                }

                AssignBaseStates(profile, idle, move, attack, hit, death);
                MergeDefaultAttackSkillBindings(profile, attack, definition);
                EditorUtility.SetDirty(profile);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EntityAnimationProfileGenerator] Created={created}, Updated={updated}, Skipped={skipped}, Folder={ProfileFolder}");
        }

        private static void AssignBaseStates(
            EntityAnimationProfile profile,
            StateClipInfo idle,
            StateClipInfo move,
            StateClipInfo attack,
            StateClipInfo hit,
            StateClipInfo death)
        {
            if (idle.IsValid)
            {
                profile.IdleState = ResolveStateName(idle);
                profile.IdleClip = idle.Clip;
            }

            if (move.IsValid)
            {
                profile.MoveState = ResolveStateName(move);
                profile.MoveClip = move.Clip;
            }

            if (attack.IsValid)
            {
                profile.AttackState = ResolveStateName(attack);
                profile.AttackClip = attack.Clip;
            }

            if (hit.IsValid)
            {
                profile.HitState = ResolveStateName(hit);
                profile.HitClip = hit.Clip;
            }

            if (death.IsValid)
            {
                profile.DeathState = ResolveStateName(death);
                profile.DeathClip = death.Clip;
            }

            profile.DefaultCrossFade = 0.05f;
            profile.FallbackDeathDuration = death.Clip != null
                ? Mathf.Max(0.05f, death.Clip.length)
                : Mathf.Max(0.05f, profile.FallbackDeathDuration);
        }

        private static void MergeDefaultAttackSkillBindings(
            EntityAnimationProfile profile,
            StateClipInfo attack,
            EntityDefinitionSO definition)
        {
            if (!attack.IsValid)
                return;

            profile.SkillAnimations ??= new List<EntityAnimationProfile.SkillAnimationBinding>();

            HashSet<string> expectedSkillIds = new HashSet<string>(
                GetDefaultAttackSkillIds(definition),
                StringComparer.OrdinalIgnoreCase);

            RemoveStaleGeneratedSkillBindings(profile, attack, expectedSkillIds);

            string stateName = ResolveStateName(attack);
            foreach (string skillId in expectedSkillIds)
            {
                var binding = profile.SkillAnimations.FirstOrDefault(candidate =>
                    candidate != null
                    && string.Equals(candidate.SkillId, skillId, StringComparison.OrdinalIgnoreCase));

                if (binding == null)
                {
                    profile.SkillAnimations.Add(new EntityAnimationProfile.SkillAnimationBinding
                    {
                        SkillId = skillId,
                        StateName = stateName,
                        Clip = attack.Clip,
                        CrossFade = -1f
                    });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(binding.StateName) && binding.Clip == null)
                {
                    binding.StateName = stateName;
                    binding.Clip = attack.Clip;
                    binding.CrossFade = -1f;
                }
            }
        }

        private static IEnumerable<string> GetDefaultAttackSkillIds(EntityDefinitionSO definition)
        {
            foreach (string skillId in GenericAttackSkillIds)
                yield return skillId;

            if (definition == null || definition.Type != StageBuilder.EntityType.Monster)
                yield break;

            foreach (string skillId in EnemyAttackSkillIds)
                yield return skillId;

            string name = $"{definition.EntityName} {definition.Prefab?.name}";
            if (ContainsName(name, "EvilMage"))
            {
                foreach (string skillId in EvilMageAttackSkillIds)
                    yield return skillId;
            }
        }

        private static void RemoveStaleGeneratedSkillBindings(
            EntityAnimationProfile profile,
            StateClipInfo attack,
            HashSet<string> expectedSkillIds)
        {
            string stateName = ResolveStateName(attack);
            for (int i = profile.SkillAnimations.Count - 1; i >= 0; i--)
            {
                var binding = profile.SkillAnimations[i];
                if (binding == null)
                {
                    profile.SkillAnimations.RemoveAt(i);
                    continue;
                }

                if (expectedSkillIds.Contains(binding.SkillId) || !IsKnownGeneratedSkillId(binding.SkillId))
                    continue;

                bool looksGenerated = binding.CrossFade < 0f
                                      && string.Equals(binding.StateName, stateName, StringComparison.OrdinalIgnoreCase)
                                      && binding.Clip == attack.Clip;

                if (looksGenerated)
                    profile.SkillAnimations.RemoveAt(i);
            }
        }

        private static bool IsKnownGeneratedSkillId(string skillId)
        {
            return GenericAttackSkillIds.Contains(skillId, StringComparer.OrdinalIgnoreCase)
                   || EnemyAttackSkillIds.Contains(skillId, StringComparer.OrdinalIgnoreCase)
                   || EvilMageAttackSkillIds.Contains(skillId, StringComparer.OrdinalIgnoreCase);
        }

        private static RuntimeAnimatorController ResolveController(EntityDefinitionSO definition)
        {
            var animator = definition.Prefab.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController != null)
                return animator.runtimeAnimatorController;

            return definition.AnimatorController;
        }

        private static List<StateClipInfo> CollectStateClips(RuntimeAnimatorController controller)
        {
            var states = new List<StateClipInfo>();
            if (controller == null)
                return states;

            if (controller is AnimatorOverrideController overrideController)
            {
                var overrideMap = BuildOverrideMap(overrideController);
                if (overrideController.runtimeAnimatorController is AnimatorController baseController)
                    CollectAnimatorController(baseController, states, overrideMap);
                else
                    AddClipsAsStates(controller.animationClips, states);
            }
            else if (controller is AnimatorController animatorController)
            {
                CollectAnimatorController(animatorController, states, null);
            }
            else
            {
                AddClipsAsStates(controller.animationClips, states);
            }

            return states;
        }

        private static Dictionary<AnimationClip, AnimationClip> BuildOverrideMap(AnimatorOverrideController overrideController)
        {
            var result = new Dictionary<AnimationClip, AnimationClip>();
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);

            foreach (var pair in overrides)
            {
                if (pair.Key != null && pair.Value != null)
                    result[pair.Key] = pair.Value;
            }

            return result;
        }

        private static void CollectAnimatorController(
            AnimatorController controller,
            List<StateClipInfo> states,
            IReadOnlyDictionary<AnimationClip, AnimationClip> overrideMap)
        {
            foreach (var layer in controller.layers)
            {
                if (layer.stateMachine != null)
                    CollectStateMachine(layer.stateMachine, states, overrideMap);
            }
        }

        private static void CollectStateMachine(
            AnimatorStateMachine stateMachine,
            List<StateClipInfo> states,
            IReadOnlyDictionary<AnimationClip, AnimationClip> overrideMap)
        {
            foreach (var childState in stateMachine.states)
            {
                var clips = new List<AnimationClip>();
                CollectClips(childState.state.motion, clips, overrideMap);

                if (clips.Count == 0)
                {
                    AddStateClip(states, childState.state.name, null);
                    continue;
                }

                foreach (AnimationClip clip in clips)
                    AddStateClip(states, childState.state.name, clip);
            }

            foreach (var childStateMachine in stateMachine.stateMachines)
            {
                if (childStateMachine.stateMachine != null)
                    CollectStateMachine(childStateMachine.stateMachine, states, overrideMap);
            }
        }

        private static void CollectClips(
            Motion motion,
            List<AnimationClip> clips,
            IReadOnlyDictionary<AnimationClip, AnimationClip> overrideMap)
        {
            if (motion == null)
                return;

            if (motion is AnimationClip clip)
            {
                if (overrideMap != null && overrideMap.TryGetValue(clip, out var overrideClip))
                    clip = overrideClip;

                if (clip != null && !clips.Contains(clip))
                    clips.Add(clip);
                return;
            }

            if (motion is BlendTree blendTree)
            {
                foreach (var child in blendTree.children)
                    CollectClips(child.motion, clips, overrideMap);
            }
        }

        private static void AddClipsAsStates(IReadOnlyList<AnimationClip> clips, List<StateClipInfo> states)
        {
            if (clips == null)
                return;

            foreach (AnimationClip clip in clips)
            {
                if (clip == null)
                    continue;

                AddStateClip(states, StripAssetPrefix(clip.name), clip);
                AddStateClip(states, clip.name, clip);
            }
        }

        private static void AddStateClip(List<StateClipInfo> states, string stateName, AnimationClip clip)
        {
            if (string.IsNullOrWhiteSpace(stateName) && clip == null)
                return;

            foreach (StateClipInfo existing in states)
            {
                if (existing.Clip == clip
                    && string.Equals(existing.StateName, stateName, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            states.Add(new StateClipInfo(stateName, clip));
        }

        private static StateClipInfo PickBestState(
            IReadOnlyList<StateClipInfo> states,
            IReadOnlyList<string> priorities,
            Func<string, bool> roleMatcher)
        {
            foreach (string priority in priorities)
            {
                StateClipInfo exactMatch = states.FirstOrDefault(state =>
                    EqualsName(state.StateName, priority)
                    || EqualsName(state.ClipName, priority)
                    || EqualsName(StripAssetPrefix(state.ClipName), priority));

                if (exactMatch.IsValid)
                    return exactMatch;
            }

            foreach (string priority in priorities)
            {
                StateClipInfo containsMatch = states.FirstOrDefault(state =>
                    ContainsName(state.StateName, priority)
                    || ContainsName(state.ClipName, priority)
                    || ContainsName(StripAssetPrefix(state.ClipName), priority));

                if (containsMatch.IsValid)
                    return containsMatch;
            }

            return states.FirstOrDefault(state =>
                roleMatcher(state.StateName)
                || roleMatcher(state.ClipName)
                || roleMatcher(StripAssetPrefix(state.ClipName)));
        }

        private static string ResolveStateName(StateClipInfo info)
        {
            if (!string.IsNullOrWhiteSpace(info.StateName))
                return info.StateName;

            return info.Clip != null ? StripAssetPrefix(info.Clip.name) : "";
        }

        private static bool IsIdleName(string value)
            => ContainsName(value, "Idle");

        private static bool IsMoveName(string value)
            => ContainsName(value, "Walk") || ContainsName(value, "Run") || ContainsName(value, "FlyFWD");

        private static bool IsAttackName(string value)
            => ContainsName(value, "Attack");

        private static bool IsHitName(string value)
            => ContainsName(value, "GetHit") || ContainsName(value, "Hit") || ContainsName(value, "pushed");

        private static bool IsDeathName(string value)
            => ContainsName(value, "Die") || ContainsName(value, "Death") || ContainsName(value, "died");

        private static bool EqualsName(string value, string other)
            => string.Equals(value, other, StringComparison.OrdinalIgnoreCase);

        private static bool ContainsName(string value, string part)
            => !string.IsNullOrWhiteSpace(value)
               && value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string StripAssetPrefix(string clipName)
        {
            if (string.IsNullOrWhiteSpace(clipName))
                return clipName;

            int underscoreIndex = clipName.LastIndexOf('_');
            if (underscoreIndex >= 0 && underscoreIndex < clipName.Length - 1)
                return clipName.Substring(underscoreIndex + 1);

            int atIndex = clipName.LastIndexOf('@');
            if (atIndex >= 0 && atIndex < clipName.Length - 1)
                return clipName.Substring(atIndex + 1);

            return clipName;
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

        private readonly struct StateClipInfo
        {
            public readonly string StateName;
            public readonly AnimationClip Clip;

            public StateClipInfo(string stateName, AnimationClip clip)
            {
                StateName = stateName;
                Clip = clip;
            }

            public bool IsValid => !string.IsNullOrWhiteSpace(StateName) || Clip != null;
            public string ClipName => Clip != null ? Clip.name : "";
        }
    }
}
