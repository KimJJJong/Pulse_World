using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;        // PlayerActor
    public Vector3 offset = new Vector3(0, 2.0f, -2.0f);
    public float followSpeed = 10f;
    public float rotateSpeed = 10f;

    private Vector3 _shakeOffset;
    private float _shakeDuration;
    private float _shakeRemaining;
    private float _shakeStrength;
    private float _shakeFrequency;
    private float _shakeSeed;

    public void Shake(float duration, float strength, float frequency = 35f)
    {
        _shakeDuration = Mathf.Max(_shakeDuration, duration);
        _shakeRemaining = Mathf.Max(_shakeRemaining, duration);
        _shakeStrength = Mathf.Max(_shakeStrength, strength);
        _shakeFrequency = Mathf.Max(1f, frequency);
        _shakeSeed = Random.value * 1000f;
    }

    public void SnapToTarget()
    {
        if (target == null) return;

        Vector3 desiredPosition =
            target.position +
            target.rotation * offset;

        transform.position = desiredPosition + _shakeOffset;
        transform.rotation = Quaternion.LookRotation(
            target.position + Vector3.up * 1.5f - desiredPosition
        );
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 타겟 회전 기준 뒤쪽 위치 계산
        Vector3 desiredPosition =
            target.position +
            target.rotation * offset;

        // 2. 위치 보간
        Vector3 basePosition = transform.position - _shakeOffset;
        basePosition = Vector3.Lerp(
            basePosition,
            desiredPosition,
            followSpeed * Time.deltaTime
        );

        UpdateShakeOffset();
        transform.position = basePosition + _shakeOffset;

        // 3. 항상 타겟 바라보기
        Quaternion desiredRotation =
            Quaternion.LookRotation(
                target.position + Vector3.up * 1.5f - basePosition
            );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotateSpeed * Time.deltaTime
        );
    }

    private void UpdateShakeOffset()
    {
        if (_shakeRemaining <= 0f)
        {
            _shakeOffset = Vector3.zero;
            _shakeDuration = 0f;
            _shakeStrength = 0f;
            return;
        }

        _shakeRemaining = Mathf.Max(0f, _shakeRemaining - Time.unscaledDeltaTime);

        float duration = Mathf.Max(0.01f, _shakeDuration);
        float progress = 1f - Mathf.Clamp01(_shakeRemaining / duration);
        float damping = 1f - Mathf.SmoothStep(0f, 1f, progress);
        float noiseTime = Time.unscaledTime * _shakeFrequency;
        float x = Mathf.PerlinNoise(_shakeSeed, noiseTime) * 2f - 1f;
        float y = Mathf.PerlinNoise(_shakeSeed + 37.17f, noiseTime) * 2f - 1f;

        _shakeOffset = (transform.right * x + transform.up * y) * (_shakeStrength * damping);
    }
}
