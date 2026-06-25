using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RhythmRPG.Game.Visual.SceneEffects
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class ForestVisualSceneSettings : MonoBehaviour
    {
        [SerializeField] private Color ambientColor = new(0.043f, 0.102f, 0.094f, 1f);
        [SerializeField] private float ambientIntensity = 0.32f;
        [SerializeField] private bool fogEnabled = true;
        [SerializeField] private Color fogColor = new(0.027f, 0.106f, 0.11f, 1f);
        [SerializeField] private FogMode fogMode = FogMode.ExponentialSquared;
        [SerializeField] private float fogDensity = 0.025f;
        [SerializeField] private Volume postProcessVolume;

        private void Reset()
        {
            postProcessVolume = GetComponent<Volume>();
        }

        private void Awake()
        {
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Start()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        private void Apply()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.fog = fogEnabled;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.reflectionIntensity = 0.35f;

            ConfigureMainCamera();
            ConfigurePostProcessVolume();
        }

        private static void ConfigureMainCamera()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            mainCamera.allowHDR = true;
            var cameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = true;
            }
        }

        private void ConfigurePostProcessVolume()
        {
            if (postProcessVolume == null)
            {
                postProcessVolume = GetComponent<Volume>();
            }

            if (postProcessVolume == null)
            {
                return;
            }

            postProcessVolume.isGlobal = true;
            postProcessVolume.priority = 10f;
            postProcessVolume.weight = 1f;

            if (postProcessVolume.profile == null)
            {
                postProcessVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                postProcessVolume.profile.name = "Forest Visual Sample Runtime Profile";
            }

            var profile = postProcessVolume.profile;
            var bloom = GetOrAdd<Bloom>(profile);
            bloom.intensity.Override(0.75f);
            bloom.threshold.Override(0.9f);
            bloom.scatter.Override(0.62f);

            var colorAdjustments = GetOrAdd<ColorAdjustments>(profile);
            colorAdjustments.postExposure.Override(-0.45f);
            colorAdjustments.contrast.Override(16f);
            colorAdjustments.saturation.Override(-8f);
            colorAdjustments.colorFilter.Override(new Color(0.78f, 1f, 0.92f, 1f));

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.34f);
            vignette.smoothness.Override(0.66f);
            vignette.color.Override(new Color(0.01f, 0.035f, 0.04f, 1f));
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
            }

            component.active = true;
            return component;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class ForestLightFlicker : MonoBehaviour
    {
        [SerializeField] private Light targetLight;
        [SerializeField] private float baseIntensity = 3f;
        [SerializeField] private float flickerAmount = 0.35f;
        [SerializeField] private float speed = 5f;
        [SerializeField] private float noiseOffset;

        private void Reset()
        {
            targetLight = GetComponent<Light>();
            if (targetLight != null)
            {
                baseIntensity = targetLight.intensity;
            }

            noiseOffset = Random.value * 100f;
        }

        private void Awake()
        {
            if (targetLight == null)
            {
                targetLight = GetComponent<Light>();
            }

            if (targetLight != null && baseIntensity <= 0f)
            {
                baseIntensity = targetLight.intensity;
            }

            if (noiseOffset <= 0f)
            {
                noiseOffset = Random.value * 100f;
            }
        }

        private void Update()
        {
            if (targetLight == null)
            {
                return;
            }

            var noise = Mathf.PerlinNoise((Time.time + noiseOffset) * speed, 0f);
            targetLight.intensity = Mathf.Max(0f, baseIntensity + (noise - 0.5f) * flickerAmount);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class ForestEmissionPulse : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color emissionColor = new(0f, 1f, 0.82f, 1f);
        [SerializeField] private float minIntensity = 2f;
        [SerializeField] private float maxIntensity = 5.5f;
        [SerializeField] private float speed = 1.8f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private Material runtimeMaterial;

        private void Reset()
        {
            targetRenderer = GetComponent<Renderer>();
        }

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            if (targetRenderer == null)
            {
                return;
            }

            runtimeMaterial = targetRenderer.material;
            runtimeMaterial.EnableKeyword("_EMISSION");
        }

        private void Update()
        {
            if (runtimeMaterial == null)
            {
                return;
            }

            var t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
            var intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
            var litColor = emissionColor * intensity;

            if (runtimeMaterial.HasProperty(BaseColorId))
            {
                runtimeMaterial.SetColor(BaseColorId, emissionColor);
            }

            if (runtimeMaterial.HasProperty(EmissionColorId))
            {
                runtimeMaterial.SetColor(EmissionColorId, litColor);
            }
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ForestParticlePreset : MonoBehaviour
    {
        public enum Preset
        {
            WarmEmber,
            CyanDust,
            BackgroundFireflyDust
        }

        public Preset preset = Preset.WarmEmber;
        [SerializeField] private ParticleSystem particleSystemTarget;

        private void Reset()
        {
            particleSystemTarget = GetComponent<ParticleSystem>();
        }

        private void Awake()
        {
            Apply();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (particleSystemTarget == null)
            {
                particleSystemTarget = GetComponent<ParticleSystem>();
            }

            if (particleSystemTarget == null)
            {
                return;
            }

            var main = particleSystemTarget.main;
            var emission = particleSystemTarget.emission;
            var shape = particleSystemTarget.shape;
            var colorOverLifetime = particleSystemTarget.colorOverLifetime;

            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            colorOverLifetime.enabled = true;

            switch (preset)
            {
                case Preset.WarmEmber:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
                    main.maxParticles = 28;
                    emission.rateOverTime = 8f;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 12f;
                    shape.radius = 0.18f;
                    colorOverLifetime.color = GradientFrom(new Color(1f, 0.62f, 0.18f, 0.9f), new Color(1f, 0.28f, 0.04f, 0f));
                    break;
                case Preset.CyanDust:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.32f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
                    main.maxParticles = 36;
                    emission.rateOverTime = 7f;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.45f;
                    colorOverLifetime.color = GradientFrom(new Color(0f, 1f, 0.82f, 0.7f), new Color(0f, 0.55f, 1f, 0f));
                    break;
                case Preset.BackgroundFireflyDust:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 7f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.13f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.05f);
                    main.maxParticles = 42;
                    emission.rateOverTime = 3f;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(12f, 2.4f, 7f);
                    colorOverLifetime.color = GradientFrom(new Color(0.45f, 1f, 0.72f, 0.3f), new Color(0.2f, 0.9f, 0.7f, 0f));
                    break;
            }
        }

        private static ParticleSystem.MinMaxGradient GradientFrom(Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(start.a, 0f),
                    new GradientAlphaKey(end.a, 1f)
                });

            return new ParticleSystem.MinMaxGradient(gradient);
        }
    }
}
