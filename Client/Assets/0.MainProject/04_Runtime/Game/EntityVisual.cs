using UnityEngine;
using System.Collections;

public class EntityVisual : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator _animator;
    // [SerializeField] private SpriteRenderer _renderer; // 3D에서는 불필요

    // Optional: If you want to auto-find components on Awake
    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
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
            Debug.Log($"[EntityVisual] StartMove: Duration={duration:F2}s, Speed={_animator.speed:F2}");
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

    public void PlayAttack(float duration)
    {
        if (_animator != null)
        {
            // 공격 애니메이션도 박자에 맞춰 Speed 조절
            _animator.speed = 1.0f / duration; 
            _animator.SetTrigger("Attack");
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
