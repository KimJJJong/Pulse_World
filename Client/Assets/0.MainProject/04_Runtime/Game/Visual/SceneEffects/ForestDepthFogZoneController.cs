using UnityEngine;

namespace RhythmRPG.Game.Visual.SceneEffects
{
    [ExecuteAlways]
    public sealed class ForestDepthFogZoneController : MonoBehaviour
    {
        public const int MaxZoneCount = 8;

        private static readonly int FogEnabledId = Shader.PropertyToID("_FogEnabled");
        private static readonly int FogZoneCountId = Shader.PropertyToID("_FogZoneCount");
        private static readonly int FogZoneCentersId = Shader.PropertyToID("_FogZoneCenters");
        private static readonly int FogZoneRightAxesId = Shader.PropertyToID("_FogZoneRightAxes");
        private static readonly int FogZoneForwardAxesId = Shader.PropertyToID("_FogZoneForwardAxes");
        private static readonly int FogZoneParamsId = Shader.PropertyToID("_FogZoneParams");
        private static readonly int FogZoneNoiseParamsId = Shader.PropertyToID("_FogZoneNoiseParams");
        private static readonly int FogZoneColorsId = Shader.PropertyToID("_FogZoneColors");

        private static readonly Vector4[] ZoneCenters = new Vector4[MaxZoneCount];
        private static readonly Vector4[] ZoneRightAxes = new Vector4[MaxZoneCount];
        private static readonly Vector4[] ZoneForwardAxes = new Vector4[MaxZoneCount];
        private static readonly Vector4[] ZoneParams = new Vector4[MaxZoneCount];
        private static readonly Vector4[] ZoneNoiseParams = new Vector4[MaxZoneCount];
        private static readonly Vector4[] ZoneColors = new Vector4[MaxZoneCount];

        private static ForestDepthFogZoneController activeController;

        [Header("Fog Shader Material")]
        [SerializeField] private Material fogMaterial;

        [Header("Fog Zones")]
        [SerializeField] private bool autoCollectChildZones = true;
        [SerializeField] private ForestDepthFogZone[] zones = System.Array.Empty<ForestDepthFogZone>();

        public void Configure(
            Material material,
            ForestDepthFogZone[] fogZones,
            bool collectChildZones,
            bool applyImmediately = true)
        {
            fogMaterial = material;
            zones = fogZones ?? System.Array.Empty<ForestDepthFogZone>();
            autoCollectChildZones = collectChildZones;

            if (applyImmediately)
            {
                ApplyNow();
            }
        }

        private void OnEnable()
        {
            activeController = this;
            ApplyNow();
        }

        private void OnDisable()
        {
            if (activeController == this)
            {
                DisableFog();
                activeController = null;
            }
        }

        private void OnDestroy()
        {
            if (activeController == this)
            {
                DisableFog();
                activeController = null;
            }
        }

        private void LateUpdate()
        {
            ApplyNow();
        }

        private void OnValidate()
        {
            ApplyNow();
        }

        public void ApplyNow()
        {
            if (fogMaterial == null)
            {
                return;
            }

            if (autoCollectChildZones)
            {
                zones = GetComponentsInChildren<ForestDepthFogZone>(true);
            }

            var zoneCount = WriteZoneArrays();
            fogMaterial.SetFloat(FogEnabledId, zoneCount > 0 ? 1f : 0f);
            fogMaterial.SetInt(FogZoneCountId, zoneCount);
            fogMaterial.SetVectorArray(FogZoneCentersId, ZoneCenters);
            fogMaterial.SetVectorArray(FogZoneRightAxesId, ZoneRightAxes);
            fogMaterial.SetVectorArray(FogZoneForwardAxesId, ZoneForwardAxes);
            fogMaterial.SetVectorArray(FogZoneParamsId, ZoneParams);
            fogMaterial.SetVectorArray(FogZoneNoiseParamsId, ZoneNoiseParams);
            fogMaterial.SetVectorArray(FogZoneColorsId, ZoneColors);
        }

        public void DisableFog()
        {
            if (fogMaterial == null)
            {
                return;
            }

            fogMaterial.SetFloat(FogEnabledId, 0f);
            fogMaterial.SetInt(FogZoneCountId, 0);
        }

        private int WriteZoneArrays()
        {
            ClearArrays();

            if (zones == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < zones.Length && count < MaxZoneCount; i++)
            {
                var zone = zones[i];
                if (zone == null || !zone.isActiveAndEnabled || !zone.HasValidSize)
                {
                    continue;
                }

                ZoneCenters[count] = zone.Center;
                ZoneRightAxes[count] = zone.RightAxis;
                ZoneForwardAxes[count] = zone.ForwardAxis;
                ZoneParams[count] = zone.ShapeParams;
                ZoneNoiseParams[count] = zone.NoiseParams;
                ZoneColors[count] = zone.FogColor;
                count++;
            }

            return count;
        }

        private static void ClearArrays()
        {
            for (var i = 0; i < MaxZoneCount; i++)
            {
                ZoneCenters[i] = Vector4.zero;
                ZoneRightAxes[i] = Vector4.zero;
                ZoneForwardAxes[i] = Vector4.zero;
                ZoneParams[i] = Vector4.zero;
                ZoneNoiseParams[i] = Vector4.zero;
                ZoneColors[i] = Vector4.zero;
            }
        }
    }
}
