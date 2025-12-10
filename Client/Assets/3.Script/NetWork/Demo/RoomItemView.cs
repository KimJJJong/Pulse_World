using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Contracts.Packet; // ★ 공통 패킷(RoomDto)

public class RoomItemView : MonoBehaviour, IPointerClickHandler
{
    public Text TitleText;
    public Text StatusText;
    public Text CurMaxText;
    public Text IdText;
    public Button JoinButton;

    private string _roomId;
    private RoomUIView _owner;

    public void Setup(RoomDto dto, RoomUIView owner)
    {
        _owner = owner;
        _roomId = dto.id;

        if (TitleText) TitleText.text = dto.title;
        if (StatusText) StatusText.text = dto.status;
        if (CurMaxText) CurMaxText.text = $"{dto.cur}/{dto.max}";
        if (IdText) IdText.text = dto.id;

        if (JoinButton)
        {
            JoinButton.onClick.RemoveAllListeners();
            JoinButton.onClick.AddListener(() => _owner.ClickJoinRoom(_roomId));
        }
    }

    // 리스트 아이템 어디를 클릭해도 Join
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!string.IsNullOrEmpty(_roomId))
            _owner.ClickJoinRoom(_roomId);
    }
}
