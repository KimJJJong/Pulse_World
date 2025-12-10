
using Contracts.Packet;
using System.Collections.Generic;
using UnityEngine;

public class RoomController : MonoBehaviour
{
    private int roomCount; //서버에서 방 갯수 불러와서 없으면 UI 띄우기? 아니면 데베?

    [SerializeField] private GameObject emptRoomUIObj;
    [SerializeField] private ObjectPool objectPoolForUI;

    private void Start()
    {
        Test();
        if(roomCount < 1) { emptRoomUIObj.SetActive(true); }
        else
        {
            emptRoomUIObj.SetActive(false);
            //있는 방들 미리 CreatRoom
        }
    }

    void Test()
    {
        roomCount = 0;
    }

}
