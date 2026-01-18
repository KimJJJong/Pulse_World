using UnityEngine;
public sealed class NetBootstrap : MonoBehaviour
{
    public static NetBootstrap Instance { get; private set; }

    public static NetBootstrap Ensure()
    {
        if (Instance != null) return Instance;

        var go = new GameObject("NetBootstrap");
        var inst = go.AddComponent<NetBootstrap>();
        return inst;
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        //EnsureComponent<SessionContext>(); // static 으로 사용중
        EnsureComponent<NetworkManager>();
        EnsureComponent<ClientFlow>();
        //EnsureComponent<TimeSync>();  // static 으로 사용중
        // PingManager는 "연결 성공 후" 생성해도 됨 (네가 지금처럼)
    }

    static T EnsureComponent<T>() where T : Component
    {
        var c = FindFirstObjectByType<T>();
        if (c != null) return c;

        var go = new GameObject(typeof(T).Name);
        DontDestroyOnLoad(go);
        return go.AddComponent<T>();
    }

    public void Shutdown()
    {
        // 로그아웃 시 호출: 전부 끄고 파괴
        Destroy(gameObject);
        Instance = null;
    }
}
