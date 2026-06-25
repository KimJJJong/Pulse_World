using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PulseWorldTitleButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private RectTransform _rect;
    private Vector3 _targetScale = Vector3.one;

    public void Initialize(RectTransform rect)
    {
        _rect = rect;
    }

    private void Awake()
    {
        if (_rect == null)
            _rect = transform as RectTransform;
    }

    private void Update()
    {
        if (_rect == null)
            return;

        _rect.localScale = Vector3.Lerp(_rect.localScale, _targetScale, Time.unscaledDeltaTime * 12f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _targetScale = new Vector3(1.035f, 1.035f, 1f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _targetScale = Vector3.one;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _targetScale = new Vector3(0.985f, 0.985f, 1f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _targetScale = new Vector3(1.035f, 1.035f, 1f);
    }
}
