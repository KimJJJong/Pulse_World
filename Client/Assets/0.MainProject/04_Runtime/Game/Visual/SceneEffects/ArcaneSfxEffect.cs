using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RhythmRPG.Game.Visual.SceneEffects
{
    public enum ArcaneSfxPreset
    {
        WaveringAbyssPortal,
        SparkleSigilRing,
        PortalCenterVortex
    }

    public enum ArcaneSfxPlane
    {
        XZ,
        XY
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ArcaneSfxEffect : MonoBehaviour
    {
        private const string GeneratedPrefix = "__ArcaneSfx_";
        private const int FullCircleSegments = 192;
        private const int ArcSegments = 44;
        private const int SpiralSegments = 128;

        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        [Header("Preset")]
        [SerializeField] private ArcaneSfxPreset preset = ArcaneSfxPreset.WaveringAbyssPortal;
        [SerializeField] private ArcaneSfxPlane plane = ArcaneSfxPlane.XZ;
        [SerializeField, Min(0.25f)] private float radius = 2.2f;
        [SerializeField, Min(0.05f)] private float playbackSpeed = 1f;

        [Header("Color")]
        [SerializeField] private Color primaryColor = new(0f, 1f, 0.72f, 1f);
        [SerializeField] private Color secondaryColor = new(0f, 0.43f, 0.34f, 0.7f);
        [SerializeField, Range(0.25f, 4f)] private float glowIntensity = 1.8f;

        [Header("Materials")]
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Material softParticleMaterial;
        [SerializeField] private Material starMaterial;
        [SerializeField] private Material darkCoreMaterial;

        [Header("Runtime")]
        [SerializeField] private bool rebuildInEditMode = true;
        [SerializeField] private bool playOnEnable = true;

        private readonly List<LineState> _lines = new();
        private readonly List<OrnamentState> _ornaments = new();
        private readonly List<ParticleSystem> _particleSystems = new();
        private MaterialPropertyBlock _propertyBlock;
        private Transform _visualRoot;
        private float _seed;
        private bool _dirty;

        public ArcaneSfxPreset Preset => preset;
        public float Radius => radius;

        public void Configure(
            ArcaneSfxPreset newPreset,
            ArcaneSfxPlane newPlane,
            float newRadius,
            Material newLineMaterial,
            Material newSoftParticleMaterial,
            Material newStarMaterial,
            Material newDarkCoreMaterial)
        {
            preset = newPreset;
            plane = newPlane;
            radius = Mathf.Max(0.25f, newRadius);
            lineMaterial = newLineMaterial;
            softParticleMaterial = newSoftParticleMaterial;
            starMaterial = newStarMaterial;
            darkCoreMaterial = newDarkCoreMaterial;

            if (preset == ArcaneSfxPreset.WaveringAbyssPortal)
            {
                primaryColor = new Color(0f, 1f, 0.72f, 1f);
                secondaryColor = new Color(0f, 0.38f, 0.32f, 0.72f);
                glowIntensity = 1.95f;
                playbackSpeed = 0.92f;
            }
            else if (preset == ArcaneSfxPreset.SparkleSigilRing)
            {
                primaryColor = new Color(0.08f, 1f, 0.58f, 1f);
                secondaryColor = new Color(0f, 0.64f, 0.46f, 0.68f);
                glowIntensity = 2.25f;
                playbackSpeed = 1.08f;
            }
            else
            {
                primaryColor = new Color(0.02f, 1f, 0.48f, 1f);
                secondaryColor = new Color(0f, 0.44f, 0.28f, 0.7f);
                glowIntensity = 2.35f;
                playbackSpeed = 1.18f;
            }

            _dirty = true;
        }

        [ContextMenu("Rebuild SFX")]
        public void Rebuild()
        {
            ClampValues();
            EnsureSeed();
            _propertyBlock ??= new MaterialPropertyBlock();

            _lines.Clear();
            _ornaments.Clear();
            _particleSystems.Clear();
            DestroyGeneratedChildren();

            _visualRoot = CreateChild("VisualRoot").transform;
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
            _visualRoot.localScale = Vector3.one;

            if (preset == ArcaneSfxPreset.WaveringAbyssPortal)
            {
                BuildWaveringPortal();
            }
            else if (preset == ArcaneSfxPreset.SparkleSigilRing)
            {
                BuildSparkleSigil();
            }
            else
            {
                BuildPortalCenterVortex();
            }

            if (playOnEnable)
            {
                PlayParticles();
            }

            _dirty = false;
        }

        public void Play()
        {
            gameObject.SetActive(true);
            PlayParticles();
        }

        public void Stop(bool clearParticles = false)
        {
            for (var i = 0; i < _particleSystems.Count; i++)
            {
                if (_particleSystems[i] != null)
                {
                    _particleSystems[i].Stop(true, clearParticles
                        ? ParticleSystemStopBehavior.StopEmittingAndClear
                        : ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        public void ClearGeneratedObjects()
        {
            _lines.Clear();
            _ornaments.Clear();
            _particleSystems.Clear();
            DestroyGeneratedChildren();
            _dirty = true;
        }

        private void Reset()
        {
            _seed = UnityEngine.Random.value * 1000f;
            _dirty = true;
        }

        private void Awake()
        {
            EnsureSeed();
        }

        private void OnEnable()
        {
            EnsureSeed();

            if (Application.isPlaying || rebuildInEditMode)
            {
                Rebuild();
            }
        }

        private void OnValidate()
        {
            ClampValues();
            _dirty = true;
        }

        private void OnDisable()
        {
            Stop();
        }

        private void Update()
        {
            if (_dirty && (Application.isPlaying || rebuildInEditMode))
            {
                Rebuild();
            }

            if (_lines.Count == 0 && _visualRoot == null && (Application.isPlaying || rebuildInEditMode))
            {
                Rebuild();
            }

            var time = GetEffectTime() * Mathf.Max(0.05f, playbackSpeed);
            UpdateLines(time);
            UpdateOrnaments(time);
        }

        private void BuildWaveringPortal()
        {
            CreateDisc("AbyssCore", radius * 0.83f, darkCoreMaterial, new Color(0f, 0f, 0.002f, 1f), 0);

            AddRing("OuterBreathingRim", radius * 1.0f, 0.095f, 0.98f, 0.11f, 3.2f, 0.18f, 0.78f, 125, true);
            AddRing("OuterMistRim", radius * 0.96f, 0.074f, 0.66f, 0.085f, 3.9f, -0.1f, 0.68f, 118, true);
            AddRing("MidBreathingRim", radius * 0.9f, 0.055f, 0.56f, 0.055f, 4.8f, 0.08f, 0.55f, 112, true);
            AddRing("InnerShadowTrace", radius * 0.74f, 0.018f, 0.2f, 0.018f, 5.6f, -0.04f, 0.36f, 80, false);

            for (var i = 0; i < 18; i++)
            {
                var t = i / 17f;
                var lineRadius = Mathf.Lerp(radius * 0.82f, radius * 1.03f, t);
                var width = Mathf.Lerp(0.012f, 0.043f, Mathf.PingPong(i * 0.41f, 1f));
                var alpha = Mathf.Lerp(0.12f, 0.42f, Mathf.PingPong(i * 0.31f, 1f));
                AddRing("LiquidThread" + i, lineRadius, width, alpha, 0.045f, 5.2f + i * 0.44f, (i % 2 == 0 ? 1f : -1f) * (0.052f + i * 0.006f), 0.58f, 68 + i, true);
            }

            for (var i = 0; i < 22; i++)
            {
                var start = i * 16.9f + Mathf.Sin(i * 1.83f) * 22f;
                var arc = Mathf.Lerp(34f, 112f, Mathf.PingPong(i * 0.37f, 1f));
                var lineRadius = Mathf.Lerp(radius * 0.82f, radius * 1.03f, Mathf.PingPong(i * 0.23f, 1f));
                AddArc("BrightCurrent" + i, lineRadius, start, arc, 0.014f + (i % 4) * 0.006f, 0.86f, 0.052f, 6.6f + i * 0.24f, 0.13f + i * 0.012f, 1.05f, 142 + i);
            }

            for (var i = 0; i < 14; i++)
            {
                var angle = i * 137.507f + Mathf.Sin(i * 0.87f) * 12f;
                var coreRadius = radius * Mathf.Sqrt(Mathf.Repeat(i * 0.3819f + 0.11f, 1f)) * 0.58f;
                var size = Mathf.Lerp(0.014f, 0.042f, Mathf.PingPong(i * 0.29f, 1f));
                var alpha = Mathf.Lerp(0.34f, 0.72f, Mathf.PingPong(i * 0.47f, 1f));
                AddOrnament("VoidStar" + i, OrnamentShape.Star, coreRadius, angle, size, alpha, 2.1f + (i % 5) * 0.22f, 0.31f + i * 0.41f, 205);
            }

            CreatePortalParticles();
            CreatePointLight("AbyssPortalGlow", radius * 2.35f, 1.65f);
        }

        private void BuildSparkleSigil()
        {
            AddRing("SigilOuter", radius * 1.035f, 0.036f, 0.94f, 0.005f, 0.5f, 0.014f, 0.48f, 130, true);
            AddRing("SigilMain", radius * 0.975f, 0.029f, 0.86f, 0.004f, 0.7f, -0.012f, 0.42f, 125, true);
            AddRing("SigilInnerEdge", radius * 0.91f, 0.02f, 0.58f, 0.003f, 0.9f, 0.01f, 0.34f, 115, true);
            AddRing("SigilFineOuter", radius * 1.085f, 0.013f, 0.42f, 0.002f, 0.6f, -0.017f, 0.28f, 105, true);
            AddRing("SigilFineInner", radius * 0.86f, 0.012f, 0.34f, 0.002f, 0.6f, 0.019f, 0.28f, 100, true);

            for (var i = 0; i < 24; i++)
            {
                var start = i * 15f + (i % 2) * 4.5f;
                var arc = i % 3 == 0 ? 10f : 16f;
                var lineRadius = i % 2 == 0 ? radius * 1.06f : radius * 0.93f;
                AddArc("SigilDash" + i, lineRadius, start, arc, 0.016f, 0.72f, 0.0015f, 0.2f, i % 2 == 0 ? 0.026f : -0.023f, 0.62f, 130 + i);
            }

            for (var i = 0; i < 12; i++)
            {
                var angle = i * 30f;
                AddRadialTick("SigilTick" + i, angle, radius * 0.9f, radius * 1.055f, i % 3 == 0 ? 0.023f : 0.014f, i % 3 == 0 ? 0.72f : 0.42f);
            }

            for (var i = 0; i < 8; i++)
            {
                var angle = i * 45f;
                AddOrnament("CardinalStar" + i, OrnamentShape.Star, radius * 1.035f, angle, 0.145f, 1f, 2.8f + i * 0.07f, 0.2f + i * 0.37f, 180);
                AddOrnament("InnerDiamond" + i, OrnamentShape.Diamond, radius * 0.92f, angle + 22.5f, 0.088f, 0.72f, 2.2f + i * 0.09f, 0.4f + i * 0.29f, 175);
            }

            for (var i = 0; i < 16; i++)
            {
                var angle = i * 22.5f + 11.25f;
                var ornamentRadius = i % 2 == 0 ? radius * 1.075f : radius * 0.985f;
                AddOrnament("OuterSpark" + i, OrnamentShape.Star, ornamentRadius, angle, 0.07f, 0.58f, 3.6f + (i % 4) * 0.23f, 0.9f + i * 0.19f, 180);
            }

            CreateSigilParticles();
            CreatePointLight("SparkleSigilGlow", radius * 2.65f, 1.35f);
        }

        private void BuildPortalCenterVortex()
        {
            CreateDisc("VortexVoid", radius * 0.68f, darkCoreMaterial, new Color(0f, 0f, 0.004f, 0.9f), 0);

            AddRing("VortexOuterMist", radius * 0.98f, 0.046f, 0.46f, 0.07f, 3.8f, 0.13f, 0.55f, 95, true);
            AddRing("VortexBrightLip", radius * 0.9f, 0.034f, 0.58f, 0.052f, 4.7f, -0.08f, 0.62f, 105, true);
            AddRing("VortexInnerShadow", radius * 0.46f, 0.018f, 0.22f, 0.028f, 5.8f, 0.05f, 0.38f, 70, false);

            for (var i = 0; i < 11; i++)
            {
                var armOffset = i * 32.7f + Mathf.Sin(i * 0.93f) * 9f;
                var outerRadius = radius * Mathf.Lerp(0.78f, 1.0f, Mathf.PingPong(i * 0.29f, 1f));
                var innerRadius = radius * Mathf.Lerp(0.1f, 0.27f, Mathf.PingPong(i * 0.43f, 1f));
                var turns = Mathf.Lerp(1.18f, 1.72f, Mathf.PingPong(i * 0.37f, 1f));
                var width = Mathf.Lerp(0.018f, 0.048f, Mathf.PingPong(i * 0.41f, 1f));
                var alpha = Mathf.Lerp(0.42f, 0.86f, Mathf.PingPong(i * 0.31f, 1f));
                AddSpiral(
                    "VortexArm" + i,
                    outerRadius,
                    innerRadius,
                    armOffset,
                    -turns,
                    width,
                    alpha,
                    0.042f,
                    4.8f + i * 0.35f,
                    0.09f + i * 0.012f,
                    0.9f,
                    140 + i,
                    true);
            }

            for (var i = 0; i < 8; i++)
            {
                var armOffset = i * 45f + 17f;
                AddSpiral(
                    "VortexSoftThread" + i,
                    radius * 0.92f,
                    radius * 0.18f,
                    armOffset,
                    -1.35f,
                    0.012f,
                    0.28f,
                    0.032f,
                    3.2f + i * 0.21f,
                    -0.045f - i * 0.008f,
                    0.5f,
                    100 + i,
                    false);
            }

            for (var i = 0; i < 38; i++)
            {
                var angle = i * 137.507f + Mathf.Sin(i * 0.63f) * 14f;
                var dotRadius = radius * Mathf.Sqrt(Mathf.Repeat(i * 0.618f + 0.08f, 1f)) * 0.95f;
                var size = Mathf.Lerp(0.012f, 0.05f, Mathf.PingPong(i * 0.33f, 1f));
                var alpha = Mathf.Lerp(0.28f, 0.88f, Mathf.PingPong(i * 0.47f, 1f));
                AddOrnament("VortexSpark" + i, OrnamentShape.Star, dotRadius, angle, size, alpha, 2.4f + (i % 6) * 0.19f, 0.17f + i * 0.36f, 210);
            }

            CreateVortexParticles();
            CreatePointLight("PortalCenterVortexGlow", radius * 2.05f, 1.25f);
        }

        private void AddRing(
            string name,
            float lineRadius,
            float width,
            float alpha,
            float waveAmplitude,
            float waveFrequency,
            float angularSpeed,
            float pulse,
            int sortingOrder,
            bool usePrimary)
        {
            var renderer = CreateLineRenderer(name, true, FullCircleSegments, width, sortingOrder);
            _lines.Add(new LineState(
                renderer,
                lineRadius,
                0f,
                360f,
                0f,
                1f,
                width,
                alpha,
                waveAmplitude,
                waveFrequency,
                angularSpeed,
                pulse,
                usePrimary,
                true,
                _seed + _lines.Count * 13.31f));
        }

        private void AddArc(
            string name,
            float lineRadius,
            float startAngle,
            float arcDegrees,
            float width,
            float alpha,
            float waveAmplitude,
            float waveFrequency,
            float angularSpeed,
            float pulse,
            int sortingOrder)
        {
            var renderer = CreateLineRenderer(name, false, ArcSegments, width, sortingOrder);
            _lines.Add(new LineState(
                renderer,
                lineRadius,
                startAngle,
                arcDegrees,
                0f,
                1f,
                width,
                alpha,
                waveAmplitude,
                waveFrequency,
                angularSpeed,
                pulse,
                true,
                false,
                _seed + _lines.Count * 9.17f));
        }

        private void AddSpiral(
            string name,
            float outerRadius,
            float innerRadius,
            float startAngle,
            float turns,
            float width,
            float alpha,
            float waveAmplitude,
            float waveFrequency,
            float angularSpeed,
            float pulse,
            int sortingOrder,
            bool usePrimary)
        {
            var renderer = CreateLineRenderer(name, false, SpiralSegments, width, sortingOrder);
            _lines.Add(new LineState(
                renderer,
                outerRadius,
                startAngle,
                turns * 360f,
                innerRadius - outerRadius,
                0.78f,
                width,
                alpha,
                waveAmplitude,
                waveFrequency,
                angularSpeed,
                pulse,
                usePrimary,
                false,
                _seed + _lines.Count * 7.73f));
        }

        private void AddRadialTick(string name, float angle, float innerRadius, float outerRadius, float width, float alpha)
        {
            var renderer = CreateLineRenderer(name, false, 2, width, 150);
            var radians = angle * Mathf.Deg2Rad;
            renderer.SetPosition(0, ToPlane(Mathf.Cos(radians) * innerRadius, Mathf.Sin(radians) * innerRadius));
            renderer.SetPosition(1, ToPlane(Mathf.Cos(radians) * outerRadius, Mathf.Sin(radians) * outerRadius));
            ApplyLineGradient(renderer, Hdr(primaryColor, glowIntensity), alpha, false);
        }

        private LineRenderer CreateLineRenderer(string name, bool loop, int positions, float width, int sortingOrder)
        {
            var go = CreateChild(name);
            var renderer = go.AddComponent<LineRenderer>();
            renderer.sharedMaterial = lineMaterial;
            renderer.useWorldSpace = false;
            renderer.loop = loop;
            renderer.positionCount = Mathf.Max(2, positions);
            renderer.widthMultiplier = width;
            renderer.numCapVertices = 4;
            renderer.numCornerVertices = 4;
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.alignment = LineAlignment.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
            ApplyLineGradient(renderer, Hdr(primaryColor, glowIntensity), 0.75f, !loop);
            return renderer;
        }

        private void CreateDisc(string name, float discRadius, Material material, Color color, int sortingOrder)
        {
            if (material == null)
            {
                return;
            }

            var go = CreateChild(name);
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            filter.sharedMesh = CreateDiscMesh(discRadius, 128);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(TintColorId, color);
            _propertyBlock.SetFloat(AlphaId, color.a);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private void AddOrnament(
            string name,
            OrnamentShape shape,
            float ornamentRadius,
            float angle,
            float size,
            float alpha,
            float twinkleSpeed,
            float phase,
            int sortingOrder = 180)
        {
            var go = CreateChild(name);
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            filter.sharedMesh = shape == OrnamentShape.Star ? CreateStarMesh(size, size * 0.34f) : CreateDiamondMesh(size);
            renderer.sharedMaterial = starMaterial != null ? starMaterial : lineMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;

            go.transform.localPosition = Polar(ornamentRadius, angle);
            go.transform.localRotation = RotationAroundNormal(angle);
            go.transform.localScale = Vector3.one;

            _ornaments.Add(new OrnamentState(
                renderer,
                go.transform,
                Vector3.one,
                Hdr(primaryColor, glowIntensity * 1.15f),
                alpha,
                twinkleSpeed,
                phase));
        }

        private void CreatePortalParticles()
        {
            CreateParticleSystem(
                "AbyssDust",
                radius * 0.6f,
                38f,
                new ParticleSystem.MinMaxCurve(1.4f, 3.0f),
                new ParticleSystem.MinMaxCurve(0.012f, 0.046f),
                new ParticleSystem.MinMaxGradient(
                    WithAlpha(primaryColor, 0.56f),
                    WithAlpha(Color.white, 0.84f)),
                false);

            CreateParticleSystem(
                "RimGlints",
                radius * 0.98f,
                18f,
                new ParticleSystem.MinMaxCurve(0.55f, 1.25f),
                new ParticleSystem.MinMaxCurve(0.035f, 0.105f),
                new ParticleSystem.MinMaxGradient(
                    WithAlpha(primaryColor, 0.75f),
                    WithAlpha(Color.white, 0.9f)),
                true);
        }

        private void CreateSigilParticles()
        {
            CreateParticleSystem(
                "SigilTwinkles",
                radius * 1.035f,
                28f,
                new ParticleSystem.MinMaxCurve(0.45f, 1.1f),
                new ParticleSystem.MinMaxCurve(0.032f, 0.095f),
                new ParticleSystem.MinMaxGradient(
                    WithAlpha(primaryColor, 0.8f),
                    WithAlpha(Color.white, 0.95f)),
                true);
        }

        private void CreateVortexParticles()
        {
            CreateParticleSystem(
                "VortexEmeraldDust",
                radius * 0.86f,
                46f,
                new ParticleSystem.MinMaxCurve(0.75f, 1.9f),
                new ParticleSystem.MinMaxCurve(0.014f, 0.055f),
                new ParticleSystem.MinMaxGradient(
                    WithAlpha(primaryColor, 0.62f),
                    WithAlpha(Color.white, 0.9f)),
                false);

            CreateParticleSystem(
                "VortexOuterSparks",
                radius * 0.98f,
                24f,
                new ParticleSystem.MinMaxCurve(0.42f, 1.05f),
                new ParticleSystem.MinMaxCurve(0.024f, 0.086f),
                new ParticleSystem.MinMaxGradient(
                    WithAlpha(primaryColor, 0.72f),
                    WithAlpha(Color.white, 0.95f)),
                true);
        }

        private void CreateParticleSystem(
            string name,
            float particleRadius,
            float rate,
            ParticleSystem.MinMaxCurve lifetime,
            ParticleSystem.MinMaxCurve size,
            ParticleSystem.MinMaxGradient color,
            bool ringShape)
        {
            var go = CreateChild(name);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.duration = 2.5f;
            main.startLifetime = lifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.015f, 0.08f);
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake = playOnEnable;
            main.maxParticles = ringShape ? 160 : 90;

            var emission = particles.emission;
            emission.rateOverTime = rate;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = particleRadius;
            shape.radiusThickness = ringShape ? 0.12f : 1f;
            shape.arc = 360f;
            shape.rotation = plane == ArcaneSfxPlane.XZ ? new Vector3(90f, 0f, 0f) : Vector3.zero;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateParticleFadeGradient(primaryColor));

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.35f, 1f, 0f));

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(ringShape ? 0.11f : 0.06f);
            noise.frequency = ringShape ? 0.82f : 0.54f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.42f);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ringShape && starMaterial != null ? starMaterial : softParticleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = ringShape ? 220 : 190;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _particleSystems.Add(particles);
        }

        private void CreatePointLight(string name, float range, float intensity)
        {
            var go = CreateChild(name);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = primaryColor;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        private void UpdateLines(float time)
        {
            for (var i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                if (line.Renderer == null)
                {
                    continue;
                }

                var count = line.Renderer.positionCount;
                var divisor = line.Loop ? count : Mathf.Max(1, count - 1);
                var startAngle = line.StartAngle + time * line.AngularSpeed * 360f;
                var wavePhase = time * (line.WaveFrequency * 0.45f) + line.Phase;
                for (var p = 0; p < count; p++)
                {
                    var t = p / (float)divisor;
                    var angle = (startAngle + line.ArcDegrees * t) * Mathf.Deg2Rad;
                    var wave = Mathf.Sin(angle * line.WaveFrequency + wavePhase)
                        + 0.45f * Mathf.Sin(angle * (line.WaveFrequency * 0.47f + 1.7f) - wavePhase * 1.31f);
                    var radialT = Mathf.Pow(Mathf.Clamp01(t), line.RadialPower);
                    var r = line.Radius + line.RadialDelta * radialT + wave * line.WaveAmplitude;
                    line.Renderer.SetPosition(p, ToPlane(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r));
                }

                var pulse = 1f + Mathf.Sin(time * (1.2f + line.Pulse) + line.Phase) * 0.12f * line.Pulse;
                line.Renderer.widthMultiplier = line.Width * Mathf.Max(0.35f, pulse);

                var tint = line.UsePrimary ? primaryColor : secondaryColor;
                var color = Hdr(tint, glowIntensity * (line.Loop ? 0.92f : 1.24f));
                var alpha = Mathf.Clamp01(line.Alpha * (0.76f + pulse * 0.24f));
                ApplyLineGradient(line.Renderer, color, alpha, !line.Loop);
            }
        }

        private void UpdateOrnaments(float time)
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            for (var i = 0; i < _ornaments.Count; i++)
            {
                var ornament = _ornaments[i];
                if (ornament.Renderer == null || ornament.Transform == null)
                {
                    continue;
                }

                var sparkle = Mathf.Sin(time * ornament.TwinkleSpeed + ornament.Phase) * 0.5f + 0.5f;
                sparkle = Mathf.SmoothStep(0.05f, 1f, sparkle);
                var scale = Mathf.Lerp(0.72f, 1.28f, sparkle);
                ornament.Transform.localScale = ornament.BaseScale * scale;

                var color = ornament.Color * Mathf.Lerp(0.7f, 1.35f, sparkle);
                color.a = Mathf.Clamp01(ornament.Alpha * Mathf.Lerp(0.46f, 1f, sparkle));
                ornament.Renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(TintColorId, color);
                _propertyBlock.SetFloat(AlphaId, color.a);
                ornament.Renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void ApplyLineGradient(LineRenderer renderer, Color color, float alpha, bool fadeEdges)
        {
            if (renderer == null)
            {
                return;
            }

            color.a = Mathf.Clamp01(alpha);
            if (!fadeEdges)
            {
                renderer.startColor = color;
                renderer.endColor = color;
                return;
            }

            var clear = color;
            clear.a = 0f;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 0.5f),
                    new GradientColorKey(color, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(color.a, 0.18f),
                    new GradientAlphaKey(color.a, 0.82f),
                    new GradientAlphaKey(0f, 1f)
                });
            renderer.colorGradient = gradient;
        }

        private GameObject CreateChild(string name)
        {
            if (_visualRoot == null && name != "VisualRoot")
            {
                _visualRoot = CreateChild("VisualRoot").transform;
            }

            var go = new GameObject(GeneratedPrefix + name);
            go.transform.SetParent(name == "VisualRoot" ? transform : _visualRoot, false);
            go.layer = gameObject.layer;
            return go;
        }

        private void DestroyGeneratedChildren()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child != null && child.name.StartsWith(GeneratedPrefix, StringComparison.Ordinal))
                {
                    DestroyObject(child.gameObject);
                }
            }

            _visualRoot = null;
        }

        private void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void PlayParticles()
        {
            for (var i = 0; i < _particleSystems.Count; i++)
            {
                if (_particleSystems[i] != null)
                {
                    _particleSystems[i].Play(true);
                }
            }
        }

        private Vector3 Polar(float polarRadius, float angleDegrees)
        {
            var radians = angleDegrees * Mathf.Deg2Rad;
            return ToPlane(Mathf.Cos(radians) * polarRadius, Mathf.Sin(radians) * polarRadius);
        }

        private Vector3 ToPlane(float x, float y)
        {
            return plane == ArcaneSfxPlane.XZ ? new Vector3(x, 0f, y) : new Vector3(x, y, 0f);
        }

        private Quaternion RotationAroundNormal(float angleDegrees)
        {
            return plane == ArcaneSfxPlane.XZ
                ? Quaternion.Euler(0f, -angleDegrees, 0f)
                : Quaternion.Euler(0f, 0f, angleDegrees);
        }

        private Mesh CreateDiscMesh(float discRadius, int segments)
        {
            var mesh = new Mesh { name = "Arcane SFX Disc" };
            var vertices = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var x = Mathf.Cos(angle) * discRadius;
                var y = Mathf.Sin(angle) * discRadius;
                vertices[i + 1] = ToPlane(x, y);
                uvs[i + 1] = new Vector2(x / (discRadius * 2f) + 0.5f, y / (discRadius * 2f) + 0.5f);
            }

            for (var i = 0; i < segments; i++)
            {
                var triangle = i * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = i + 1;
                triangles[triangle + 2] = i == segments - 1 ? 1 : i + 2;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private Mesh CreateStarMesh(float outerRadius, float innerRadius)
        {
            const int points = 8;
            var mesh = new Mesh { name = "Arcane SFX Star" };
            var vertices = new Vector3[points + 1];
            var triangles = new int[points * 3];
            var uvs = new Vector2[points + 1];
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (var i = 0; i < points; i++)
            {
                var angle = (i / (float)points * Mathf.PI * 2f) + Mathf.PI * 0.25f;
                var pointRadius = i % 2 == 0 ? outerRadius : innerRadius;
                var x = Mathf.Cos(angle) * pointRadius;
                var y = Mathf.Sin(angle) * pointRadius;
                vertices[i + 1] = ToPlane(x, y);
                uvs[i + 1] = new Vector2(x / (outerRadius * 2f) + 0.5f, y / (outerRadius * 2f) + 0.5f);
            }

            for (var i = 0; i < points; i++)
            {
                var triangle = i * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = i + 1;
                triangles[triangle + 2] = i == points - 1 ? 1 : i + 2;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private Mesh CreateDiamondMesh(float size)
        {
            var mesh = new Mesh { name = "Arcane SFX Diamond" };
            mesh.vertices = new[]
            {
                ToPlane(0f, size),
                ToPlane(size * 0.58f, 0f),
                ToPlane(0f, -size),
                ToPlane(-size * 0.58f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0.5f, 1f),
                new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 0.5f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private Gradient CreateParticleFadeGradient(Color tint)
        {
            var bright = Hdr(tint, glowIntensity);
            bright.a = 1f;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(bright, 0f),
                    new GradientColorKey(Color.white, 0.45f),
                    new GradientColorKey(bright, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.95f, 0.18f),
                    new GradientAlphaKey(0.55f, 0.62f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private void ClampValues()
        {
            radius = Mathf.Max(0.25f, radius);
            playbackSpeed = Mathf.Max(0.05f, playbackSpeed);
            glowIntensity = Mathf.Clamp(glowIntensity, 0.25f, 4f);
        }

        private void EnsureSeed()
        {
            if (_seed <= 0f)
            {
                _seed = UnityEngine.Random.value * 1000f + 1f;
            }
        }

        private float GetEffectTime()
        {
            return Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        }

        private static Color Hdr(Color color, float intensity)
        {
            var result = color * Mathf.Max(0.01f, intensity);
            result.a = color.a;
            return result;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private enum OrnamentShape
        {
            Star,
            Diamond
        }

        private readonly struct LineState
        {
            public readonly LineRenderer Renderer;
            public readonly float Radius;
            public readonly float StartAngle;
            public readonly float ArcDegrees;
            public readonly float RadialDelta;
            public readonly float RadialPower;
            public readonly float Width;
            public readonly float Alpha;
            public readonly float WaveAmplitude;
            public readonly float WaveFrequency;
            public readonly float AngularSpeed;
            public readonly float Pulse;
            public readonly bool UsePrimary;
            public readonly bool Loop;
            public readonly float Phase;

            public LineState(
                LineRenderer renderer,
                float radius,
                float startAngle,
                float arcDegrees,
                float radialDelta,
                float radialPower,
                float width,
                float alpha,
                float waveAmplitude,
                float waveFrequency,
                float angularSpeed,
                float pulse,
                bool usePrimary,
                bool loop,
                float phase)
            {
                Renderer = renderer;
                Radius = radius;
                StartAngle = startAngle;
                ArcDegrees = arcDegrees;
                RadialDelta = radialDelta;
                RadialPower = Mathf.Max(0.01f, radialPower);
                Width = width;
                Alpha = alpha;
                WaveAmplitude = waveAmplitude;
                WaveFrequency = waveFrequency;
                AngularSpeed = angularSpeed;
                Pulse = pulse;
                UsePrimary = usePrimary;
                Loop = loop;
                Phase = phase;
            }
        }

        private readonly struct OrnamentState
        {
            public readonly Renderer Renderer;
            public readonly Transform Transform;
            public readonly Vector3 BaseScale;
            public readonly Color Color;
            public readonly float Alpha;
            public readonly float TwinkleSpeed;
            public readonly float Phase;

            public OrnamentState(
                Renderer renderer,
                Transform transform,
                Vector3 baseScale,
                Color color,
                float alpha,
                float twinkleSpeed,
                float phase)
            {
                Renderer = renderer;
                Transform = transform;
                BaseScale = baseScale;
                Color = color;
                Alpha = alpha;
                TwinkleSpeed = twinkleSpeed;
                Phase = phase;
            }
        }
    }
}
