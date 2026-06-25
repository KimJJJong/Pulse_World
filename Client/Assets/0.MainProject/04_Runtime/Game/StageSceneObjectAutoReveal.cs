using System.Collections;
using UnityEngine;

namespace RhythmRPG.Game.Stage
{
    public sealed class StageSceneObjectAutoReveal : MonoBehaviour
    {
        public StageSceneObjectTarget Target;
        public int DelayMs;
        public int DurationMs = 900;
        public bool ShakeCameraOnReveal;
        public float ShakeDelaySeconds;
        public float CameraShakeDuration = 0.55f;
        public float CameraShakeStrength = 0.085f;
        public float CameraShakeFrequency = 24f;

        private IEnumerator Start()
        {
            if (Target == null)
                Target = GetComponent<StageSceneObjectTarget>();

            yield return null;

            if (Target == null)
                yield break;

            int resolvedDelayMs = Mathf.Max(0, DelayMs);
            Target.SetVisible(true, DurationMs, resolvedDelayMs);

            if (!ShakeCameraOnReveal)
                yield break;

            float totalDelaySeconds = resolvedDelayMs / 1000f + Mathf.Max(0f, ShakeDelaySeconds);
            if (totalDelaySeconds > 0f)
                yield return new WaitForSeconds(totalDelaySeconds);

            TriggerCameraShake();
        }

        private void TriggerCameraShake()
        {
            global::CameraFollow follow = global::CameraBinder.Instance != null
                ? global::CameraBinder.Instance.Follow
                : null;

            if (follow == null && Camera.main != null)
                follow = Camera.main.GetComponent<global::CameraFollow>();

            if (follow == null)
                follow = FindFirstObjectByType<global::CameraFollow>();

            follow?.Shake(CameraShakeDuration, CameraShakeStrength, CameraShakeFrequency);
        }
    }
}
