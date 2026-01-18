using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LoginView : MonoBehaviour
{
    [Header("Identity")]
    public TMP_Text DeviceIdText = null!;
    public Button CopyDeviceIdButton = null!;
    public Button ResetDeviceIdButton = null!;

    [Header("Action")]
    public Button LoginButton = null!;

    [Header("Feedback")]
    public TMP_Text ErrorText = null!;
    public GameObject Busy = null!;

    public void SetBusy(bool on)
    {
        if (Busy) Busy.SetActive(on);

        if (LoginButton) LoginButton.interactable = !on;
        if (CopyDeviceIdButton) CopyDeviceIdButton.interactable = !on;
        if (ResetDeviceIdButton) ResetDeviceIdButton.interactable = !on;
    }

    public void SetError(string msg)
    {
        if (!ErrorText) return;
        ErrorText.text = msg ?? "";
        ErrorText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
    }
}
