using System;
using UnityEngine;

namespace RhythmRPG.Game.Stage
{
    [DisallowMultipleComponent]
    public sealed class StageCrystalLinkBeam : MonoBehaviour
    {
        public Transform Source;
        public Transform Target;
        public LineRenderer Line;
        public Light LinkLight;
        public GameObject[] LinkedObjects = Array.Empty<GameObject>();
        public Color BeamColor = new(0.72f, 0.46f, 1f, 0.9f);
        public float BeamWidth = 0.08f;
        public float SourceYOffset = 0.55f;
        public float TargetYOffset = 1.05f;

        private static Material _lineMaterial;

        private void Reset()
        {
            Source = transform;
            Line = GetComponent<LineRenderer>();
            LinkLight = GetComponentInChildren<Light>(true);
        }

        private void Awake()
        {
            EnsureLine();
            ApplyActiveState(true);
        }

        private void OnEnable()
        {
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
            UpdateBeam();
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

            Line.SetPosition(0, Source.position + Vector3.up * SourceYOffset);
            Line.SetPosition(1, Target.position + Vector3.up * TargetYOffset);
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
