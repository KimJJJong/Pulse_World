using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class HomeMapPieceHitArea : MonoBehaviour, ICanvasRaycastFilter
{
    [SerializeField] private RawImage _rawImage;
    [SerializeField] private Image _image;
    [SerializeField] [Range(0f, 1f)] private float _alphaThreshold = 0.1f;

    public void Configure()
    {
        EnsureTargets();
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        var rectTransform = transform as RectTransform;
        if (rectTransform == null)
            return true;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var localPoint))
            return false;

        var rect = rectTransform.rect;
        if (!rect.Contains(localPoint))
            return false;

        var normalized = new Vector2(
            Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x),
            Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y));

        if (!TryResolveTextureUv(normalized, out var texture, out var uv))
            return true;

        try
        {
            return texture.GetPixelBilinear(uv.x, uv.y).a >= _alphaThreshold;
        }
        catch (UnityException)
        {
            return true;
        }
    }

    private void Awake()
    {
        EnsureTargets();
    }

    private void OnEnable()
    {
        EnsureTargets();
    }

    private void OnValidate()
    {
        EnsureTargets();
    }

    private void EnsureTargets()
    {
        if (_rawImage == null)
            _rawImage = GetComponent<RawImage>();
        if (_image == null)
            _image = GetComponent<Image>();
    }

    private bool TryResolveTextureUv(Vector2 normalized, out Texture2D texture, out Vector2 uv)
    {
        EnsureTargets();

        if (_rawImage != null && _rawImage.texture is Texture2D rawTexture)
        {
            texture = rawTexture;
            var uvRect = _rawImage.uvRect;
            uv = new Vector2(
                uvRect.x + normalized.x * uvRect.width,
                uvRect.y + normalized.y * uvRect.height);
            return true;
        }

        if (_image != null && _image.sprite != null && _image.sprite.texture != null)
        {
            var sprite = _image.sprite;
            texture = sprite.texture;
            var textureRect = sprite.textureRect;
            uv = new Vector2(
                (textureRect.x + normalized.x * textureRect.width) / texture.width,
                (textureRect.y + normalized.y * textureRect.height) / texture.height);
            return true;
        }

        texture = null;
        uv = default;
        return false;
    }
}
