using UnityEngine;

public sealed class KeepAliveOnPlay : MonoBehaviour
{
    void Awake()
    {
        // 플레이 상태에서만 DontDestroyOnLoad 호출
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);
    }
}
