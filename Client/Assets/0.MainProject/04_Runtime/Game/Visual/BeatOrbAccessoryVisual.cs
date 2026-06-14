using UnityEngine;

namespace RhythmRPG.Visual
{
    public sealed class BeatOrbAccessoryVisual : MonoBehaviour
    {
        [SerializeField] private Transform orb;
        [SerializeField] private Transform glowVolume;
        [SerializeField] private Light orbLight;
        [SerializeField] private Renderer coreRenderer;
        [SerializeField] private Renderer glowRenderer;
        [SerializeField] private float radius = 0.52f;
        [SerializeField] private float height = 1.88f;
        [SerializeField] private float orbitDegreesPerSecond = 130f;
        [SerializeField] private float pulseSpeed = 3.0f;
        [SerializeField] private float pulseScale = 0.16f;
        [SerializeField] private float glowPulseScale = 0.08f;
        [SerializeField, Range(0.05f, 0.75f)] private float glowAlpha = 0.18f;

        private MaterialPropertyBlock _coreBlock;
        private MaterialPropertyBlock _glowBlock;
        private Vector3 _baseOrbScale = Vector3.one;
        private Vector3 _baseGlowScale = Vector3.one;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            if (orb == null)
                orb = transform.childCount > 0 ? transform.GetChild(0) : transform;

            if (coreRenderer == null && orb != null)
                coreRenderer = orb.GetComponent<Renderer>() != null ? orb.GetComponent<Renderer>() : orb.GetComponentInChildren<Renderer>();
            if (glowVolume == null && orb != null)
                glowVolume = orb.Find("GlowVolume");
            if (glowRenderer == null && glowVolume != null)
                glowRenderer = glowVolume.GetComponent<Renderer>();
            if (orbLight == null && orb != null)
                orbLight = orb.GetComponentInChildren<Light>();
            _baseOrbScale = orb != null ? orb.localScale : Vector3.one;
            _baseGlowScale = glowVolume != null ? glowVolume.localScale : Vector3.one;
            _coreBlock = new MaterialPropertyBlock();
            _glowBlock = new MaterialPropertyBlock();
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
            if (glowVolume != null)
                glowVolume.localScale = _baseGlowScale * (1f + pulse * glowPulseScale);

            if (orbLight != null)
            {
                orbLight.intensity = 0.02f + pulse * 0.05f;
                orbLight.range = 0.65f + pulse * 0.15f;
            }

            Color color = Color.Lerp(new Color(0.45f, 0.95f, 1f, 1f), new Color(1f, 0.88f, 0.38f, 1f), pulse);
            if (coreRenderer != null)
            {
                coreRenderer.GetPropertyBlock(_coreBlock);
                _coreBlock.SetColor(BaseColorId, color);
                _coreBlock.SetColor(ColorId, color);
                _coreBlock.SetColor(EmissionColorId, color * (0.9f + pulse * 1.25f));
                coreRenderer.SetPropertyBlock(_coreBlock);
            }

            if (glowRenderer != null)
            {
                Color glowColor = Color.Lerp(new Color(0.42f, 0.96f, 1f, glowAlpha * 0.72f), new Color(1f, 0.78f, 0.24f, glowAlpha), pulse);
                glowRenderer.GetPropertyBlock(_glowBlock);
                _glowBlock.SetColor(BaseColorId, glowColor);
                _glowBlock.SetColor(ColorId, glowColor);
                _glowBlock.SetColor(EmissionColorId, new Color(glowColor.r, glowColor.g, glowColor.b, 1f) * (1.0f + pulse * 0.9f));
                glowRenderer.SetPropertyBlock(_glowBlock);
            }
        }
    }
}
