using UnityEngine;

public sealed class TownBootstrap : MonoBehaviour
{
    bool _sent;

    void Start()
    {
        if (!NetworkManager.Instance.IsReady)
        {
            Debug.LogWarning("[Town] Not ready. You should return to Login or reconnect.");
            return;
        }

        SendTownEnterOnce();
    }

    void SendTownEnterOnce()
    {
        if (_sent) return;
        _sent = true;

        // 네 프로토콜에 맞춰 패킷 이름/필드 변경
        var p = new CS_TownEnter
        {
            // 예: uid 등 필요하면 넣기
        };

        NetworkManager.Instance.Send(p.Write());
    }
}
