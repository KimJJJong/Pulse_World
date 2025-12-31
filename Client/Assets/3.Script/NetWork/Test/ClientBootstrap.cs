using UnityEngine;

public static class ClientBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureSystems()
    {
        if (Object.FindFirstObjectByType<SystemsRoot>() != null) return;

        var go = new GameObject("Systems");
        Object.DontDestroyOnLoad(go);

        go.AddComponent<SystemsRoot>();      // DDoL 재보증 + 로드 감시
        go.AddComponent<ClientGameState>();
        go.AddComponent<ClientHandlers>();
        go.AddComponent<MainThreadDispatcher>();

        if (Object.FindFirstObjectByType<NetWorkManager>() == null)
            Debug.LogWarning("[Bootstrap] NetworkManager.Instance 가 없습니다. 전송은 스텁 로그만 남습니다.");
    }
}
