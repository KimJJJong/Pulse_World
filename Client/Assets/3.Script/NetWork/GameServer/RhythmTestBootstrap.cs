using UnityEngine;

public class RhythmTestBootstrap : MonoBehaviour
{
    [Header("Optional 프리팹")]
    public GameObject entityPrefab;
    public Camera mainCamera;

    void Awake()
    {
        // 1) 카메라 없으면 생성
        if (Camera.main == null)
        {
            var camGo = new GameObject("MainCamera");
            mainCamera = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(5, 10, -10);
            camGo.transform.rotation = Quaternion.Euler(45, 45, 0);
        }
        else
        {
            mainCamera = Camera.main;
        }

        // 2) GameState
        if (ClientGameState.Instance == null)
        {
            var go = new GameObject("ClientGameState");
            go.AddComponent<ClientGameState>();
        }

        // 3) ClientHandlers
        if (ClientHandlers.Instance == null)
        {
            var go = new GameObject("ClientHandlers");
            go.AddComponent<ClientHandlers>();
        }

        // 4) RhythmClient
        if (RhythmClient.Instance == null)
        {
            var go = new GameObject("RhythmClient");
            go.AddComponent<RhythmClient>();
        }

        // 5) BoardView
        if (BoardView.Instance == null)
        {
            var go = new GameObject("BoardView");
            var bv = go.AddComponent<BoardView>();
            if (entityPrefab != null)
                bv.GetType().GetField("entityPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(bv, entityPrefab);
        }

        // 6) RhythmInputController
        if (FindFirstObjectByType<RhythmInputController>() == null)
        {
            var go = new GameObject("RhythmInputController");
            go.AddComponent<RhythmInputController>();
        }

        // 7) BeatDebugUI
        if (FindFirstObjectByType<BeatDebugUI_TMP>() == null)
        {
            var go = new GameObject("BeatDebugUI_TMP");
            go.AddComponent<BeatDebugUI_TMP>();
        }

        Debug.Log("[RhythmTestBootstrap] Setup complete. 준비 끝. 서버에 접속해서 패킷만 오면 바로 테스트 가능.");
    }
}
