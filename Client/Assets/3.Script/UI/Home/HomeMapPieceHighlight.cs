using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Image))]
public sealed class HomeMapPieceHighlight : MonoBehaviour
{
    private const string ShaderResourcePath = "UI/UI_Map/S_MapPieceOutline";
    private const string ShaderName = "UI/RhythmRPG/Map Piece Outline";

    private static Material _sharedMaterial;

    [SerializeField] private Image _image;
    [SerializeField] private Color _selectedColor = new Color(1f, 0.78f, 0.18f, 1f);
    [SerializeField] private bool _selected;
    [SerializeField] [Range(0.5f, 8f)] private float _outlineWidth = 2.5f;
    [SerializeField] [Range(0f, 1f)] private float _outlineAlpha = 0.92f;
    [SerializeField] [Range(0f, 1f)] private float _fillAlpha = 0.16f;

    private Sprite _runtimeSprite;

    public void Configure(Sprite sprite)
    {
        EnsureImage();
        if (_image == null)
            return;

        if (sprite != null)
            _image.sprite = sprite;

        ConfigureImage();
        Apply();
    }

    public void Configure(Texture2D texture)
    {
        EnsureImage();
        if (_image == null || texture == null)
            return;

        if (_runtimeSprite == null || _runtimeSprite.texture != texture)
        {
            _runtimeSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            _runtimeSprite.name = $"{texture.name}_MapPieceHighlight";
        }

        Configure(_runtimeSprite);
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        Apply();
    }

    private void Awake()
    {
        EnsureImage();
        ConfigureImage();
        Apply();
    }

    private void OnEnable()
    {
        EnsureImage();
        ConfigureImage();
        Apply();
    }

    private void OnValidate()
    {
        EnsureImage();
        ConfigureImage();
        Apply();
    }

    private void EnsureImage()
    {
        if (_image == null)
            _image = GetComponent<Image>();
    }

    private void ConfigureImage()
    {
        if (_image == null)
            return;

        _image.raycastTarget = false;
        _image.type = Image.Type.Simple;
        _image.preserveAspect = false;
        _image.useSpriteMesh = false;
        _image.material = GetSharedMaterial();
    }

    private void Apply()
    {
        if (_image == null)
            return;

        var color = _selected ? _selectedColor : _selectedColor;
        color.a = _selected ? _selectedColor.a : 0f;
        _image.color = color;
        _image.enabled = _selected;

        var material = GetSharedMaterial();
        if (material == null)
            return;

        material.SetFloat("_OutlineWidth", _outlineWidth);
        material.SetFloat("_OutlineAlpha", _outlineAlpha);
        material.SetFloat("_FillAlpha", _fillAlpha);
        if (_image.material != material)
            _image.material = material;
    }

    private static Material GetSharedMaterial()
    {
        if (_sharedMaterial != null)
            return _sharedMaterial;

        var shader = Resources.Load<Shader>(ShaderResourcePath);
        if (shader == null)
            shader = Shader.Find(ShaderName);
        if (shader == null)
            return null;

        _sharedMaterial = new Material(shader)
        {
            name = "M_MapPieceOutline_Runtime",
            hideFlags = HideFlags.HideAndDontSave
        };
        _sharedMaterial.SetFloat("_OutlineWidth", 2.5f);
        _sharedMaterial.SetFloat("_OutlineAlpha", 0.92f);
        _sharedMaterial.SetFloat("_FillAlpha", 0.16f);
        return _sharedMaterial;
    }
}
