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

    [Header("Game Ticket")]
    public TMP_InputField GamePreferredRegion = null!;
    public TMP_InputField GameRoomId = null!;
    public TMP_InputField GameMap = null!;
    public TMP_InputField GameMaxPlayers = null!;
    public Button GameIssueButton = null!;
    public TMP_Text GameTransitionIdText = null!;
    public TMP_Text GameTicketIdText = null!;
    public TMP_Text GameExpireText = null!;
    public TMP_Text GameServerIdText = null!;
    public TMP_Text GameEndpointText = null!;
    public TMP_Text GameKeyText = null!;
    public Button GameConnectButton = null!;

    [Header("Feedback")]
    public TMP_Text StatusText = null!;
    public GameObject Busy = null!;

    public void SetBusy(bool on)
    {
        if (Busy) Busy.SetActive(on);

        if (TownIssueButton) TownIssueButton.interactable = !on;
        if (TownConnectButton) TownConnectButton.interactable = !on;

        if (GameIssueButton) GameIssueButton.interactable = !on;
        if (GameConnectButton) GameConnectButton.interactable = !on;
    }

    public void SetStatus(string msg)
    {
        if (!StatusText) return;
        StatusText.text = msg ?? "";
        StatusText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
    }
}
