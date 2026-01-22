//using UnityEngine;

//public sealed class SceneTransit : MonoBehaviour
//{
//    public static SceneTransit I { get; private set; }
//    // ① 런타임 시작 전에 보장: 없으면 만든다
//    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
//    static void Ensure()
//    {
//        if (I == null)
//        {
//            var go = new GameObject("SceneTransit");
//            go.AddComponent<SceneTransit>();   // → Awake에서 I가 세팅됨
//        }
//    }
//    void Awake() { if (I != null) { Destroy(gameObject); return; } I = this; DontDestroyOnLoad(gameObject); }

//    string _nextScene;
//    RoomLaunchPayload _payload;

//    public void SetNext(string target, RoomLaunchPayload payload = null)
//    { _nextScene = target; _payload = payload; }

//    public string PeekTarget() => _nextScene;             // 로딩 씬 전용
//    public RoomLaunchPayload ConsumePayload() { var p = _payload; _payload = null; return p; } // 타겟 씬에서 1회 소비
//    public void ClearTarget() { _nextScene = null; }       // 로딩 끝나면 타겟 문자열만 비우기(선택)
//}
