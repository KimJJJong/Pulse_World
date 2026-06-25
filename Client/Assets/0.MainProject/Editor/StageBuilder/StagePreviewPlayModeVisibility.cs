using UnityEngine;

namespace RhythmRPG.Editor.StageBuilder
{
    [ExecuteAlways]
    public sealed class StagePreviewPlayModeVisibility : MonoBehaviour
    {
        private bool _lastHidden;

        private void OnEnable()
        {
            ApplyVisibility();
        }

        private void Update()
        {
            ApplyVisibility();
        }

        private void OnDisable()
        {
            SetPreviewVisible(true);
        }

        private void ApplyVisibility()
        {
            bool shouldHide = Application.isPlaying;
            if (_lastHidden == shouldHide)
                return;

            _lastHidden = shouldHide;
            SetPreviewVisible(!shouldHide);
        }

        private void SetPreviewVisible(bool visible)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = visible;
            }

            var colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = visible;
            }
        }
    }
}
