using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class HomeUIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private RectTransform _scaleTarget;
    [SerializeField] private Graphic _tintTarget;
    [SerializeField] private float _hoverScale = 1.035f;
    [SerializeField] private float _pressedScale = 0.965f;
    [SerializeField] private float _response = 18f;
    [SerializeField] private Color _hoverTint = new Color(1f, 0.92f, 0.68f, 1f);
    [SerializeField] private Color _pressedTint = new Color(0.76f, 1f, 0.96f, 1f);
    [SerializeField] private Color _transparentHoverTint = new Color(1f, 0.82f, 0.28f, 0.18f);
    [SerializeField] private Color _transparentPressedTint = new Color(1f, 0.70f, 0.16f, 0.28f);

    private Vector3 _baseScale = Vector3.one;
    private Color _baseColor = Color.white;
    private Selectable _selectable;
    private bool _hovered;
    private bool _pressed;
    private bool _selected;
    private bool _captured;

    public void Configure(RectTransform scaleTarget, Graphic tintTarget)
    {
        _scaleTarget = scaleTarget != null ? scaleTarget : transform as RectTransform;
        _tintTarget = tintTarget != null ? tintTarget : FindFallbackGraphic();
        CaptureBaseState(force: true);
    }

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
        if (_scaleTarget == null)
            _scaleTarget = transform as RectTransform;
        if (_tintTarget == null)
            _tintTarget = FindFallbackGraphic();

        CaptureBaseState(force: true);
    }

    private void OnEnable()
    {
        CaptureBaseState(force: false);
        ApplyImmediate();
    }

    private void OnDisable()
    {
        _hovered = false;
        _pressed = false;
        _selected = false;
        RestoreImmediate();
    }

    private void Update()
    {
        if (!_captured)
            CaptureBaseState(force: true);

        var interactable = _selectable == null || _selectable.IsInteractable();
        var targetScale = interactable && _pressed
            ? _pressedScale
            : interactable && (_hovered || _selected)
                ? _hoverScale
                : 1f;

        if (_scaleTarget != null)
        {
            var desiredScale = _baseScale * targetScale;
            _scaleTarget.localScale = Vector3.Lerp(_scaleTarget.localScale, desiredScale, GetBlend());
        }

        if (_tintTarget != null)
        {
            var desiredColor = GetTargetColor(interactable);
            _tintTarget.color = Color.Lerp(_tintTarget.color, desiredColor, GetBlend());
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        _pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pressed = false;
    }

    public void OnSelect(BaseEventData eventData)
    {
        _selected = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _selected = false;
        _pressed = false;
    }

    private void CaptureBaseState(bool force)
    {
        if (_captured && !force)
            return;

        if (_scaleTarget != null)
            _baseScale = _scaleTarget.localScale;

        if (_tintTarget != null)
            _baseColor = _tintTarget.color;

        _captured = true;
    }

    private Graphic FindFallbackGraphic()
    {
        var graphic = GetComponent<Graphic>();
        if (graphic != null)
            return graphic;

        return GetComponentInChildren<Graphic>(true);
    }

    private Color GetTargetColor(bool interactable)
    {
        if (!interactable)
        {
            var disabled = _baseColor;
            disabled.a *= 0.55f;
            return disabled;
        }

        if (_baseColor.a <= 0.01f)
        {
            if (_pressed)
                return _transparentPressedTint;
            if (_hovered || _selected)
                return _transparentHoverTint;
            return _baseColor;
        }

        if (_pressed)
            return Color.Lerp(_baseColor, _pressedTint, 0.35f);
        if (_hovered || _selected)
            return Color.Lerp(_baseColor, _hoverTint, 0.28f);
        return _baseColor;
    }

    private float GetBlend()
    {
        return 1f - Mathf.Exp(-Mathf.Max(0.01f, _response) * Time.unscaledDeltaTime);
    }

    private void ApplyImmediate()
    {
        if (_scaleTarget != null)
            _scaleTarget.localScale = _baseScale;
        if (_tintTarget != null)
            _tintTarget.color = GetTargetColor(_selectable == null || _selectable.IsInteractable());
    }

    private void RestoreImmediate()
    {
        if (_scaleTarget != null)
            _scaleTarget.localScale = _baseScale;
        if (_tintTarget != null)
            _tintTarget.color = _baseColor;
    }
}
