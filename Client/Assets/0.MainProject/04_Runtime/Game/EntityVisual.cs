using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class EntityVisual : MonoBehaviour
{
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private const float FallbackAttackClipLengthSeconds = 1f;
    private const float FallbackMoveClipLengthSeconds = 1f;
    private const float FallbackHitClipLengthSeconds = 0.35f;
    private const float FallbackDeathClipLengthSeconds = 0.45f;
    private const float IdlePinCheckIntervalSeconds = 0.05f;
    private const float IdleReplayNormalizedThreshold = 0.95f;

    private static readonly string[] IdleStateCandidates =
    {
        "IdleBattle", "IdleNormal", "Idle_B", "Idle", "IdleNormal 0", "pushed"
    };

    private static readonly string[] MoveStateCandidates =
    {
        "WalkFWD", "FlyFWD", "Run", "Walking_A", "FlyFWDFast", "Walk"
    };

    private static readonly string[] AttackStateCandidates =
    {
        "Attack01", "Attack02", "Attack03", "Attack", "Melee_2H_Attack_Slice"
    };

    private static readonly string[] HitStateCandidates =
    {
        "GetHit", "Hit_A", "Hit", "pushed"
    };

    private static readonly string[] DeathStateCandidates =
    {
        "Die", "Death_A", "Death", "died"
    };

    [Header("Components")]
    [SerializeField] private Animator _animator;
    [SerializeField] private EntityAnimationProfile _animationProfile;
    [SerializeField, Range(0f, 0.25f)] private float _defaultCrossFade = 0.05f;
    // [Fix] AudioSource/AudioClip 하드코딩 사운드 제거 — 사운드는 ClientSkillRunner의 SoundAction이 담당
    private Coroutine _resetAnimatorSpeedCoroutine;
    private Coroutine _returnToIdleCoroutine;
    private RuntimeAnimatorController _cachedParameterController;
    private readonly HashSet<int> _animatorParameterHashes = new();
    private readonly List<StateCandidate> _stateCandidates = new();
    private bool _movementInProgress;
    private bool _isDead;
    private float _nextIdlePinCheckTime;
    private float _deathStartedAt;
    private float _deathDuration = FallbackDeathClipLengthSeconds;

    private enum AnimationRole
    {
        Idle,
        Move,
        Attack,
        Hit,
        Death
    }

    private readonly struct StateCandidate
    {
        public readonly string StateName;
        public readonly AnimationClip Clip;
        public readonly float CrossFade;

        public StateCandidate(string stateName, AnimationClip clip, float crossFade)
        {
            StateName = stateName;
            Clip = clip;
            CrossFade = crossFade;
        }
    }

    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
        EnsureIdlePose();
    }

    private void Update()
    {
        MaintainIdlePose();
    }

    public void Bind(Animator animator)
    {
        _animator = animator;
        _cachedParameterController = null;
        _animatorParameterHashes.Clear();
        EnsureIdlePose();
    }

    public void BindAnimationProfile(EntityAnimationProfile profile)
    {
        _animationProfile = profile;
        EnsureIdlePose();
    }

    public void SetRotation(float yAngle)
    {
        transform.rotation = Quaternion.Euler(0f, yAngle, 0f);
    }

    public bool IsDead => _isDead;

    public bool HasAnimator => _animator != null;

    // -------------------------------------------------------------------------
    //  이동
    // -------------------------------------------------------------------------

    /// <summary>
    /// start → end 로 duration 초 동안 이동.
    /// 플레이어 예측 이동은 현재 위치(transform.position)를 start로 넘기므로 스냅 없음.
    /// AI 이동은 serverFromW를 넘겨 정확한 출발 타일에서 시작한다.
    /// </summary>
    public void StartMove(Vector3 start, Vector3 end, float duration)
    {
        if (_isDead)
            return;

        StopVisualCoroutines();

        if (_animator != null)
        {
            duration = Mathf.Max(duration, 0.05f);
            if (SetAnimatorBoolIfExists(IsMovingHash, true))
            {
                _animator.speed = 1.0f / duration;
            }
            else if (TryPlayRoleState(AnimationRole.Move, null, duration, 0f, true, out _))
            {
                // Direct state playback is used for asset-store controllers without Animator parameters.
            }
        }

        _movementInProgress = true;
        StartCoroutine(CoMove(start, end, duration));
    }

    private IEnumerator CoMove(Vector3 start, Vector3 end, float duration)
    {
        float t = 0f;
        Vector3 actualStart = transform.position;

        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / duration);
            transform.position = Vector3.Lerp(actualStart, end, alpha);
            yield return null;
        }

        transform.position = end;
        _movementInProgress = false;

        if (_animator != null && !_isDead)
            ForceIdlePose(false, true);
    }

    // -------------------------------------------------------------------------
    //  충돌 피드백: 살짝 갔다가 퉁 튕기는 연출
    // -------------------------------------------------------------------------

    /// <summary>
    /// 이동이 거부된 방향으로 살짝 파고들었다가 원래 위치로 탄성 복귀.
    /// </summary>
    /// <param name="attemptedPos">이동하려 했던 방향(목표 월드 포지션)</param>
    /// <param name="returnPos">최종적으로 돌아가야 할 월드 포지션</param>
    /// <param name="bumpRatio">얼마나 파고들지 (0~1, 기본 0.35)</param>
    public void PlayBumpBack(Vector3 attemptedPos, Vector3 returnPos, float bumpRatio = 0.35f)
    {
        if (_isDead)
            return;

        StopVisualCoroutines();
        _movementInProgress = true;
        StartCoroutine(CoBumpBack(attemptedPos, returnPos, bumpRatio));
    }

    private IEnumerator CoBumpBack(Vector3 attemptedPos, Vector3 returnPos, float bumpRatio)
    {
        const float bumpInDuration  = 0.07f;
        const float bumpOutDuration = 0.13f;

        if (_animator != null)
        {
            _animator.speed = 1f;
            ForceIdlePose(false, true);
        }

        Vector3 startPos   = transform.position;
        Vector3 bumpTarget = Vector3.Lerp(returnPos, attemptedPos, bumpRatio);

        float t = 0f;
        while (t < bumpInDuration)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, bumpTarget, Mathf.Clamp01(t / bumpInDuration));
            yield return null;
        }
        transform.position = bumpTarget;

        t = 0f;
        while (t < bumpOutDuration)
        {
            t += Time.deltaTime;
            float smooth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / bumpOutDuration));
            transform.position = Vector3.Lerp(bumpTarget, returnPos, smooth);
            yield return null;
        }
        transform.position = returnPos;
        _movementInProgress = false;

        if (_animator != null && !_isDead)
            ForceIdlePose(false, true);
    }

    // -------------------------------------------------------------------------
    //  전투
    // -------------------------------------------------------------------------

    // [Fix] PlayAttack 제거 — Attack/Skill 모두 PlaySkill로 통일.
    // BoardView, ClientSkillRunner 등 호출부는 PlaySkill()만 사용하세요.

    /// <summary>
    /// 공격/스킬 애니메이션 재생. 사운드는 ClientSkillRunner의 SoundAction이 담당.
    /// </summary>
    public void PlaySkill(float duration, bool isMine = false, float normalizedStart = 0f)
        => PlaySkill(null, duration, isMine, normalizedStart);

    /// <summary>
    /// 공격/스킬 애니메이션 재생. skillId가 있으면 EntityAnimationProfile의 매핑을 우선 사용한다.
    /// </summary>
    public void PlaySkill(string skillId, float duration, bool isMine = false, float normalizedStart = 0f)
    {
        if (_isDead)
            return;

        if (_animator != null)
        {
            duration = Mathf.Max(duration, 0.05f);
            normalizedStart = Mathf.Clamp01(normalizedStart);

            StopAnimatorSpeedReset();
            StopReturnToIdle();
            SetAnimatorBoolIfExists(IsMovingHash, false);

            bool playedDirectly = TryPlayRoleState(
                AnimationRole.Attack,
                skillId,
                duration,
                normalizedStart,
                true,
                out float clipLength);

            if (!playedDirectly)
                _animator.speed = clipLength / duration;

            bool hasAttackTrigger = HasAnimatorParameter(AttackHash);
            if (hasAttackTrigger)
                _animator.ResetTrigger(AttackHash);

            if (!playedDirectly && hasAttackTrigger)
                _animator.SetTrigger(AttackHash);

            float remainingDuration = duration * (1f - normalizedStart);
            _resetAnimatorSpeedCoroutine = StartCoroutine(CoResetAnimatorSpeedAfter(remainingDuration));
        }
        // [Fix] 하드코딩 AudioClip 재생 제거 — FMOD SoundAction에서 전담 처리
        LogTimingIfMine("Skill", isMine);
    }

    private void StopVisualCoroutines()
    {
        StopAllCoroutines();
        _resetAnimatorSpeedCoroutine = null;
        _returnToIdleCoroutine = null;
        _movementInProgress = false;
    }

    private void StopAnimatorSpeedReset()
    {
        if (_resetAnimatorSpeedCoroutine == null)
            return;

        StopCoroutine(_resetAnimatorSpeedCoroutine);
        _resetAnimatorSpeedCoroutine = null;
    }

    private void StopReturnToIdle()
    {
        if (_returnToIdleCoroutine == null)
            return;

        StopCoroutine(_returnToIdleCoroutine);
        _returnToIdleCoroutine = null;
    }

    private IEnumerator CoResetAnimatorSpeedAfter(float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, delay));

        if (_animator != null && !_isDead)
            ForceIdlePose(false, true);

        _resetAnimatorSpeedCoroutine = null;
    }

    private void ReturnToIdleAfter(float delay)
    {
        StopReturnToIdle();
        _returnToIdleCoroutine = StartCoroutine(CoReturnToIdleAfter(delay));
    }

    private IEnumerator CoReturnToIdleAfter(float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, delay));

        if (_animator != null && !_isDead)
            ForceIdlePose(false, true);

        _returnToIdleCoroutine = null;
    }

    public void EnsureIdlePose()
    {
        ForceIdlePose(true, false);
    }

    private void ForceIdlePose(bool stopPendingCoroutines, bool useCrossFade)
    {
        if (_animator == null || _isDead)
            return;

        if (stopPendingCoroutines)
        {
            StopAnimatorSpeedReset();
            StopReturnToIdle();
        }

        ResetAnimatorTriggerIfExists(AttackHash);
        ResetAnimatorTriggerIfExists(HitHash);
        SetAnimatorBoolIfExists(IsMovingHash, false);
        _animator.speed = 1f;
        TryPlayRoleState(AnimationRole.Idle, null, 0f, 0f, useCrossFade, out _);
    }

    private void MaintainIdlePose()
    {
        if (_animator == null
            || _isDead
            || _movementInProgress
            || _resetAnimatorSpeedCoroutine != null
            || _returnToIdleCoroutine != null
            || Time.time < _nextIdlePinCheckTime)
        {
            return;
        }

        _nextIdlePinCheckTime = Time.time + IdlePinCheckIntervalSeconds;

        if (_animator.layerCount <= 0 || !TryGetRoleStateHash(AnimationRole.Idle, null, out int idleHash))
            return;

        if (_animator.IsInTransition(0))
        {
            ForceIdlePose(false, true);
            return;
        }

        var state = _animator.GetCurrentAnimatorStateInfo(0);
        bool isIdleState = state.fullPathHash == idleHash || state.shortNameHash == idleHash;
        if (!isIdleState)
        {
            ForceIdlePose(false, true);
            return;
        }

        if (!IsIdleClipLooping() && state.normalizedTime >= IdleReplayNormalizedThreshold)
            _animator.Play(idleHash, 0, 0f);
    }

    private float ResolveFallbackClipLength(AnimationRole role)
    {
        return role switch
        {
            AnimationRole.Move => FallbackMoveClipLengthSeconds,
            AnimationRole.Hit => FallbackHitClipLengthSeconds,
            AnimationRole.Death => ResolveFallbackDeathDuration(),
            _ => FallbackAttackClipLengthSeconds
        };
    }

    private float ResolveFallbackDeathDuration()
    {
        if (_animationProfile != null && _animationProfile.FallbackDeathDuration > 0f)
            return _animationProfile.FallbackDeathDuration;

        return FallbackDeathClipLengthSeconds;
    }

    private bool TryPlayRoleState(
        AnimationRole role,
        string skillId,
        float duration,
        float normalizedStart,
        bool useCrossFade,
        out float clipLength)
    {
        clipLength = ResolveFallbackClipLength(role);

        if (_animator == null || _animator.layerCount <= 0)
            return false;

        BuildStateCandidates(role, skillId);

        for (int i = 0; i < _stateCandidates.Count; i++)
        {
            var candidate = _stateCandidates[i];
            if (string.IsNullOrWhiteSpace(candidate.StateName))
                continue;

            if (!TryGetStateHash(candidate.StateName, out int stateHash))
                continue;

            clipLength = ResolveClipLength(role, candidate.StateName, candidate.Clip);
            if (duration > 0f)
                _animator.speed = clipLength / Mathf.Max(duration, 0.05f);
            else
                _animator.speed = 1f;

            float crossFade = ResolveCrossFade(candidate.CrossFade);
            if (useCrossFade && normalizedStart <= 0.001f && crossFade > 0f)
                _animator.CrossFade(stateHash, crossFade, 0, 0f);
            else
                _animator.Play(stateHash, 0, Mathf.Clamp01(normalizedStart));

            return true;
        }

        return false;
    }

    private bool TryGetRoleStateHash(AnimationRole role, string skillId, out int stateHash)
    {
        stateHash = 0;
        if (_animator == null || _animator.layerCount <= 0)
            return false;

        BuildStateCandidates(role, skillId);

        for (int i = 0; i < _stateCandidates.Count; i++)
        {
            var candidate = _stateCandidates[i];
            if (string.IsNullOrWhiteSpace(candidate.StateName))
                continue;

            if (TryGetStateHash(candidate.StateName, out stateHash))
                return true;
        }

        return false;
    }

    private bool IsIdleClipLooping()
    {
        if (_animationProfile != null && _animationProfile.IdleClip != null)
            return _animationProfile.IdleClip.isLooping;

        var controller = _animator != null ? _animator.runtimeAnimatorController : null;
        if (controller == null || controller.animationClips == null)
            return false;

        foreach (var clip in controller.animationClips)
        {
            if (clip != null && ClipMatchesRole(AnimationRole.Idle, clip.name))
                return clip.isLooping;
        }

        return false;
    }

    private void BuildStateCandidates(AnimationRole role, string skillId)
    {
        _stateCandidates.Clear();

        AddProfileCandidates(role, skillId);

        foreach (var stateName in GetDefaultStateCandidates(role))
            AddStateCandidate(stateName, null, -1f);

        AddControllerClipCandidates(role);
    }

    private void AddProfileCandidates(AnimationRole role, string skillId)
    {
        if (_animationProfile == null)
            return;

        if (role == AnimationRole.Attack
            && _animationProfile.TryGetSkillAnimation(skillId, out var skillBinding))
        {
            AddStateCandidate(ResolveStateName(skillBinding.StateName, skillBinding.Clip), skillBinding.Clip, skillBinding.CrossFade);
        }

        switch (role)
        {
            case AnimationRole.Idle:
                AddStateCandidate(ResolveStateName(_animationProfile.IdleState, _animationProfile.IdleClip), _animationProfile.IdleClip, -1f);
                break;
            case AnimationRole.Move:
                AddStateCandidate(ResolveStateName(_animationProfile.MoveState, _animationProfile.MoveClip), _animationProfile.MoveClip, -1f);
                break;
            case AnimationRole.Attack:
                AddStateCandidate(ResolveStateName(_animationProfile.AttackState, _animationProfile.AttackClip), _animationProfile.AttackClip, -1f);
                break;
            case AnimationRole.Hit:
                AddStateCandidate(ResolveStateName(_animationProfile.HitState, _animationProfile.HitClip), _animationProfile.HitClip, -1f);
                break;
            case AnimationRole.Death:
                AddStateCandidate(ResolveStateName(_animationProfile.DeathState, _animationProfile.DeathClip), _animationProfile.DeathClip, -1f);
                break;
        }
    }

    private void AddControllerClipCandidates(AnimationRole role)
    {
        var controller = _animator != null ? _animator.runtimeAnimatorController : null;
        if (controller == null || controller.animationClips == null)
            return;

        foreach (var clip in controller.animationClips)
        {
            if (clip == null || !ClipMatchesRole(role, clip.name))
                continue;

            AddStateCandidate(clip.name, clip, -1f);
            AddStateCandidate(StripAssetPrefix(clip.name), clip, -1f);
        }
    }

    private void AddStateCandidate(string stateName, AnimationClip clip, float crossFade)
    {
        if (string.IsNullOrWhiteSpace(stateName))
            return;

        for (int i = 0; i < _stateCandidates.Count; i++)
        {
            if (string.Equals(_stateCandidates[i].StateName, stateName, StringComparison.OrdinalIgnoreCase))
                return;
        }

        _stateCandidates.Add(new StateCandidate(stateName, clip, crossFade));
    }

    private float ResolveClipLength(AnimationRole role, string stateName, AnimationClip explicitClip)
    {
        if (explicitClip != null)
            return Mathf.Max(explicitClip.length, 0.05f);

        var controller = _animator != null ? _animator.runtimeAnimatorController : null;
        if (controller != null && controller.animationClips != null)
        {
            foreach (var clip in controller.animationClips)
            {
                if (clip == null)
                    continue;

                if (!ClipMatchesRole(role, clip.name))
                    continue;

                if (string.Equals(clip.name, stateName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(StripAssetPrefix(clip.name), stateName, StringComparison.OrdinalIgnoreCase))
                {
                    return Mathf.Max(clip.length, 0.05f);
                }
            }
        }

        return ResolveFallbackClipLength(role);
    }

    private bool TryGetStateHash(string stateName, out int stateHash)
    {
        stateHash = 0;
        if (_animator == null || string.IsNullOrWhiteSpace(stateName))
            return false;

        int fullPathHash = Animator.StringToHash($"Base Layer.{stateName}");
        if (_animator.HasState(0, fullPathHash))
        {
            stateHash = fullPathHash;
            return true;
        }

        int shortNameHash = Animator.StringToHash(stateName);
        if (_animator.HasState(0, shortNameHash))
        {
            stateHash = shortNameHash;
            return true;
        }

        return false;
    }

    private float ResolveCrossFade(float candidateCrossFade)
    {
        if (candidateCrossFade >= 0f)
            return candidateCrossFade;

        if (_animationProfile != null)
            return _animationProfile.DefaultCrossFade;

        return _defaultCrossFade;
    }

    private static string ResolveStateName(string stateName, AnimationClip clip)
    {
        if (!string.IsNullOrWhiteSpace(stateName))
            return stateName;

        return clip != null ? clip.name : "";
    }

    private static IReadOnlyList<string> GetDefaultStateCandidates(AnimationRole role)
    {
        return role switch
        {
            AnimationRole.Idle => IdleStateCandidates,
            AnimationRole.Move => MoveStateCandidates,
            AnimationRole.Hit => HitStateCandidates,
            AnimationRole.Death => DeathStateCandidates,
            _ => AttackStateCandidates
        };
    }

    private static bool ClipMatchesRole(AnimationRole role, string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return false;

        return role switch
        {
            AnimationRole.Idle => Contains(clipName, "Idle"),
            AnimationRole.Move => Contains(clipName, "Walk")
                                  || Contains(clipName, "Run")
                                  || Contains(clipName, "FlyFWD"),
            AnimationRole.Attack => Contains(clipName, "Attack"),
            AnimationRole.Hit => Contains(clipName, "GetHit")
                                 || Contains(clipName, "Hit")
                                 || Contains(clipName, "pushed"),
            AnimationRole.Death => Contains(clipName, "Die")
                                   || Contains(clipName, "Death")
                                   || Contains(clipName, "died"),
            _ => false
        };
    }

    private static bool Contains(string value, string part)
        => value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

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

    private void LogTimingIfMine(string actionName, bool isMine)
    {
        if (!isMine) return;
        if (RhythmClient.Instance == null) return;
        long serverNowMs = RhythmClient.Instance.GetCurrentServerTimeMs();
        long nearestBeat = RhythmClient.Instance.GetNearestBeatIndex(serverNowMs);
        long beatTimeMs  = RhythmClient.Instance.GetBeatTimeMs(nearestBeat);
        long diff        = serverNowMs - beatTimeMs;
        //Debug.LogWarning($"[SFX Timing] {actionName} Diff to Peak: {diff}ms (Beat:{nearestBeat})");

        if (RhythmInputController.Instance != null)
        {
            long inputMs = RhythmInputController.LastAttackInputServerTimeMs;
            if (inputMs > 0)
                Debug.LogWarning($"[SFX Timing] {actionName} Diff to Input: {serverNowMs - inputMs}ms");
        }
    }

    public void PlayHit()
        => PlayHit(FallbackHitClipLengthSeconds);

    public void PlayHit(float duration)
    {
        if (_isDead || _animator == null)
            return;

        duration = Mathf.Max(duration, 0.05f);
        StopAnimatorSpeedReset();
        StopReturnToIdle();
        _animator.speed = 1f;

        if (TryPlayRoleState(AnimationRole.Hit, null, duration, 0f, true, out _))
        {
            ReturnToIdleAfter(duration);
            return;
        }

        if (HasAnimatorParameter(HitHash))
        {
            _animator.SetTrigger(HitHash);
            ReturnToIdleAfter(duration);
        }
    }

    public void SetDie()
        => PlayDeath();

    public float PlayDeath()
    {
        if (_isDead)
            return GetRemainingDeathDelaySeconds();

        _isDead = true;
        _deathStartedAt = Time.time;
        _deathDuration = ResolveFallbackDeathDuration();

        StopVisualCoroutines();

        if (_animator != null)
        {
            SetAnimatorBoolIfExists(IsMovingHash, false);
            _animator.speed = 1f;
            SetAnimatorBoolIfExists(IsDeadHash, true);

            if (TryPlayRoleState(AnimationRole.Death, null, 0f, 0f, true, out float clipLength))
                _deathDuration = clipLength;
        }

        return _deathDuration;
    }

    public float GetRemainingDeathDelaySeconds()
    {
        if (!_isDead)
            return 0f;

        return Mathf.Max(0.05f, _deathDuration - (Time.time - _deathStartedAt));
    }

    private bool SetAnimatorBoolIfExists(int hash, bool value)
    {
        if (_animator == null || !HasAnimatorParameter(hash))
            return false;

        _animator.SetBool(hash, value);
        return true;
    }

    private void ResetAnimatorTriggerIfExists(int hash)
    {
        if (_animator == null || !HasAnimatorTriggerParameter(hash))
            return;

        _animator.ResetTrigger(hash);
    }

    private bool HasAnimatorTriggerParameter(int hash)
    {
        if (_animator == null)
            return false;

        foreach (var parameter in _animator.parameters)
        {
            if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Trigger)
                return true;
        }

        return false;
    }

    private bool HasAnimatorParameter(int hash)
    {
        if (_animator == null)
            return false;

        var controller = _animator.runtimeAnimatorController;
        if (_cachedParameterController != controller)
        {
            _cachedParameterController = controller;
            _animatorParameterHashes.Clear();

            foreach (var parameter in _animator.parameters)
                _animatorParameterHashes.Add(parameter.nameHash);
        }

        return _animatorParameterHashes.Contains(hash);
    }
}
