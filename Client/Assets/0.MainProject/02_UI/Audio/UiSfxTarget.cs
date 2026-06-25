using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class UiSfxTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    private Selectable _selectable;
    private Button _button;
    private Toggle _toggle;
    private Dropdown _dropdown;
    private TMP_Dropdown _tmpDropdown;
    private bool _pointerInside;
    private bool _selected;
    private bool _bound;
    private float _armedTime;
    private float _directUserActionUntil;

    public void Bind(Selectable selectable)
    {
        _selectable = selectable != null ? selectable : GetComponent<Selectable>();
        CacheComponents();
        RebindListeners();
    }

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
        CacheComponents();
    }

    private void OnEnable()
    {
        _armedTime = Time.unscaledTime + 0.25f;
        if (_selectable == null)
            _selectable = GetComponent<Selectable>();

        CacheComponents();
        RebindListeners();
    }

    private void OnDisable()
    {
        _pointerInside = false;
        _selected = false;
        UnbindListeners();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _pointerInside = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _pointerInside = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        MarkDirectUserAction();
    }

    public void OnSelect(BaseEventData eventData)
    {
        _selected = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _selected = false;
    }

    public void OnSubmit(BaseEventData eventData)
    {
        MarkDirectUserAction();
    }

    private void CacheComponents()
    {
        _button = GetComponent<Button>();
        _toggle = GetComponent<Toggle>();
        _dropdown = GetComponent<Dropdown>();
        _tmpDropdown = GetComponent<TMP_Dropdown>();
    }

    private void RebindListeners()
    {
        if (_bound)
            UnbindListeners();

        if (_button != null)
            _button.onClick.AddListener(OnButtonClick);
        if (_toggle != null)
            _toggle.onValueChanged.AddListener(OnToggleValueChanged);
        if (_dropdown != null)
            _dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        if (_tmpDropdown != null)
            _tmpDropdown.onValueChanged.AddListener(OnDropdownValueChanged);

        _bound = true;
    }

    private void UnbindListeners()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnButtonClick);
        if (_toggle != null)
            _toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        if (_dropdown != null)
            _dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
        if (_tmpDropdown != null)
            _tmpDropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);

        _bound = false;
    }

    private void OnButtonClick()
    {
        if (!CanPlayUserAction())
            return;

        UiSfxService.PlayGlobal(UiSfxService.ClassifyButton(gameObject));
    }

    private void OnToggleValueChanged(bool value)
    {
        if (CanPlayUserChange())
            UiSfxService.PlayGlobal(UiSfxKind.Toggle);
    }

    private void OnDropdownValueChanged(int value)
    {
        if (CanPlayUserChange())
            UiSfxService.PlayGlobal(UiSfxKind.Confirm);
    }

    private bool CanPlayUserChange()
    {
        return CanPlay() && (HasRecentDirectUserAction() || _pointerInside || _selected || IsCurrentSelection());
    }

    private bool CanPlayUserAction()
    {
        return CanPlay() && (HasRecentDirectUserAction() || IsCurrentSelection());
    }

    private bool CanPlay()
    {
        return Time.unscaledTime >= _armedTime
            && _selectable != null
            && _selectable.IsActive()
            && _selectable.IsInteractable();
    }

    private bool IsCurrentSelection()
    {
        return EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
    }

    private void MarkDirectUserAction()
    {
        _directUserActionUntil = Time.unscaledTime + 0.25f;
    }

    private bool HasRecentDirectUserAction()
    {
        return Time.unscaledTime <= _directUserActionUntil;
    }
}
