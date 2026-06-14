using UnityEngine;

namespace RhythmRPG.Visual
{
    public sealed class BeatOrbAccessoryVisual : MonoBehaviour
    {
        [SerializeField] private Transform orb;
        [SerializeField] private Light orbLight;
        [SerializeField] private float radius = 0.38f;
        [SerializeField] private float height = 0.42f;
        [SerializeField] private float orbitDegreesPerSecond = 130f;
        [SerializeField] private float pulseSpeed = 3.0f;
        [SerializeField] private float pulseScale = 0.16f;

        private Renderer _renderer;
        private MaterialPropertyBlock _propertyBlock;
        private Vector3 _baseOrbScale = Vector3.one;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            if (orb == null)
                orb = transform.childCount > 0 ? transform.GetChild(0) : transform;

            _renderer = orb != null ? orb.GetComponentInChildren<Renderer>() : null;
            if (orbLight == null && orb != null)
                orbLight = orb.GetComponentInChildren<Light>();
            _baseOrbScale = orb != null ? orb.localScale : Vector3.one;
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (orb == null)
                return;

            float time = Time.time;
            float angle = time * orbitDegreesPerSecond * Mathf.Deg2Rad;
            float lift = Mathf.Sin(time * pulseSpeed * 1.7f) * 0.045f;
            orb.localPosition = new Vector3(Mathf.Cos(angle) * radius, height + lift, Mathf.Sin(angle) * radius);
            orb.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);

            float pulse = 0.5f + 0.5f * Mathf.Sin(time * pulseSpeed);
            float scale = 1f + pulse * pulseScale;
            orb.localScale = _baseOrbScale * scale;

            if (orbLight != null)
                orbLight.intensity = 0.7f + pulse * 1.1f;

            if (_renderer == null)
                return;

            Color color = Color.Lerp(new Color(0.45f, 0.95f, 1f, 1f), new Color(1f, 0.88f, 0.38f, 1f), pulse);
            _renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, color);
            _propertyBlock.SetColor(EmissionColorId, color * (1.2f + pulse * 1.8f));
            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
