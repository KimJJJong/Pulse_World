using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NetClient.Room.UI
{
    public sealed class RoomListItemView : MonoBehaviour
    {
        [SerializeField] TMP_Text txtRoomName;
        [SerializeField] TMP_Text txtMap;
        [SerializeField] TMP_Text txtPlayers;
        [SerializeField] Button btnJoin;

        string _roomId;
        Action<string> _onJoin;

        public void Bind(RoomSummaryDto dto, Action<string> onJoin)
        {
            _roomId = dto.roomId;
            _onJoin = onJoin;

            if (txtRoomName) txtRoomName.text = string.IsNullOrEmpty(dto.title) ? dto.roomId : dto.title;
            if (txtMap) txtMap.text = $"Map: {dto.mapId}";
            if (txtPlayers) txtPlayers.text = $"Players: {dto.memberCount}/{dto.maxPlayers}";

            if (btnJoin)
            {
                btnJoin.onClick.RemoveAllListeners();
                btnJoin.onClick.AddListener(() => _onJoin?.Invoke(_roomId));
                btnJoin.interactable = true;
            }
        }
    }
}
