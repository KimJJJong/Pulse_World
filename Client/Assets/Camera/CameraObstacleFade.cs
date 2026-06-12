using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;

public class CameraObstacleFade : MonoBehaviour
{
    [Header("Settings")]
    public Transform target;          // The player (or target) transform
    public LayerMask obstacleLayer;   // Layers to check for obstacles
    public float fadeSpeed = 5f;      // How fast to fade in/out
    public float targetAlpha = 0.28f; // The alpha/dither value when faded
    public float targetHeightOffset = 1.0f; // Offset to aim for body instead of feet

    [Header("Raycast")]
    public float rayRadius = 0.5f;    // Radius of the ray (SphereCast)
    
    [Header("Debug")]
    public bool debugMode = false;

    // Internal
    private readonly Dictionary<Renderer, float> _fadingRenderers = new Dictionary<Renderer, float>();
    private readonly Dictionary<Renderer, RendererMaterialState> _originalMaterialStates = new Dictionary<Renderer, RendererMaterialState>();
    private readonly HashSet<Renderer> _ditherReadyRenderers = new HashSet<Renderer>();
    private readonly HashSet<Renderer> _hitRenderers = new HashSet<Renderer>();
    private readonly List<Renderer> _fadingRendererKeys = new List<Renderer>(32);
    private readonly List<Renderer> _renderersToRemove = new List<Renderer>(16);
    private readonly Dictionary<Collider, Renderer> _colliderRendererCache = new Dictionary<Collider, Renderer>();
    private Collider[] _hitColliders = new Collider[50]; // Changed from RaycastHit to Collider
    
    // Cache
    private MaterialPropertyBlock _propBlock;
    private Shader _ditherShader;
    private static readonly int DitherPropID = Shader.PropertyToID("_DitherInternal");
    private static readonly int BaseMapPropID = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexPropID = Shader.PropertyToID("_MainTex");
    private static readonly int BaseColorPropID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropID = Shader.PropertyToID("_Color");
    private static readonly int BumpMapPropID = Shader.PropertyToID("_BumpMap");
    private static readonly int BumpScalePropID = Shader.PropertyToID("_BumpScale");
    private static readonly int MetallicPropID = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessPropID = Shader.PropertyToID("_Smoothness");
    private static readonly int MetallicGlossMapPropID = Shader.PropertyToID("_MetallicGlossMap");
    private static readonly int EmissionColorPropID = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMapPropID = Shader.PropertyToID("_EmissionMap");
    private static readonly int OcclusionStrengthPropID = Shader.PropertyToID("_OcclusionStrength");
    private static readonly int OcclusionMapPropID = Shader.PropertyToID("_OcclusionMap");
    private static readonly int ReceiveShadowsPropID = Shader.PropertyToID("_ReceiveShadows");
    private const string DitherShaderName = "RhythmRPG/TopDown/DitherTransparentPBR";

