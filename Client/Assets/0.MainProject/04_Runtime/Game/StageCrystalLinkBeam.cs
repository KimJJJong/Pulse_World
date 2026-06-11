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
            Line.positionCount = 2;
            Line.startWidth = BeamWidth;
            Line.endWidth = BeamWidth * 1.35f;
            Line.startColor = BeamColor;
            Line.endColor = BeamColor;
            Line.numCapVertices = 6;
            Line.numCornerVertices = 4;
            Line.textureMode = LineTextureMode.Stretch;
            Line.material = GetLineMaterial();
        }

        private void UpdateBeam()
        {
            if (Line == null || Source == null || Target == null)
                return;

            Line.SetPosition(0, ResolveEndpoint(Source, SourceYOffset));
            Line.SetPosition(1, ResolveEndpoint(Target, TargetYOffset));
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
    }
}
