using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmRPG.Game.Visual.SceneEffects
{
    [DisallowMultipleComponent]
    public sealed class BlacksmithFurnaceFireEffect : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Light fireLight;
        [SerializeField] private Renderer[] flameRenderers = Array.Empty<Renderer>();
        [SerializeField] private ParticleSystem[] particleSystems = Array.Empty<ParticleSystem>();
        [SerializeField] private Color fireColor = new(1f, 0.42f, 0.08f, 1f);
        [SerializeField, Min(0f)] private float baseLightIntensity = 2.35f;
        [SerializeField, Min(0f)] private float lightIntensityVariance = 0.55f;
        [SerializeField, Min(0f)] private float baseLightRange = 4.2f;
        [SerializeField, Min(0f)] private float lightRangeVariance = 0.45f;
        [SerializeField, Min(0.1f)] private float flickerSpeed = 8.5f;
        [SerializeField, Range(0f, 0.2f)] private float flameScaleVariance = 0.07f;
        [SerializeField, Range(0f, 1f)] private float particleRateVariance = 0.22f;
        [SerializeField] private bool collectTargetsOnAwake = true;

        private readonly List<RendererState> _renderers = new();
        private readonly List<ParticleState> _particles = new();
        private MaterialPropertyBlock _propertyBlock;
        private float _noiseSeed;

        public void Configure(
            Light light,
            Renderer[] renderers,
            ParticleSystem[] particles,
            Color color,
            float lightIntensity,
            float lightRange)
        {
            fireLight = light;
            flameRenderers = Compact(renderers);
            particleSystems = Compact(particles);
            fireColor = color;
            baseLightIntensity = Mathf.Max(0f, lightIntensity);
            baseLightRange = Mathf.Max(0f, lightRange);
            collectTargetsOnAwake = false;
            RebuildCaches();
        }

        public void CollectTargetsFromChildren()
        {
            fireLight = GetComponentInChildren<Light>(true);
            flameRenderers = GetComponentsInChildren<Renderer>(true);
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            RebuildCaches();
        }

        private void Reset()
        {
            _noiseSeed = UnityEngine.Random.value * 100f;
            CollectTargetsFromChildren();
        }

        private void Awake()
        {
            if (_noiseSeed <= 0f)
            {
                _noiseSeed = UnityEngine.Random.value * 100f;
            }

            if (collectTargetsOnAwake && (fireLight == null || flameRenderers.Length == 0))
            {
                CollectTargetsFromChildren();
                return;
            }

            RebuildCaches();
        }

        private void OnEnable()
        {
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            if (_renderers.Count == 0 && flameRenderers.Length > 0)
            {
                RebuildCaches();
            }
        }

        private void OnDisable()
        {
            if (fireLight != null)
            {
                fireLight.intensity = baseLightIntensity;
                fireLight.range = baseLightRange;
            }

            for (var i = 0; i < _renderers.Count; i++)
            {
                _renderers[i].Restore();
            }

            for (var i = 0; i < _particles.Count; i++)
            {
                _particles[i].Restore();
            }
        }

        private void Update()
        {
            var time = Time.time * flickerSpeed;
            var softNoise = Mathf.PerlinNoise(_noiseSeed, time * 0.21f);
            var quickNoise = Mathf.PerlinNoise(_noiseSeed + 13.7f, time * 0.53f);
            var sine = Mathf.Sin((time + _noiseSeed) * 1.67f) * 0.5f + 0.5f;
            var flicker01 = Mathf.Clamp01((softNoise * 0.55f) + (quickNoise * 0.3f) + (sine * 0.15f));
            var centered = (flicker01 - 0.5f) * 2f;

            if (fireLight != null)
            {
                fireLight.intensity = Mathf.Max(0f, baseLightIntensity + centered * lightIntensityVariance);
                fireLight.range = Mathf.Max(0f, baseLightRange + centered * lightRangeVariance);
            }

            var emissionScale = Mathf.Lerp(2.75f, 5.25f, flicker01);
            var scale = 1f + centered * flameScaleVariance;
            for (var i = 0; i < _renderers.Count; i++)
            {
                _renderers[i].Apply(scale, fireColor * emissionScale, _propertyBlock);
            }

            var particleScale = 1f + centered * particleRateVariance;
            for (var i = 0; i < _particles.Count; i++)
            {
                _particles[i].Apply(particleScale);
            }
        }

        private void RebuildCaches()
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            _renderers.Clear();
            _particles.Clear();

            for (var i = 0; i < flameRenderers.Length; i++)
            {
                var rendererTarget = flameRenderers[i];
                if (rendererTarget != null)
                {
                    _renderers.Add(new RendererState(rendererTarget));
                }
            }

            for (var i = 0; i < particleSystems.Length; i++)
            {
                var particleSystem = particleSystems[i];
                if (particleSystem != null)
                {
                    _particles.Add(new ParticleState(particleSystem));
                }
            }
        }

        private static T[] Compact<T>(T[] source) where T : UnityEngine.Object
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<T>();
            }

            var list = new List<T>(source.Length);
            for (var i = 0; i < source.Length; i++)
            {
                if (source[i] != null)
                {
                    list.Add(source[i]);
                }
            }

            return list.ToArray();
        }

        private readonly struct RendererState
        {
            private readonly Renderer _renderer;
            private readonly Transform _transform;
            private readonly Vector3 _baseScale;

            public RendererState(Renderer renderer)
            {
                _renderer = renderer;
                _transform = renderer.transform;
                _baseScale = renderer.transform.localScale;
            }

            public void Apply(float scale, Color emissionColor, MaterialPropertyBlock block)
            {
                if (_renderer == null || _transform == null || block == null)
                {
                    return;
                }

                _transform.localScale = _baseScale * scale;
                _renderer.GetPropertyBlock(block);
                block.SetColor(EmissionColorId, emissionColor);
                _renderer.SetPropertyBlock(block);
            }

            public void Restore()
            {
                if (_transform != null)
                {
                    _transform.localScale = _baseScale;
                }
            }
        }

        private readonly struct ParticleState
        {
            private readonly ParticleSystem _particleSystem;
            private readonly float _baseRate;

            public ParticleState(ParticleSystem particleSystem)
            {
                _particleSystem = particleSystem;
                _baseRate = particleSystem.emission.rateOverTimeMultiplier;
            }

            public void Apply(float scale)
            {
                if (_particleSystem == null)
                {
                    return;
                }

                var emission = _particleSystem.emission;
                emission.rateOverTimeMultiplier = Mathf.Max(0f, _baseRate * scale);
            }

            public void Restore()
            {
                if (_particleSystem == null)
                {
                    return;
                }

                var emission = _particleSystem.emission;
                emission.rateOverTimeMultiplier = _baseRate;
            }
        }
    }
}
