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
    public float targetAlpha = 0.2f;  // The alpha/dither value when faded
    public float targetHeightOffset = 1.0f; // Offset to aim for body instead of feet

    [Header("Raycast")]
    public float rayRadius = 0.5f;    // Radius of the ray (SphereCast)
    
    [Header("Debug")]
    public bool debugMode = false;

    // Internal
    private Dictionary<Renderer, float> _fadingRenderers = new Dictionary<Renderer, float>();
    private List<Renderer> _hitRenderers = new List<Renderer>();
    private Collider[] _hitColliders = new Collider[50]; // Changed from RaycastHit to Collider
    
    // Cache
    private MaterialPropertyBlock _propBlock;
    private static readonly int DitherPropID = Shader.PropertyToID("_DitherInternal");

    void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
        FindTargetIfNull();
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
            QueryTriggerInteraction.Ignore
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

            Renderer r = col.GetComponent<Renderer>();
            if (r == null) r = col.GetComponentInChildren<Renderer>(); 

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
        List<Renderer> toRemove = new List<Renderer>();
        var keys = new List<Renderer>(_fadingRenderers.Keys);

        foreach (var r in keys)
        {
            if (r == null) 
            {
                toRemove.Add(r);
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
                    toRemove.Add(r);
                }
            }
        }

        foreach (var r in toRemove)
        {
            if (r != null)
            {
                _fadingRenderers.Remove(r);
                SetDitherValue(r, 1.0f); 
            }
            else
            {
                _fadingRenderers.Remove(r);
            }
        }
    }

    private void SetDitherValue(Renderer r, float val)
    {
        r.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(DitherPropID, val);
        r.SetPropertyBlock(_propBlock);
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
