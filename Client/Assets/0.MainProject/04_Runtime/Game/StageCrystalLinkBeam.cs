using System;
using UnityEngine;

namespace RhythmRPG.Game.Stage
{
    [DisallowMultipleComponent]
    public sealed class StageCrystalLinkBeam : MonoBehaviour
    {
        public Transform Source;
        public Transform Target;
        public string SourcePath = "Crystal";
        public string TargetPath = "Runic_Circle_Platform/Crystal";
        public LineRenderer Line;
        public Light LinkLight;
        public GameObject[] LinkedObjects = Array.Empty<GameObject>();
        public Color BeamColor = new(0.72f, 0.46f, 1f, 0.9f);
        public float BeamWidth = 0.08f;
        public float SourceYOffset;
        public float TargetYOffset;
        public bool UseRendererBoundsEndpoints = true;
        [Range(4, 48)] public int SegmentCount = 18;
        public float ArcHeight = 0.55f;
        public float DistanceArcFactor = 0.025f;
        public float WaveAmplitude = 0.08f;
        public float WaveFrequency = 1.6f;
        public float PulseFrequency = 1.25f;
        [Range(0f, 0.6f)] public float PulseStrength = 0.22f;

        private static Material _lineMaterial;

        private void Reset()
        {
            Source = transform;
            Line = GetComponent<LineRenderer>();
            LinkLight = GetComponentInChildren<Light>(true);
        }

        private void Awake()
        {
            ResolveEndpoints();
            EnsureLine();
            ApplyActiveState(true);
        }

        private void OnEnable()
        {
            ResolveEndpoints();
            EnsureLine();
            ApplyActiveState(true);
            UpdateBeam();
        }

        private void OnDisable()
        {
            ApplyActiveState(false);
        }

        private void LateUpdate()
        {
            ResolveEndpoints();
            UpdateBeam();
        }

        private void ResolveEndpoints()
        {
            if (Source == null && !string.IsNullOrWhiteSpace(SourcePath))
                Source = ResolveChildPath(transform.root, SourcePath);

            if (Source == null)
                Source = transform.parent;

            if (Target == null && !string.IsNullOrWhiteSpace(TargetPath))
            {
                GameObject targetObject = GameObject.Find(TargetPath);
                if (targetObject != null)
                    Target = targetObject.transform;
            }
        }

        private void EnsureLine()
        {
            if (Line == null)
                Line = GetComponent<LineRenderer>();

            if (Line == null)
                Line = gameObject.AddComponent<LineRenderer>();

            Line.useWorldSpace = true;
            Line.positionCount = Mathf.Max(4, SegmentCount);
            Line.startWidth = BeamWidth;
            Line.endWidth = BeamWidth * 1.35f;
            Line.startColor = BeamColor;
            Line.endColor = BeamColor;
            Line.numCapVertices = 6;
            Line.numCornerVertices = 4;
            Line.textureMode = LineTextureMode.Stretch;
            Line.alignment = LineAlignment.View;
            Line.widthCurve = CreateBeamWidthCurve();
            Line.colorGradient = CreateBeamGradient(BeamColor);
            Line.material = GetLineMaterial();
        }

        private void UpdateBeam()
        {
            if (Line == null || Source == null || Target == null)
                return;

            Vector3 start = ResolveEndpoint(Source, SourceYOffset);
            Vector3 end = ResolveEndpoint(Target, TargetYOffset);
            int segmentCount = Mathf.Max(4, SegmentCount);
            if (Line.positionCount != segmentCount)
                Line.positionCount = segmentCount;

            float distance = Vector3.Distance(start, end);
            Vector3 direction = end - start;
            Vector3 horizontalDirection = new Vector3(direction.x, 0f, direction.z);
            Vector3 side = horizontalDirection.sqrMagnitude > 0.0001f
                ? Vector3.Cross(Vector3.up, horizontalDirection.normalized)
                : transform.right;

            float arc = ArcHeight + distance * DistanceArcFactor;
            float time = Time.time;
            float pulse = 1f + Mathf.Sin(time * Mathf.PI * 2f * PulseFrequency) * PulseStrength;

            for (int i = 0; i < segmentCount; i++)
            {
                float t = segmentCount <= 1 ? 0f : i / (segmentCount - 1f);
                Vector3 point = Vector3.Lerp(start, end, t);
                float arcT = Mathf.Sin(t * Mathf.PI);
                float wave = Mathf.Sin((t * Mathf.PI * 2f) + time * WaveFrequency) * WaveAmplitude * arcT;
                point += Vector3.up * (arc * arcT);
                point += side * wave;
                Line.SetPosition(i, point);
            }

            Line.widthMultiplier = pulse;
            if (LinkLight != null)
            {
                float normalizedPulse = Mathf.InverseLerp(1f - PulseStrength, 1f + PulseStrength, pulse);
                LinkLight.intensity = Mathf.Lerp(1.45f, 2.15f, normalizedPulse);
            }
        }

        private Vector3 ResolveEndpoint(Transform endpointRoot, float yOffset)
        {
            if (endpointRoot == null)
                return transform.position;

            if (!UseRendererBoundsEndpoints || !TryGetCrystalBounds(endpointRoot, out Bounds bounds))
                return endpointRoot.position + Vector3.up * yOffset;

            return bounds.center + Vector3.up * yOffset;
        }

        private static bool TryGetCrystalBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null
                    || renderer is LineRenderer
                    || renderer is ParticleSystemRenderer
                    || renderer.transform.name.StartsWith("FX_", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static Transform ResolveChildPath(Transform root, string path)
        {
            if (root == null || string.IsNullOrWhiteSpace(path))
                return null;

            string[] parts = path.Split('/');
            Transform current = root;
            int startIndex = parts.Length > 0 && string.Equals(parts[0], root.name, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            for (int i = startIndex; i < parts.Length; i++)
            {
                string part = parts[i];
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                current = current.Find(part);
                if (current == null)
                    return null;
            }

            return current;
        }

        private void ApplyActiveState(bool active)
        {
            if (Line != null)
                Line.enabled = active;

            if (LinkLight != null)
                LinkLight.enabled = active;

            if (LinkedObjects == null)
                return;

            foreach (GameObject linkedObject in LinkedObjects)
            {
                if (linkedObject != null)
                    linkedObject.SetActive(active);
            }
        }

        private static Material GetLineMaterial()
        {
            if (_lineMaterial != null)
                return _lineMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            _lineMaterial = shader != null ? new Material(shader) : null;
            if (_lineMaterial != null)
                _lineMaterial.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

            return _lineMaterial;
        }

        private static AnimationCurve CreateBeamWidthCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.18f),
                new Keyframe(0.12f, 0.75f),
                new Keyframe(0.5f, 1.25f),
                new Keyframe(0.88f, 0.82f),
                new Keyframe(1f, 0.24f));
        }

        private static Gradient CreateBeamGradient(Color color)
        {
            Color start = new(color.r, color.g, color.b, color.a * 0.38f);
            Color middle = new(0.88f, 0.96f, 1f, Mathf.Clamp01(color.a));
            Color end = new(0.64f, 0.92f, 1f, color.a * 0.58f);

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(color, 0.35f),
                    new GradientColorKey(middle, 0.62f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(start.a, 0f),
                    new GradientAlphaKey(color.a, 0.35f),
                    new GradientAlphaKey(middle.a, 0.62f),
                    new GradientAlphaKey(end.a, 1f)
                });
            return gradient;
        }
    }
}
