using UnityEngine;

namespace RhythmRPG.Game.Visual.SceneEffects
{
    [ExecuteAlways]
    public sealed class ForestDepthFogZone : MonoBehaviour
    {
        [Header("Zone Shape")]
        [SerializeField, Min(0.5f)] private float width = 14f;
        [SerializeField, Min(0.5f)] private float length = 18f;
        [SerializeField, Range(0.5f, 20f)] private float edgeBlendDistance = 6f;

        [Header("Fog Look")]
        [SerializeField, Range(0.001f, 0.2f)] private float density = 0.035f;
        [SerializeField, Range(0f, 5f)] private float noiseStrength = 1f;
        [SerializeField, Range(0.001f, 0.2f)] private float noiseScale = 0.045f;
        [SerializeField] private Color fogColor = new(0.08f, 0.15f, 0.16f, 1f);

        public bool HasValidSize => width > 0f && length > 0f;
        public Vector4 Center => transform.position;
        public Vector4 RightAxis => transform.right.normalized;
        public Vector4 ForwardAxis => transform.forward.normalized;
        public Vector4 ShapeParams => new(width * 0.5f, length * 0.5f, edgeBlendDistance, density);
        public Vector4 NoiseParams => new(noiseStrength, noiseScale, 0f, 0f);
        public Vector4 FogColor => fogColor;
        public float Density => density;

        public void Configure(
            float zoneWidth,
            float zoneLength,
            float blendDistance,
            float fogDensity,
            float boundaryNoiseStrength,
            float boundaryNoiseScale,
            Color color)
        {
            width = zoneWidth;
            length = zoneLength;
            edgeBlendDistance = blendDistance;
            density = fogDensity;
            noiseStrength = boundaryNoiseStrength;
            noiseScale = boundaryNoiseScale;
            fogColor = color;
        }

        public void SetDensity(float fogDensity, bool applyImmediately = true)
        {
            density = Mathf.Max(0f, fogDensity);

            if (applyImmediately)
            {
                var controller = GetComponentInParent<ForestDepthFogZoneController>(true);
                controller?.ApplyNow();
            }
        }

        private void OnValidate()
        {
            var controller = GetComponentInParent<ForestDepthFogZoneController>();
            controller?.ApplyNow();
        }

        private void OnEnable()
        {
            var controller = GetComponentInParent<ForestDepthFogZoneController>();
            controller?.ApplyNow();
        }

        private void OnDisable()
        {
            var controller = GetComponentInParent<ForestDepthFogZoneController>();
            controller?.ApplyNow();
        }

        private void OnDrawGizmosSelected()
        {
            var cachedMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(width, 0.05f, length));
            Gizmos.color = new Color(fogColor.r, fogColor.g, fogColor.b, 0.35f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = cachedMatrix;
        }
    }
}
