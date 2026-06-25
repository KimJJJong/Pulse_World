using UnityEngine;
using NetClient.Room.UI;

namespace NetClient.Room.UI
{
    public sealed class RoomUiHotkey : MonoBehaviour
    {
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                if (RoomUiRoot.Instance != null)
                    RoomUiRoot.Instance.Toggle();
            }
        }
    }
}
