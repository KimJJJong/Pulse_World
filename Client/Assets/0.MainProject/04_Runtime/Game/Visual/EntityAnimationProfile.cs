using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EntityAnimationProfile", menuName = "RhythmRPG/Entity Animation Profile")]
public sealed class EntityAnimationProfile : ScriptableObject
{
    [Serializable]
    public sealed class SkillAnimationBinding
    {
        public string SkillId = "";
        public string StateName = "";
        public AnimationClip Clip;
        public float CrossFade = -1f;
    }

    [Header("Base States")]
    public string IdleState = "";
    public AnimationClip IdleClip;
    public string MoveState = "";
    public AnimationClip MoveClip;
    public string AttackState = "";
    public AnimationClip AttackClip;
    public string HitState = "";
    public AnimationClip HitClip;
    public string DeathState = "";
    public AnimationClip DeathClip;

    [Header("Timing")]
    [Range(0f, 0.25f)] public float DefaultCrossFade = 0.05f;
    [Min(0f)] public float FallbackDeathDuration = 0.45f;

    [Header("Skill Overrides")]
    public List<SkillAnimationBinding> SkillAnimations = new();

    public bool TryGetSkillAnimation(string skillId, out SkillAnimationBinding binding)
    {
        binding = null;
        if (string.IsNullOrWhiteSpace(skillId) || SkillAnimations == null)
            return false;

        for (int i = 0; i < SkillAnimations.Count; i++)
        {
            var candidate = SkillAnimations[i];
            if (candidate == null)
                continue;

            if (!string.Equals(candidate.SkillId, skillId, StringComparison.OrdinalIgnoreCase))
                continue;

            binding = candidate;
            return true;
        }

        return false;
    }
}
