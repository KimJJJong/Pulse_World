using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemQuantityPopupUI : MonoBehaviour
{
    [SerializeField] private Slider _slider;
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private Button _confirmBtn;
    [SerializeField] private Button _cancelBtn;
    [SerializeField] private Button _bgBtn; // Close on BG click

    private int _maxAmount;
    private int _currentAmount;
    private System.Action<int> _onConfirm;

    private void Awake()
    {
        if (_slider != null) _slider.onValueChanged.AddListener(OnSliderChanged);
        if (_inputField != null) _inputField.onValueChanged.AddListener(OnInputChanged);
        if (_confirmBtn != null) _confirmBtn.onClick.AddListener(OnConfirm);
        if (_cancelBtn != null) _cancelBtn.onClick.AddListener(Close);
        if (_bgBtn != null) _bgBtn.onClick.AddListener(Close);
    }

    public void Open(string itemName, int maxAmount, System.Action<int> onConfirm)
    {
        _maxAmount = maxAmount;
        _onConfirm = onConfirm;
        
        if (_titleText != null) _titleText.text = $"Discard {itemName}";
        
        if (_slider != null)
        {
            _slider.minValue = 1;
            _slider.maxValue = maxAmount;
            _slider.value = maxAmount; // Default to max? or 1?
        }
        
        UpdateAmount(maxAmount);
        gameObject.SetActive(true);
    }

    private void OnSliderChanged(float val)
    {
        UpdateAmount((int)val);
    }

    private void OnInputChanged(string val)
    {
        if (int.TryParse(val, out int result))
        {
            int clamped = Mathf.Clamp(result, 1, _maxAmount);
            if (clamped != result)
            {
                _inputField.text = clamped.ToString();
            }
            _currentAmount = clamped;
            if (_slider != null) _slider.SetValueWithoutNotify(clamped);
        }
    }

    private void UpdateAmount(int amt)
    {
        _currentAmount = amt;
        if (_inputField != null) _inputField.text = amt.ToString();
        if (_slider != null) _slider.SetValueWithoutNotify(amt);
    }

    private void OnConfirm()
    {
        Debug.Log($"[ItemQuantityPopupUI] OnConfirm Clicked. Amount: {_currentAmount}");
        _onConfirm?.Invoke(_currentAmount);
        Close();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
