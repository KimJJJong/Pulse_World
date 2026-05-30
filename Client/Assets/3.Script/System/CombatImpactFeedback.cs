using System.Collections;
using UnityEngine;

public class CombatImpactFeedback : MonoBehaviour
{
    public static CombatImpactFeedback Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = FindFirstObjectByType<CombatImpactFeedback>();
            if (_instance != null) return _instance;

            var go = new GameObject(nameof(CombatImpactFeedback));
            _instance = go.AddComponent<CombatImpactFeedback>();
            return _instance;
        }
    }

    private static CombatImpactFeedback _instance;

    [Header("Hit Stop")]
    [SerializeField] private float hitStopDuration = 0.055f;
    [SerializeField] private float hitStopTimeScale = 0f;
    [SerializeField] private float catchUpTimeScale = 1.35f;

    [Header("Camera Shake")]
    [SerializeField] private float shakeDuration = 0.12f;
    [SerializeField] private float shakeStrength = 0.11f;
    [SerializeField] private float shakeFrequency = 38f;

    private Coroutine _hitStopRoutine;
    private float _restoreTimeScale = 1f;
    private float _restoreFixedDeltaTime = 0.02f;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PlayLocalAttackImpact()
    {
        TriggerHitStop();
        TriggerCameraShake();
    }

    private void TriggerHitStop()
    {
        if (!Application.isPlaying) return;

        if (_hitStopRoutine == null)
        {
            if (Time.timeScale <= 0f) return;

            _restoreTimeScale = Time.timeScale;
            _restoreFixedDeltaTime = Time.fixedDeltaTime;
        }
        else
        {
            StopCoroutine(_hitStopRoutine);
        }

        _hitStopRoutine = StartCoroutine(CoHitStop());
    }

    private IEnumerator CoHitStop()
    {
        float baseTimeScale = Mathf.Max(0.01f, _restoreTimeScale);
        float baseFixedDeltaTime = Mathf.Max(0.0001f, _restoreFixedDeltaTime);

        ApplyTimeScale(Mathf.Max(0f, hitStopTimeScale), baseTimeScale, baseFixedDeltaTime);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, hitStopDuration));

        float catchUpScale = Mathf.Max(baseTimeScale, catchUpTimeScale);
        if (catchUpScale > baseTimeScale)
        {
            float catchUpDuration = hitStopDuration * baseTimeScale / (catchUpScale - baseTimeScale);
            ApplyTimeScale(catchUpScale, baseTimeScale, baseFixedDeltaTime);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, catchUpDuration));
        }

        ApplyTimeScale(_restoreTimeScale, baseTimeScale, baseFixedDeltaTime);
        Time.fixedDeltaTime = _restoreFixedDeltaTime;
        _hitStopRoutine = null;
    }

    private static void ApplyTimeScale(float timeScale, float baseTimeScale, float baseFixedDeltaTime)
    {
        Time.timeScale = timeScale;
        Time.fixedDeltaTime = Mathf.Max(0.0001f, baseFixedDeltaTime * (timeScale / baseTimeScale));
    }

    private void TriggerCameraShake()
    {
        CameraFollow follow = CameraBinder.Instance != null ? CameraBinder.Instance.Follow : null;

        if (follow == null && Camera.main != null)
            follow = Camera.main.GetComponent<CameraFollow>();

        if (follow == null)
            follow = FindFirstObjectByType<CameraFollow>();

        follow?.Shake(shakeDuration, shakeStrength, shakeFrequency);
    }
}
