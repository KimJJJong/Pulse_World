using UnityEngine;
using System.Collections;

public class EntityVisual : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator _animator;
    [SerializeField] private AudioSource _audioSource;

    [Header("SFX")]
    [SerializeField] private AudioClip _attackSound;
    [SerializeField] private AudioClip _skillSound;

    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
    }

    public void Bind(Animator animator)
    {
        _animator = animator;
    }

    public void SetRotation(float yAngle)
    {
        var e = transform.eulerAngles;
        e.y = yAngle;
        transform.eulerAngles = e;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  이동
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// start → end 로 duration 초 동안 이동.
    /// start 파라미터를 실제 출발점으로 사용한다.
    /// 플레이어 예측 이동은 현재 위치(transform.position)를 start로 넘기므로 스냅 없음.
    /// AI 이동은 serverFromW를 넘겨 정확한 출발 타일에서 시작한다.
    /// </summary>
    public void StartMove(Vector3 start, Vector3 end, float duration)
    {
        StopAllCoroutines();

        if (_animator != null)
        {
            _animator.speed = 1.0f / Mathf.Max(duration, 0.05f);
            _animator.SetBool("IsMoving", true);
        }

        StartCoroutine(CoMove(start, end, duration));
    }

    private IEnumerator CoMove(Vector3 start, Vector3 end, float duration)
    {
        float t = 0f;

        // ★ 핵심 수정: 현재 렌더 위치에서 부드럽게 보정.
        //   transform.position = start 를 제거해 순간이동 제거.
        //   start와 현재 위치 차이가 있어도 end로 부드럽게 수렴.
        Vector3 actualStart = transform.position;

        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / duration);
            // actualStart(현재 위치) → end 로 보간
            transform.position = Vector3.Lerp(actualStart, end, alpha);
            yield return null;
        }

        transform.position = end;

        if (_animator != null)
            _animator.SetBool("IsMoving", false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  충돌 피드백: 살짝 갔다가 퉁 튕기는 연출
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 이동이 거부된 방향으로 살짝 파고들었다가 원래 위치로 탄성 복귀.
    /// </summary>
    /// <param name="attemptedPos">이동하려 했던 방향(목표 월드 포지션)</param>
    /// <param name="returnPos">최종적으로 돌아가야 할 월드 포지션</param>
    /// <param name="bumpRatio">얼마나 파고들지 (0~1, 기본 0.35)</param>
    public void PlayBumpBack(Vector3 attemptedPos, Vector3 returnPos, float bumpRatio = 0.35f)
    {
        StopAllCoroutines();
        StartCoroutine(CoBumpBack(attemptedPos, returnPos, bumpRatio));
    }

    private IEnumerator CoBumpBack(Vector3 attemptedPos, Vector3 returnPos, float bumpRatio)
    {
        if (_animator != null)
        {
            _animator.speed = 1f;
            _animator.SetBool("IsMoving", true);
        }

        // Phase 1: 목표 방향으로 bumpRatio 만큼 파고들기 (0.07초)
        const float bumpInDuration  = 0.07f;
        const float bumpOutDuration = 0.13f;

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

        // Phase 2: 원래 위치로 SmoothStep 탄성 복귀 (0.13초)
        t = 0f;
        while (t < bumpOutDuration)
        {
            t += Time.deltaTime;
            float smooth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / bumpOutDuration));
            transform.position = Vector3.Lerp(bumpTarget, returnPos, smooth);
            yield return null;
        }
        transform.position = returnPos;

        if (_animator != null)
            _animator.SetBool("IsMoving", false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  전투
    // ─────────────────────────────────────────────────────────────────────────

    public void PlayAttack(float duration, bool isMine = false)
    {
        if (_animator != null)
        {
            _animator.speed = 2f;
            _animator.SetTrigger("Attack");
        }
        PlaySoundWithTimingLog(_attackSound, "Attack", isMine);
    }

    public void PlaySkill(float duration, bool isMine = false)
    {
        if (_animator != null)
        {
            _animator.speed = 2f;
            _animator.SetTrigger("Attack");
        }
        PlaySoundWithTimingLog(_skillSound, "Skill", isMine);
    }

    private void PlaySoundWithTimingLog(AudioClip clip, string actionName, bool isMine)
    {
        if (_audioSource != null && clip != null)
            _audioSource.PlayOneShot(clip);

        if (RhythmClient.Instance != null)
        {
            long serverNowMs  = RhythmClient.Instance.GetCurrentServerTimeMs();
            long nearestBeat  = RhythmClient.Instance.GetNearestBeatIndex(serverNowMs);
            long beatTimeMs   = RhythmClient.Instance.GetBeatTimeMs(nearestBeat);
            long diff         = serverNowMs - beatTimeMs;

            Debug.LogWarning($"[SFX Timing] {actionName} Diff to Peak: {diff}ms (Beat:{nearestBeat})");

            if (isMine && RhythmInputController.Instance != null)
            {
                long inputMs = RhythmInputController.LastAttackInputServerTimeMs;
                if (inputMs > 0)
                    Debug.LogWarning($"[SFX Timing] {actionName} Diff to Input: {serverNowMs - inputMs}ms");
            }
        }
    }

    public void PlayHit()
    {
        if (_animator != null) _animator.SetTrigger("Hit");
    }

    public void SetDie()
    {
        if (_animator != null) _animator.SetBool("IsDead", true);
    }
}