    void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
        FindTargetIfNull();
    }

    void OnDisable()
    {
        RestoreAllRenderers();
    }

    void Reset()
    {
        FindTargetIfNull();
        obstacleLayer = LayerMask.GetMask("Default", "Environment", "Wall"); 
    }

    void FindTargetIfNull()
    {
        if (target == null)
        {
            var follow = GetComponent<CameraFollow>();
            if (follow != null) target = follow.target;
        }
    }

    void LateUpdate()
    {
        if (target == null)
        {
            FindTargetIfNull();
            if (target == null) return;
        }

        // 1. Calculate Capsule Points
        Vector3 startPos = transform.position;
        Vector3 endPos = target.position + Vector3.up * targetHeightOffset;
        
        // Use OverlapCapsule to check the volume
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            startPos, 
            endPos, 
            rayRadius, 
            _hitColliders, 
            obstacleLayer,
            QueryTriggerInteraction.Collide
        );

        if (debugMode)
        {
            Debug.DrawLine(startPos, endPos, Color.red);
        }

        _hitRenderers.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            var col = _hitColliders[i];
            
            // Skip target or its children
            if (col.transform == target || col.transform.IsChildOf(target)) continue;

            if (!_colliderRendererCache.TryGetValue(col, out Renderer r))
            {
                var fadeTarget = col.GetComponent<CameraObstacleFadeTarget>();
                r = fadeTarget != null ? fadeTarget.TargetRenderer : null;
                if (r == null) r = col.GetComponent<Renderer>();
                if (r == null) r = col.GetComponentInParent<Renderer>();
                if (r == null) r = col.GetComponentInChildren<Renderer>();
                _colliderRendererCache[col] = r;
            }

            if (r != null)
            {
                _hitRenderers.Add(r);
                if (!_fadingRenderers.ContainsKey(r))
                {
                    _fadingRenderers.Add(r, 1.0f); 
                }

                if (debugMode)
                {
                    // Draw a point on the object center
                    Debug.DrawRay(col.bounds.center, Vector3.up * 0.5f, Color.green);
                }
            }
        }

        // 2. Update Dither Values
        _renderersToRemove.Clear();
        _fadingRendererKeys.Clear();
        foreach (var key in _fadingRenderers.Keys)
            _fadingRendererKeys.Add(key);

        for (int i = 0; i < _fadingRendererKeys.Count; i++)
        {
            var r = _fadingRendererKeys[i];
            if (r == null) 
            {
                _renderersToRemove.Add(r);
                continue;
            }

            // Check if still hit
            bool isHit = _hitRenderers.Contains(r);
            float currentVal = _fadingRenderers[r];
            
            float targetVal = isHit ? targetAlpha : 1.0f;

            if (Mathf.Abs(currentVal - targetVal) > 0.01f)
            {
                float newVal = Mathf.MoveTowards(currentVal, targetVal, Time.deltaTime * fadeSpeed);
                _fadingRenderers[r] = newVal;
                SetDitherValue(r, newVal);
            }
            else
            {
                if (currentVal != targetVal)
                {
                    _fadingRenderers[r] = targetVal;
                    SetDitherValue(r, targetVal);
                }

                if (!isHit && targetVal >= 0.99f)
                {
                    _renderersToRemove.Add(r);
                }
            }
        }

        for (int i = 0; i < _renderersToRemove.Count; i++)
        {
            var r = _renderersToRemove[i];
            if (r != null)
            {
                _fadingRenderers.Remove(r);
                RestoreRenderer(r);
            }
            else
            {
                _fadingRenderers.Remove(r);
            }
        }
    }

    private void SetDitherValue(Renderer r, float val)
    {
        EnsureDitherReady(r);
        if (!_ditherReadyRenderers.Contains(r))
        {
            return;
        }

        r.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(DitherPropID, val);
        r.SetPropertyBlock(_propBlock);
    }

    private void EnsureDitherReady(Renderer r)
    {
        if (r == null || _ditherReadyRenderers.Contains(r))
        {
            return;
        }

        var sharedMaterials = r.sharedMaterials;
        var originalSharedMaterials = CopyMaterials(sharedMaterials);
        bool allMaterialsSupportDither = true;
        foreach (var material in sharedMaterials)
        {
            if (material != null && !material.HasProperty(DitherPropID))
            {
                allMaterialsSupportDither = false;
                break;
            }
        }

        if (allMaterialsSupportDither)
        {
            CacheOriginalRendererState(r, originalSharedMaterials);
            _ditherReadyRenderers.Add(r);
            return;
        }

        if (_ditherShader == null)
        {
            _ditherShader = Shader.Find(DitherShaderName);
        }

        if (_ditherShader == null)
        {
            Debug.LogWarning($"[CameraObstacleFade] Missing dither shader: {DitherShaderName}");
            return;
        }

        var state = CacheOriginalRendererState(r, originalSharedMaterials);
        var materials = r.materials;
        state.RuntimeMaterials = materials;
        _ditherReadyRenderers.Add(r);

        foreach (var material in materials)
        {
            if (material == null || material.HasProperty(DitherPropID))
            {
                continue;
            }

            var baseTexture = GetTexture(material, BaseMapPropID);
            var baseScale = GetTextureScale(material, BaseMapPropID, Vector2.one);
            var baseOffset = GetTextureOffset(material, BaseMapPropID, Vector2.zero);
            if (baseTexture == null)
            {
                baseTexture = GetTexture(material, MainTexPropID);
                baseScale = GetTextureScale(material, MainTexPropID, Vector2.one);
                baseOffset = GetTextureOffset(material, MainTexPropID, Vector2.zero);
            }

            var baseColor = material.HasProperty(BaseColorPropID)
                ? material.GetColor(BaseColorPropID)
                : material.HasProperty(ColorPropID)
                    ? material.GetColor(ColorPropID)
                    : Color.white;
            var bumpMap = GetTexture(material, BumpMapPropID);
            var metallicGlossMap = GetTexture(material, MetallicGlossMapPropID);
            var emissionMap = GetTexture(material, EmissionMapPropID);
            var occlusionMap = GetTexture(material, OcclusionMapPropID);
            var bumpScale = GetFloat(material, BumpScalePropID, 1.0f);
            var metallic = GetFloat(material, MetallicPropID, 0.0f);
            var smoothness = GetFloat(material, SmoothnessPropID, 0.5f);
            var emissionColor = GetColor(material, EmissionColorPropID, Color.black);
            var occlusionStrength = GetFloat(material, OcclusionStrengthPropID, 1.0f);
            var receiveShadows = GetFloat(material, ReceiveShadowsPropID, 1.0f);

            material.shader = _ditherShader;

            if (baseTexture != null && material.HasProperty(BaseMapPropID))
            {
                material.SetTexture(BaseMapPropID, baseTexture);
                material.SetTextureScale(BaseMapPropID, baseScale);
                material.SetTextureOffset(BaseMapPropID, baseOffset);
            }

            if (material.HasProperty(BaseColorPropID))
            {
                material.SetColor(BaseColorPropID, baseColor);
            }

            SetTexture(material, BumpMapPropID, bumpMap);
            SetTexture(material, MetallicGlossMapPropID, metallicGlossMap);
            SetTexture(material, EmissionMapPropID, emissionMap);
            SetTexture(material, OcclusionMapPropID, occlusionMap);
            SetFloat(material, BumpScalePropID, bumpScale);
            SetFloat(material, MetallicPropID, metallic);
            SetFloat(material, SmoothnessPropID, smoothness);
            SetColor(material, EmissionColorPropID, emissionColor);
            SetFloat(material, OcclusionStrengthPropID, occlusionStrength);
            SetFloat(material, ReceiveShadowsPropID, receiveShadows);
            SetKeyword(material, "_NORMALMAP", bumpMap != null);
            SetKeyword(material, "_METALLICSPECGLOSSMAP", metallicGlossMap != null);
            SetKeyword(material, "_EMISSION", emissionMap != null || emissionColor.maxColorComponent > 0.001f);
            SetKeyword(material, "_OCCLUSIONMAP", occlusionMap != null);
            SetKeyword(material, "_RECEIVE_SHADOWS_OFF", receiveShadows <= 0.5f);

            material.SetFloat(DitherPropID, 1.0f);
        }
    }

    private RendererMaterialState CacheOriginalRendererState(Renderer renderer, Material[] originalSharedMaterials)
    {
        if (_originalMaterialStates.TryGetValue(renderer, out var state))
        {
            return state;
        }

        var originalBlock = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(originalBlock);
        state = new RendererMaterialState(originalSharedMaterials, originalBlock);
        _originalMaterialStates.Add(renderer, state);
        return state;
    }

    private void RestoreRenderer(Renderer renderer)
    {
        if (ReferenceEquals(renderer, null))
        {
            return;
        }

        if (renderer == null)
        {
            _originalMaterialStates.Remove(renderer);
            _ditherReadyRenderers.Remove(renderer);
            return;
        }

        if (_originalMaterialStates.TryGetValue(renderer, out var state))
        {
            renderer.sharedMaterials = state.SharedMaterials;
            if (state.PropertyBlock != null && !state.PropertyBlock.isEmpty)
            {
                renderer.SetPropertyBlock(state.PropertyBlock);
            }
            else
            {
                renderer.SetPropertyBlock(null);
            }

            DestroyRuntimeMaterials(state);
            _originalMaterialStates.Remove(renderer);
        }
        else if (_ditherReadyRenderers.Contains(renderer))
        {
            renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat(DitherPropID, 1.0f);
            renderer.SetPropertyBlock(_propBlock);
        }

        _ditherReadyRenderers.Remove(renderer);
    }

    private void RestoreAllRenderers()
    {
        _fadingRendererKeys.Clear();
        foreach (var renderer in _fadingRenderers.Keys)
        {
            AddUniqueRenderer(_fadingRendererKeys, renderer);
        }

        foreach (var renderer in _ditherReadyRenderers)
        {
            AddUniqueRenderer(_fadingRendererKeys, renderer);
        }

        foreach (var renderer in _originalMaterialStates.Keys)
        {
            AddUniqueRenderer(_fadingRendererKeys, renderer);
        }

        for (int i = 0; i < _fadingRendererKeys.Count; i++)
        {
            RestoreRenderer(_fadingRendererKeys[i]);
        }

        _fadingRenderers.Clear();
        _hitRenderers.Clear();
        _ditherReadyRenderers.Clear();
        _originalMaterialStates.Clear();
        _colliderRendererCache.Clear();
    }

    private static void AddUniqueRenderer(List<Renderer> renderers, Renderer renderer)
    {
        if (!renderers.Contains(renderer))
        {
            renderers.Add(renderer);
        }
    }

    private static Material[] CopyMaterials(Material[] materials)
    {
        if (materials == null)
        {
            return null;
        }

        var copy = new Material[materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            copy[i] = materials[i];
        }

        return copy;
    }

    private static void DestroyRuntimeMaterials(RendererMaterialState state)
    {
        if (state == null || state.RuntimeMaterials == null)
        {
            return;
        }

        for (int i = 0; i < state.RuntimeMaterials.Length; i++)
        {
            var material = state.RuntimeMaterials[i];
            if (material == null || ContainsMaterial(state.SharedMaterials, material))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }

        state.RuntimeMaterials = null;
    }

    private static bool ContainsMaterial(Material[] materials, Material material)
    {
        if (materials == null)
        {
            return false;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == material)
            {
                return true;
            }
        }

        return false;
    }

    private static Texture GetTexture(Material material, int propertyId)
        => material != null && material.HasProperty(propertyId) ? material.GetTexture(propertyId) : null;

    private static float GetFloat(Material material, int propertyId, float fallback)
        => material != null && material.HasProperty(propertyId) ? material.GetFloat(propertyId) : fallback;

    private static Color GetColor(Material material, int propertyId, Color fallback)
        => material != null && material.HasProperty(propertyId) ? material.GetColor(propertyId) : fallback;

    private static Vector2 GetTextureScale(Material material, int propertyId, Vector2 fallback)
        => material != null && material.HasProperty(propertyId) ? material.GetTextureScale(propertyId) : fallback;

    private static Vector2 GetTextureOffset(Material material, int propertyId, Vector2 fallback)
        => material != null && material.HasProperty(propertyId) ? material.GetTextureOffset(propertyId) : fallback;

    private static void SetTexture(Material material, int propertyId, Texture value)
    {
        if (material != null && value != null && material.HasProperty(propertyId))
            material.SetTexture(propertyId, value);
    }

    private static void SetFloat(Material material, int propertyId, float value)
    {
        if (material != null && material.HasProperty(propertyId))
            material.SetFloat(propertyId, value);
    }

    private static void SetColor(Material material, int propertyId, Color value)
    {
        if (material != null && material.HasProperty(propertyId))
            material.SetColor(propertyId, value);
    }

    private static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (material == null)
            return;

        if (enabled)
            material.EnableKeyword(keyword);
        else
            material.DisableKeyword(keyword);
    }

    private sealed class RendererMaterialState
    {
        public readonly Material[] SharedMaterials;
        public readonly MaterialPropertyBlock PropertyBlock;
        public Material[] RuntimeMaterials;

        public RendererMaterialState(Material[] sharedMaterials, MaterialPropertyBlock propertyBlock)
        {
            SharedMaterials = sharedMaterials;
            PropertyBlock = propertyBlock;
        }
    }
    
    // Helper to draw wire capsule
    public static void DrawWireCapsule(Vector3 p1, Vector3 p2, float radius)
    {
        // Special case when points are same
        if (p1 == p2)
        {
            Gizmos.DrawWireSphere(p1, radius);
            return;
        }
        
        Vector3 forward = p2 - p1;
        Quaternion rot = Quaternion.LookRotation(forward);
        float length = forward.magnitude;
        Vector3 center = p1 + forward * 0.5f;
        
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(radius * 2, radius * 2, length));
        Gizmos.matrix = oldMatrix;
        
        Gizmos.DrawWireSphere(p1, radius);
        Gizmos.DrawWireSphere(p2, radius);
        Gizmos.DrawLine(p1 + rot * Vector3.up * radius, p2 + rot * Vector3.up * radius);
        Gizmos.DrawLine(p1 - rot * Vector3.up * radius, p2 - rot * Vector3.up * radius);
        Gizmos.DrawLine(p1 + rot * Vector3.right * radius, p2 + rot * Vector3.right * radius);
        Gizmos.DrawLine(p1 - rot * Vector3.right * radius, p2 - rot * Vector3.right * radius);
    }

    void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (target != null)
        {
            Vector3 startPos = transform.position;
            Vector3 endPos = target.position + Vector3.up * targetHeightOffset;

            // Always draw simplified line if selected, or full capsule if debug
           
            if (debugMode)
            {
                Gizmos.color = new Color(1, 0, 0, 0.5f);
                DrawWireCapsule(startPos, endPos, rayRadius);
            }
            else
            {
                 Gizmos.color = Color.yellow;
                 Gizmos.DrawLine(startPos, endPos);
            }
        }
#endif
    }
}
