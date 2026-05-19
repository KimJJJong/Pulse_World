using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoardTileVisual : MonoBehaviour
{
    private const string TopSurfaceName = "__TileTopSurface";
    private const string WarningSurfaceName = "__TileWarningOverlay";

    [SerializeField] private Renderer baseRenderer;
    [SerializeField] private Renderer topRenderer;
    [SerializeField] private Renderer warningRenderer;
    [SerializeField] private float topSurfaceOffset = 0.01f;
    [SerializeField] private float warningSurfaceOffset = 0.025f;

    private static Mesh _topSurfaceMesh;
    private static Material _warningOverlayMaterial;
    private MaterialPropertyBlock _baseBlock;
    private MaterialPropertyBlock _topBlock;
    private MaterialPropertyBlock _warningBlock;

    public Renderer BaseRenderer => ResolveBaseRenderer();

    public static Renderer FindBaseRenderer(GameObject root)
    {
        if (root == null)
            return null;

        if (root.TryGetComponent<Renderer>(out var rootRenderer) && !IsManagedSurfaceRenderer(rootRenderer))
            return rootRenderer;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer != null && !IsManagedSurfaceRenderer(renderer))
                return renderer;
        }

        return null;
    }

    public Renderer ResolveBaseRenderer()
    {
        if (baseRenderer != null && !IsManagedSurfaceRenderer(baseRenderer))
            return baseRenderer;

        baseRenderer = FindBaseRenderer(gameObject);
        return baseRenderer;
    }

    public void SetBaseMaterial(Material material)
    {
        var renderer = ResolveBaseRenderer();
        if (renderer != null && material != null && renderer.sharedMaterial != material)
            renderer.sharedMaterial = material;
    }

    public void SetBaseColor(Color color)
    {
        var renderer = ResolveBaseRenderer();
        if (renderer == null)
            return;

        _baseBlock ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(_baseBlock);
        _baseBlock.SetColor("_BaseColor", color);
        _baseBlock.SetColor("_Color", color);
        renderer.SetPropertyBlock(_baseBlock);
    }

    public void SetTopMaterial(Material material)
    {
        if (material == null)
        {
            HideTopSurface();
            return;
        }

        var renderer = EnsureTopSurface();
        if (renderer == null)
            return;

        renderer.sharedMaterial = material;
        renderer.enabled = true;
        SetTopColor(Color.white);
    }

    public void SetTopColor(Color color)
    {
        if (topRenderer == null || !topRenderer.enabled)
            return;

        _topBlock ??= new MaterialPropertyBlock();
        topRenderer.GetPropertyBlock(_topBlock);
        _topBlock.SetColor("_BaseColor", color);
        _topBlock.SetColor("_Color", color);
        topRenderer.SetPropertyBlock(_topBlock);
    }

    public void HideTopSurface()
    {
        if (topRenderer != null)
            topRenderer.enabled = false;
    }

    public void ShowWarningOverlay(Color color)
    {
        var renderer = EnsureWarningSurface();
        if (renderer == null)
            return;

        renderer.enabled = true;
        _warningBlock ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(_warningBlock);
        _warningBlock.SetColor("_BaseColor", color);
        _warningBlock.SetColor("_Color", color);
        renderer.SetPropertyBlock(_warningBlock);
    }

    public void HideWarningOverlay()
    {
        if (warningRenderer != null)
            warningRenderer.enabled = false;
    }

    public void RefreshTopSurfaceLayout()
    {
        if (topRenderer != null)
        {
            ConfigureTopSurfaceMesh(topRenderer.gameObject);
            AlignSurface(topRenderer, topSurfaceOffset);
        }

        if (warningRenderer != null)
        {
            ConfigureTopSurfaceMesh(warningRenderer.gameObject);
            AlignSurface(warningRenderer, warningSurfaceOffset);
        }
    }

    private Renderer EnsureTopSurface()
    {
        if (topRenderer == null)
        {
            var existing = transform.Find(TopSurfaceName);
            if (existing != null)
                topRenderer = existing.GetComponent<Renderer>();
        }

        if (topRenderer == null)
        {
            var top = new GameObject(TopSurfaceName);
            top.name = TopSurfaceName;
            top.transform.SetParent(transform, false);
            top.AddComponent<MeshFilter>();
            topRenderer = top.AddComponent<MeshRenderer>();
        }

        ConfigureTopSurfaceMesh(topRenderer.gameObject);
        AlignSurface(topRenderer, topSurfaceOffset);
        return topRenderer;
    }

    private Renderer EnsureWarningSurface()
    {
        if (warningRenderer == null)
        {
            var existing = transform.Find(WarningSurfaceName);
            if (existing != null)
                warningRenderer = existing.GetComponent<Renderer>();
        }

        if (warningRenderer == null)
        {
            var warning = new GameObject(WarningSurfaceName);
            warning.name = WarningSurfaceName;
            warning.transform.SetParent(transform, false);
            warning.AddComponent<MeshFilter>();
            warningRenderer = warning.AddComponent<MeshRenderer>();
        }

        ConfigureTopSurfaceMesh(warningRenderer.gameObject);
        warningRenderer.sharedMaterial = GetWarningOverlayMaterial();
        warningRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        warningRenderer.receiveShadows = false;
        AlignSurface(warningRenderer, warningSurfaceOffset);
        return warningRenderer;
    }

    private static void ConfigureTopSurfaceMesh(GameObject top)
    {
        if (top == null)
            return;

        if (!top.TryGetComponent<MeshFilter>(out var meshFilter))
            meshFilter = top.AddComponent<MeshFilter>();

        meshFilter.sharedMesh = GetTopSurfaceMesh();
    }

    private static Mesh GetTopSurfaceMesh()
    {
        if (_topSurfaceMesh != null)
            return _topSurfaceMesh;

        _topSurfaceMesh = new Mesh
        {
            name = "BoardTileTopSurfaceMesh"
        };

        _topSurfaceMesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, 0.5f)
        };

        _topSurfaceMesh.triangles = new[]
        {
            0, 2, 1,
            2, 3, 1
        };

        _topSurfaceMesh.uv = new[]
        {
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f)
        };

        _topSurfaceMesh.RecalculateNormals();
        _topSurfaceMesh.RecalculateBounds();
        return _topSurfaceMesh;
    }

    private void AlignSurface(Renderer surfaceRenderer, float offset)
    {
        var renderer = ResolveBaseRenderer();
        if (renderer == null || surfaceRenderer == null)
            return;

        Bounds bounds = renderer.bounds;
        Transform topTransform = surfaceRenderer.transform;
        Vector3 worldCenter = new Vector3(bounds.center.x, bounds.max.y + offset, bounds.center.z);

        topTransform.position = worldCenter;
        topTransform.rotation = Quaternion.identity;

        Vector3 parentScale = transform.lossyScale;
        float scaleX = Mathf.Approximately(parentScale.x, 0f) ? bounds.size.x : bounds.size.x / parentScale.x;
        float scaleZ = Mathf.Approximately(parentScale.z, 0f) ? bounds.size.z : bounds.size.z / parentScale.z;
        topTransform.localScale = new Vector3(
            Mathf.Abs(scaleX),
            1f,
            Mathf.Abs(scaleZ));
    }

    private static Material GetWarningOverlayMaterial()
    {
        if (_warningOverlayMaterial != null)
            return _warningOverlayMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Standard");

        _warningOverlayMaterial = new Material(shader)
        {
            name = "BoardTileWarningOverlay_Runtime",
            renderQueue = 3000
        };

        _warningOverlayMaterial.color = new Color(1f, 0.08f, 0f, 0.45f);
        _warningOverlayMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _warningOverlayMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _warningOverlayMaterial.SetInt("_ZWrite", 0);
        _warningOverlayMaterial.SetFloat("_Surface", 1f);
        _warningOverlayMaterial.EnableKeyword("_ALPHABLEND_ON");
        _warningOverlayMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return _warningOverlayMaterial;
    }

    private static bool IsManagedSurfaceRenderer(Renderer renderer)
    {
        if (renderer == null)
            return false;

        string rendererName = renderer.transform.name;
        return rendererName == TopSurfaceName || rendererName == WarningSurfaceName;
    }

    private static void DestroyObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
