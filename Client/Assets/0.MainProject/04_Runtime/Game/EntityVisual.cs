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

    // Optional: If you want to auto-find components on Awake
    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
    }

    public void Bind(Animator animator)
    {
        _animator = animator;
    }

    /// <summary>
    /// Moves the entity from start to end over duration.
    /// Handles animation state and rotation.
    /// </summary>
    public void StartMove(Vector3 start, Vector3 end, float duration)
    {
        // Cancel existing move if necessary? For now, we assume sequential actions or overwrite.
        StopAllCoroutines();
        
        // Animation
        if (_animator != null)
        {
            _animator.speed = 1.0f / duration;
            _animator.SetBool("IsMoving", true);
            //Debug.Log($"[EntityVisual] StartMove: Duration={duration:F2}s, Speed={_animator.speed:F2}");
        }

        // Rotation (LookAt)
        // [User Request] A/D/S 입력 시 화면이 돌아가는 현상(Entity 회전 -> 카메라 회전)을 막기 위해 회전 로직 제거
        /*
        Vector3 dir = (end - start).normalized;
        if (dir != Vector3.zero)
        {
            transform.LookAt(end); 
        }
        */

        StartCoroutine(CoMove(start, end, duration));
    }

    private IEnumerator CoMove(Vector3 start, Vector3 end, float duration)
    {
        float t = 0f;
        transform.position = start;

        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / duration);
            
            // [Fix] 매 프레임 Raycast는 떨림 유발 -> Start/End 높이를 믿고 보간만 수행
            transform.position = Vector3.Lerp(start, end, alpha);
            
            yield return null;
        }

        transform.position = end;

        // 도착 후 한 번만 보정하고 싶다면 여기서 수행 (옵션)
        // AdjustHeightToGround();

        if (_animator != null)
        {
            _animator.SetBool("IsMoving", false);
        }
    }

    public void PlayAttack(float duration, bool isMine = false)
    {
        if (_animator != null)
        {
            // 박자 계산 무시하고 항상 기본 속도(1.0f)로 고정 재생
            _animator.speed = 2f;
            _animator.SetTrigger("Attack");
        }
        PlaySoundWithTimingLog(_attackSound, "Attack", isMine);
    }
    public void PlaySkill(float duration, bool isMine = false)
    {
        if (_animator != null)
        {
            // 스킬 역시 박자 계산 무시하고 1배속으로 고정
            _animator.speed = 2f;
            _animator.SetTrigger("Attack"); // (스킬용 Trigger가 따로 없다면)
        }
        PlaySoundWithTimingLog(_skillSound, "Skill", isMine);
    }

    private void PlaySoundWithTimingLog(AudioClip clip, string actionName, bool isMine)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }

        // [User Request] Sound 실행 시점을 Peek와의 +-를 WarnignLogin로 클라에 출력
        if (RhythmClient.Instance != null)
        {
            long serverNowMs = RhythmClient.Instance.GetCurrentServerTimeMs();
            long nearestBeat = RhythmClient.Instance.GetNearestBeatIndex(serverNowMs);
            long beatTimeMs = RhythmClient.Instance.GetBeatTimeMs(nearestBeat);
            
            long diff = serverNowMs - beatTimeMs;
            
            // Peak(비트)보다 일찍 쳤으면 음수, 늦게 쳤으면 양수
            Debug.LogWarning($"[SFX Timing] {actionName} sound played. Diff to Peak: {diff}ms (Nearest Beat: {nearestBeat})");

            // [User Request] 플레이어의 Input 과 Sound play 의 Diff도 디버깅 추가
            if (isMine && RhythmInputController.Instance != null)
            {
                long inputMs = RhythmInputController.LastAttackInputServerTimeMs;
                if (inputMs > 0)
                {
                    long diffToInput = serverNowMs - inputMs;
                    Debug.LogWarning($"[SFX Timing] {actionName} sound played. Diff to Input: {diffToInput}ms");
                }
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
