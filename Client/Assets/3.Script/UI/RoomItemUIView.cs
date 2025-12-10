using Contracts.Packet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomItemUIView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [Header("0: Able 1:Enable")]
    [SerializeField] private GameObject[] activeObjs = new GameObject[2]; //0: Able 1:Enable
    [SerializeField] private Button joinButton;

    private string _roomId;
    private LobbyUIPresenter _owner;

    void SwichingActiveObjs(bool b)
    {
        if(activeObjs == null) return;
        activeObjs[0].SetActive(b);
        activeObjs[1].SetActive(!b);
    }

    public void Setup(RoomDto dto, LobbyUIPresenter owner)
    {
        _owner = owner;
        _roomId = dto.id;

        if (titleText) titleText.text = dto.title;

        if (dto.cur < dto.max) SwichingActiveObjs(true);
        else SwichingActiveObjs(false);

        if (joinButton)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() => _owner.ClickJoinRoom(_roomId));
        }
    }

}
