using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmRPG.Game.Visual.SceneEffects
{
    [DisallowMultipleComponent]
    public sealed class ForestBeatLightPulse : MonoBehaviour
    {
        [SerializeField] private Light[] targetLights = Array.Empty<Light>();
        [SerializeField] private Renderer[] targetRenderers = Array.Empty<Renderer>();
        [SerializeField] private ParticleSystem[] targetParticles = Array.Empty<ParticleSystem>();
        [SerializeField] private Color emissionColor = Color.white;
        [SerializeField] private bool tintRendererBaseColor;
        [SerializeField, Min(0f)] private float lightBaseMultiplier = 1f;
        [SerializeField, Min(0f)] private float rendererBaseMultiplier = 1f;
        [SerializeField, Min(0f)] private float particleBaseMultiplier = 1f;
        [SerializeField, Min(1f)] private float lightPeakMultiplier = 1.45f;
        [SerializeField, Min(1f)] private float emissionPeakMultiplier = 1.7f;
        [SerializeField, Min(1f)] private float alphaPeakMultiplier = 1.18f;
        [SerializeField, Min(1f)] private float particlePeakMultiplier = 1.35f;
        [SerializeField, Range(0.05f, 1f)] private float pulseDurationBeats = 0.48f;
        [SerializeField, Range(0.5f, 6f)] private float falloffPower = 2.3f;
        [SerializeField, Range(0f, 0.15f)] private float flickerAmount = 0.025f;
        [SerializeField, Min(0f)] private float flickerSpeed = 7f;
        [SerializeField, Range(0f, 1f)] private float beatOffsetBeats;
        [SerializeField, Min(1f)] private float fallbackBpm = 120f;
        [SerializeField] private bool useRhythmClient = true;
        [SerializeField] private bool collectTargetsOnAwake = true;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int GlowAlphaId = Shader.PropertyToID("_Alpha");

        private readonly List<LightState> _lights = new();
        private readonly List<RendererState> _renderers = new();
        private readonly List<ParticleState> _particles = new();
        private float _noiseSeed;

        public int LightTargetCount => targetLights?.Length ?? 0;
        public int RendererTargetCount => targetRenderers?.Length ?? 0;
        public int ParticleTargetCount => targetParticles?.Length ?? 0;
        public bool UseRhythmClient => useRhythmClient;
        public float FallbackBpm => fallbackBpm;

        public void Configure(
            Light[] lights,
            Renderer[] renderers,
            ParticleSystem[] particles,
            Color color,
            float lightPeak,
            float emissionPeak,
            float alphaPeak,
            float particlePeak,
            float durationBeats,
            float falloff,
            float flicker,
            float offsetBeats = 0f,
            float lightBase = 1f,
            float rendererBase = 1f,
            float particleBase = 1f,
            bool tintBaseColor = false)
        {
            targetLights = Compact(lights);
            targetRenderers = Compact(renderers);
            targetParticles = Compact(particles);
            emissionColor = color;
            tintRendererBaseColor = tintBaseColor;
            lightBaseMultiplier = Mathf.Max(0f, lightBase);
            rendererBaseMultiplier = Mathf.Max(0f, rendererBase);
            particleBaseMultiplier = Mathf.Max(0f, particleBase);
            lightPeakMultiplier = Mathf.Max(1f, lightPeak);
            emissionPeakMultiplier = Mathf.Max(1f, emissionPeak);
            alphaPeakMultiplier = Mathf.Max(1f, alphaPeak);
            particlePeakMultiplier = Mathf.Max(1f, particlePeak);
            pulseDurationBeats = Mathf.Clamp(durationBeats, 0.05f, 1f);
            falloffPower = Mathf.Clamp(falloff, 0.5f, 6f);
            flickerAmount = Mathf.Clamp(flicker, 0f, 0.15f);
            beatOffsetBeats = Mathf.Repeat(offsetBeats, 1f);
            collectTargetsOnAwake = false;

            RebuildCaches();
        }

        public void CollectTargetsFromChildren()
        {
            var lights = new List<Light>();
            foreach (var light in GetComponentsInChildren<Light>(true))
            {
                if (light != null && light.enabled && light.gameObject.activeInHierarchy)
                {
                    lights.Add(light);
                }
            }

            var renderers = new List<Renderer>();
            foreach (var rendererTarget in GetComponentsInChildren<Renderer>(true))
            {
                if (rendererTarget != null && rendererTarget.enabled && rendererTarget.gameObject.activeInHierarchy && HasBeatMaterial(rendererTarget))
                {
                    renderers.Add(rendererTarget);
                }
            }

            var particles = new List<ParticleSystem>();
            foreach (var particleTarget in GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particleTarget != null && particleTarget.gameObject.activeInHierarchy)
                {
                    particles.Add(particleTarget);
                }
            }

            targetLights = lights.ToArray();
            targetRenderers = renderers.ToArray();
            targetParticles = particles.ToArray();
            RebuildCaches();
        }

        public void ConfigureTiming(bool rhythmClientEnabled, float bpm)
        {
            useRhythmClient = rhythmClientEnabled;
            fallbackBpm = Mathf.Max(1f, bpm);
        }

        public void SetPulseColor(Color color, bool tintBaseColor = true)
        {
            emissionColor = color;
            tintRendererBaseColor = tintBaseColor;

            foreach (var light in Compact(targetLights))
            {
                if (light != null)
                    light.color = color;
            }

            foreach (var particleTarget in Compact(targetParticles))
            {
                if (particleTarget == null)
                    continue;

                var main = particleTarget.main;
                main.startColor = color;
            }

            if (tintRendererBaseColor)
            {
                EnsureTintableRendererTargets();
            }

            RebuildCaches();

            if (tintRendererBaseColor)
            {
                ApplyRendererColorImmediately();
            }
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

            if (collectTargetsOnAwake && IsTargetListEmpty())
            {
                CollectTargetsFromChildren();
            }
            else
            {
                RebuildCaches();
            }
        }

        private void OnEnable()
        {
            if (_lights.Count == 0 && _renderers.Count == 0 && _particles.Count == 0)
            {
                RebuildCaches();
            }
        }

        private void OnDisable()
        {
            RestoreBaseValues();
        }

        private void Update()
        {
            if (_lights.Count == 0 && _renderers.Count == 0 && _particles.Count == 0)
            {
                return;
            }

            var pulse = CalculatePulse01();
            var flicker = CalculateFlicker();
            var lightScale = lightBaseMultiplier * Mathf.Lerp(1f, lightPeakMultiplier, pulse) * flicker;
            var emissionScale = rendererBaseMultiplier * Mathf.Lerp(1f, emissionPeakMultiplier, pulse);
            var alphaScale = rendererBaseMultiplier * Mathf.Lerp(1f, alphaPeakMultiplier, pulse);
            var particleScale = particleBaseMultiplier * Mathf.Lerp(1f, particlePeakMultiplier, pulse);

            for (var i = 0; i < _lights.Count; i++)
            {
                _lights[i].Apply(lightScale);
            }

            for (var i = 0; i < _renderers.Count; i++)
            {
                _renderers[i].Apply(emissionColor, emissionScale, alphaScale);
            }

            for (var i = 0; i < _particles.Count; i++)
            {
                _particles[i].Apply(particleScale);
            }
        }

        private void RebuildCaches()
        {
            _lights.Clear();
            foreach (var light in Compact(targetLights))
            {
                _lights.Add(new LightState(light));
            }

            _renderers.Clear();
            foreach (var rendererTarget in Compact(targetRenderers))
            {
                _renderers.Add(new RendererState(rendererTarget, emissionColor, tintRendererBaseColor));
            }

            _particles.Clear();
            foreach (var particleTarget in Compact(targetParticles))
            {
                _particles.Add(new ParticleState(particleTarget));
            }
        }

        private void RestoreBaseValues()
        {
            for (var i = 0; i < _lights.Count; i++)
            {
                _lights[i].Restore();
            }

            for (var i = 0; i < _renderers.Count; i++)
            {
                _renderers[i].Restore(emissionColor);
            }

            for (var i = 0; i < _particles.Count; i++)
            {
                _particles[i].Restore();
            }
        }

        private bool IsTargetListEmpty()
        {
            return (targetLights == null || targetLights.Length == 0)
                && (targetRenderers == null || targetRenderers.Length == 0)
                && (targetParticles == null || targetParticles.Length == 0);
        }

        private void EnsureTintableRendererTargets()
        {
            var renderers = new List<Renderer>(Compact(targetRenderers));
            foreach (var rendererTarget in GetComponentsInChildren<Renderer>(true))
            {
                if (rendererTarget == null || renderers.Contains(rendererTarget))
                {
                    continue;
                }

                if (IsTintableCrystalRenderer(rendererTarget))
                {
                    renderers.Add(rendererTarget);
                }
            }

            targetRenderers = renderers.ToArray();
        }

        private void ApplyRendererColorImmediately()
        {
            var emissionScale = Mathf.Max(1f, rendererBaseMultiplier);
            var alphaScale = Mathf.Max(1f, rendererBaseMultiplier);
            for (var i = 0; i < _renderers.Count; i++)
            {
                _renderers[i].Apply(emissionColor, emissionScale, alphaScale);
            }
        }

        private float CalculatePulse01()
        {
            var progress = TryGetRhythmProgress(out var rhythmProgress)
                ? rhythmProgress
                : GetFallbackProgress();

            progress = Mathf.Repeat(progress - beatOffsetBeats, 1f);
            if (progress > pulseDurationBeats)
            {
                return 0f;
            }

            var normalized = Mathf.Clamp01(progress / pulseDurationBeats);
            return Mathf.Pow(1f - normalized, falloffPower);
        }

        private bool TryGetRhythmProgress(out float progress)
        {
            progress = 0f;
            if (!useRhythmClient || RhythmClient.Instance == null)
            {
                return false;
            }

            var beat = RhythmClient.Instance.GetCurrentBeatIndex();
            if (beat < 0)
            {
                return false;
            }

            progress = Mathf.Clamp01((float)RhythmClient.Instance.GetCurrentBeatProgress01());
            return true;
        }

        private float GetFallbackProgress()
        {
            var secondsPerBeat = 60f / Mathf.Max(1f, fallbackBpm);
            return Mathf.Repeat(Time.time, secondsPerBeat) / secondsPerBeat;
        }

        private float CalculateFlicker()
        {
            if (flickerAmount <= 0f || flickerSpeed <= 0f)
            {
                return 1f;
            }

            var noise = Mathf.PerlinNoise((Time.time + _noiseSeed) * flickerSpeed, 0f);
            return Mathf.Max(0f, 1f + (noise - 0.5f) * flickerAmount);
        }

        private static bool HasBeatMaterial(Renderer rendererTarget)
        {
            var materials = rendererTarget.sharedMaterials;
            if (materials == null)
            {
                return false;
            }

            foreach (var material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty(GlowIntensityId))
                {
                    return true;
                }

                if (material.HasProperty(EmissionColorId) && material.GetColor(EmissionColorId).maxColorComponent > 0.01f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTintableCrystalRenderer(Renderer rendererTarget)
        {
            if (rendererTarget is ParticleSystemRenderer || rendererTarget is TrailRenderer || rendererTarget is LineRenderer)
            {
                return false;
            }

            var materials = rendererTarget.sharedMaterials;
            return HasTintableColorProperty(materials)
                && (HasCrystalVisualName(rendererTarget.gameObject.name) || HasCrystalVisualMaterial(materials));
        }

        private static bool HasTintableColorProperty(Material[] materials)
        {
            if (materials == null)
            {
                return false;
            }

            foreach (var material in materials)
            {
                if (material != null
                    && (material.HasProperty(BaseColorId)
                        || material.HasProperty(ColorId)
                        || material.HasProperty(EmissionColorId)
                        || material.HasProperty(GlowColorId)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCrystalVisualMaterial(Material[] materials)
        {
            if (materials == null)
            {
                return false;
            }

            foreach (var material in materials)
            {
                if (material != null && HasCrystalVisualName(material.name))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCrystalVisualName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.IndexOf("Crystal", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Prism", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Gem", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Emerald", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static T[] Compact<T>(T[] values) where T : UnityEngine.Object
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<T>();
            }

            var compacted = new List<T>(values.Length);
            foreach (var value in values)
            {
                if (value != null && !compacted.Contains(value))
                {
                    compacted.Add(value);
                }
            }

            return compacted.ToArray();
        }

        private sealed class LightState
        {
            private readonly Light _light;
            private readonly float _baseIntensity;

            public LightState(Light light)
            {
                _light = light;
                _baseIntensity = light != null ? light.intensity : 0f;
            }

            public void Apply(float scale)
            {
                if (_light != null)
                {
                    _light.intensity = _baseIntensity * scale;
                }
            }

            public void Restore()
            {
                if (_light != null)
                {
                    _light.intensity = _baseIntensity;
                }
            }
        }

        private sealed class RendererState
        {
            private readonly Renderer _renderer;
            private readonly MaterialPropertyBlock _block = new();
            private readonly bool _tintBaseColor;
            private readonly bool _hasEmissionColor;
            private readonly bool _hasBaseColor;
            private readonly bool _hasColor;
            private readonly bool _hasBaseMap;
            private readonly bool _hasMainTex;
            private readonly bool _hasEmissionMap;
            private readonly bool _hasGlowColor;
            private readonly bool _hasGlowIntensity;
            private readonly bool _hasGlowAlpha;
            private readonly Color _baseColor = Color.white;
            private readonly Color _color = Color.white;
            private readonly Texture _baseMap;
            private readonly Texture _mainTex;
            private readonly Texture _emissionMap;
            private float _baseEmissionIntensity = 2f;
            private readonly float _baseGlowIntensity = 1f;
            private readonly float _baseGlowAlpha = 1f;

            public RendererState(Renderer renderer, Color fallbackColor, bool tintBaseColor)
            {
                _renderer = renderer;
                _tintBaseColor = tintBaseColor;
                _baseEmissionIntensity = Mathf.Max(1f, fallbackColor.maxColorComponent * 2f);

                var materials = renderer != null ? renderer.sharedMaterials : null;
                if (materials == null)
                {
                    return;
                }

                foreach (var material in materials)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    if (!_hasBaseColor && material.HasProperty(BaseColorId))
                    {
                        _hasBaseColor = true;
                        _baseColor = material.GetColor(BaseColorId);
                    }

                    if (!_hasColor && material.HasProperty(ColorId))
                    {
                        _hasColor = true;
                        _color = material.GetColor(ColorId);
                    }

                    if (!_hasBaseMap && material.HasProperty(BaseMapId))
                    {
                        var texture = material.GetTexture(BaseMapId);
                        if (texture != null)
                        {
                            _hasBaseMap = true;
                            _baseMap = texture;
                        }
                    }

                    if (!_hasMainTex && material.HasProperty(MainTexId))
                    {
                        var texture = material.GetTexture(MainTexId);
                        if (texture != null)
                        {
                            _hasMainTex = true;
                            _mainTex = texture;
                        }
                    }

                    if (!_hasEmissionMap && material.HasProperty(EmissionMapId))
                    {
                        var texture = material.GetTexture(EmissionMapId);
                        if (texture != null)
                        {
                            _hasEmissionMap = true;
                            _emissionMap = texture;
                        }
                    }

                    if (!_hasEmissionColor && material.HasProperty(EmissionColorId))
                    {
                        _hasEmissionColor = true;
                        var materialEmission = material.GetColor(EmissionColorId);
                        _baseEmissionIntensity = materialEmission.maxColorComponent > 0.001f
                            ? Mathf.Max(1f, materialEmission.maxColorComponent)
                            : Mathf.Max(1f, Hdr(fallbackColor, 2f).maxColorComponent);
                    }

                    if (!_hasGlowColor && material.HasProperty(GlowColorId))
                    {
                        _hasGlowColor = true;
                    }

                    if (!_hasGlowIntensity && material.HasProperty(GlowIntensityId))
                    {
                        _hasGlowIntensity = true;
                        _baseGlowIntensity = Mathf.Max(0f, material.GetFloat(GlowIntensityId));
                    }

                    if (!_hasGlowAlpha && material.HasProperty(GlowAlphaId))
                    {
                        _hasGlowAlpha = true;
                        _baseGlowAlpha = Mathf.Clamp01(material.GetFloat(GlowAlphaId));
                    }
                }
            }

            public void Apply(Color tint, float emissionScale, float alphaScale)
            {
                if (_renderer == null)
                {
                    return;
                }

                _renderer.GetPropertyBlock(_block);

                if (_tintBaseColor && _hasBaseColor)
                {
                    _block.SetColor(BaseColorId, TintColor(tint, _baseColor.a, alphaScale));
                }

                if (_tintBaseColor && _hasColor)
                {
                    _block.SetColor(ColorId, TintColor(tint, _color.a, alphaScale));
                }

                if (_tintBaseColor && (_hasBaseColor || _hasColor))
                {
                    ApplyTintTextures();
                }

                if (_hasEmissionColor)
                {
                    var emission = tint * _baseEmissionIntensity * emissionScale;
                    emission.a = tint.a;
                    _block.SetColor(EmissionColorId, emission);
                }

                if (_hasGlowColor)
                {
                    _block.SetColor(GlowColorId, tint);
                }

                if (_hasGlowIntensity)
                {
                    _block.SetFloat(GlowIntensityId, _baseGlowIntensity * emissionScale);
                }

                if (_hasGlowAlpha)
                {
                    _block.SetFloat(GlowAlphaId, Mathf.Clamp01(_baseGlowAlpha * alphaScale));
                }

                _renderer.SetPropertyBlock(_block);
            }

            public void Restore(Color tint)
            {
                Apply(tint, 1f, 1f);
                if (!_tintBaseColor || _renderer == null)
                {
                    return;
                }

                _renderer.GetPropertyBlock(_block);

                if (_hasBaseColor)
                {
                    _block.SetColor(BaseColorId, _baseColor);
                }

                if (_hasColor)
                {
                    _block.SetColor(ColorId, _color);
                }

                RestoreTintTextures();
                _renderer.SetPropertyBlock(_block);
            }

            private void ApplyTintTextures()
            {
                var whiteTexture = Texture2D.whiteTexture;

                if (_hasBaseMap)
                {
                    _block.SetTexture(BaseMapId, whiteTexture);
                }

                if (_hasMainTex)
                {
                    _block.SetTexture(MainTexId, whiteTexture);
                }

                if (_hasEmissionMap)
                {
                    _block.SetTexture(EmissionMapId, whiteTexture);
                }
            }

            private void RestoreTintTextures()
            {
                if (_hasBaseMap && _baseMap != null)
                {
                    _block.SetTexture(BaseMapId, _baseMap);
                }

                if (_hasMainTex && _mainTex != null)
                {
                    _block.SetTexture(MainTexId, _mainTex);
                }

                if (_hasEmissionMap && _emissionMap != null)
                {
                    _block.SetTexture(EmissionMapId, _emissionMap);
                }
            }

            private static Color Hdr(Color color, float exposureValue)
            {
                var hdr = color * Mathf.Pow(2f, exposureValue);
                hdr.a = 1f;
                return hdr;
            }

            private static Color TintColor(Color tint, float baseAlpha, float alphaScale)
            {
                return new Color(
                    Mathf.Clamp01(tint.r),
                    Mathf.Clamp01(tint.g),
                    Mathf.Clamp01(tint.b),
                    Mathf.Clamp01(baseAlpha * alphaScale));
            }
        }

        private sealed class ParticleState
        {
            private readonly ParticleSystem _particleSystem;
            private readonly float _baseEmissionMultiplier;

            public ParticleState(ParticleSystem particleSystem)
            {
                _particleSystem = particleSystem;
                if (particleSystem == null)
                {
                    return;
                }

                var emission = particleSystem.emission;
                _baseEmissionMultiplier = emission.rateOverTimeMultiplier;
            }

            public void Apply(float scale)
            {
                if (_particleSystem == null)
                {
                    return;
                }

                var emission = _particleSystem.emission;
                emission.rateOverTimeMultiplier = _baseEmissionMultiplier * scale;
            }

            public void Restore()
            {
                if (_particleSystem == null)
                {
                    return;
                }

                var emission = _particleSystem.emission;
                emission.rateOverTimeMultiplier = _baseEmissionMultiplier;
            }
        }
    }
}
