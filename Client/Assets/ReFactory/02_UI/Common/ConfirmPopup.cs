using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ConfirmPopup : MonoBehaviour
{
    public TMP_Text Title = null!;
    public TMP_Text Message = null!;
    public Button OkButton = null!;
    public Button CancelButton = null!;

    Action? _onOk;
    Action? _onCancel;

    public void Show(string title, string message, Action onOk, Action? onCancel = null)
    {
        Title.text = title;
        Message.text = message;

        _onOk = onOk;
        _onCancel = onCancel;

        gameObject.SetActive(true);
    }

    void Awake()
    {
        OkButton.onClick.AddListener(() =>
        {
            var cb = _onOk;
            Hide();
            cb?.Invoke();
        });

        CancelButton.onClick.AddListener(() =>
        {
            var cb = _onCancel;
            Hide();
            cb?.Invoke();
        });

        gameObject.SetActive(false);
    }

    void Hide()
    {
        _onOk = null;
        _onCancel = null;
        gameObject.SetActive(false);
    }
}
