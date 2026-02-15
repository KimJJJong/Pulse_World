using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TownView : MonoBehaviour
{
    [Header("Town Ticket")]
    public TMP_InputField TownPreferredRegion = null!;
    public Button TownIssueButton = null!;
    public TMP_Text TownTicketIdText = null!;
    public TMP_Text TownExpireText = null!;
    public TMP_Text TownEndpointText = null!;
    public Button TownConnectButton = null!;

  

    [Header("Feedback")]
    public TMP_Text StatusText = null!;
    public GameObject Busy = null!;

    public void SetBusy(bool on)
    {
        if (Busy) Busy.SetActive(on);

        if (TownIssueButton) TownIssueButton.interactable = !on;
        if (TownConnectButton) TownConnectButton.interactable = !on;


    }

    public void SetStatus(string msg)
    {
        if (!StatusText) return;
        StatusText.text = msg ?? "";
        StatusText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
    }
}
