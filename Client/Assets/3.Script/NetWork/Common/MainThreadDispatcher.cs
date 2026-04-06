using System;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// 소켓 수신 스레드 등 백그라운드 스레드에서 Unity 메인 스레드로 Action을 디스패치합니다.
///
/// [RTT Fix] 처리 경로 분리:
///   PostImmediate() : BeatSync / BeatActions 등 지연에 민감한 패킷용
///                     -> 현재 Update()에서 일반 큐보다 먼저(최우선) 처리
///   Post()          : 일반 패킷, 연결/해제 이벤트 등
///                     -> 매 Update() 말미에 처리 (순서 보장)
///
/// [DefaultExecutionOrder(-100)] 으로 다른 MonoBehaviour보다 먼저 Update() 실행.
/// -> 같은 프레임 내에서 Beat 패킷이 게임 로직보다 먼저 처리됨.
/// </summary>
[DefaultExecutionOrder(-100)]   // [RTT Fix] NetworkManager / 게임 로직보다 먼저 Update() 실행
public class MainThreadDispatcher : MonoBehaviour
{
    static MainThreadDispatcher _inst;

    // [RTT Fix] 우선순위 큐 분리
    // _priorityQ : Beat/BeatSync 등 즉각 반영 필요한 패킷 (먼저 처리)
    // _q         : 일반 패킷, 연결 이벤트 등
    static readonly ConcurrentQueue<Action> _priorityQ = new ConcurrentQueue<Action>();
    static readonly ConcurrentQueue<Action> _q         = new ConcurrentQueue<Action>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (_inst != null) return;
        var go = new GameObject(nameof(MainThreadDispatcher));
        DontDestroyOnLoad(go);
        _inst = go.AddComponent<MainThreadDispatcher>();
    }

    /// <summary>
    /// [RTT Fix] 우선 처리 큐 - BeatSync, BeatActions, BeatTelegraphs 전용.
    /// 같은 Update()에서 일반 Post()보다 먼저 소비됩니다.
    /// Thread-Safe (ConcurrentQueue).
    /// </summary>
    public static void PostImmediate(Action a)
    {
        if (a != null) _priorityQ.Enqueue(a);
    }

    /// <summary>일반 처리 큐 - 씬 오브젝트 조작, 연결/해제 이벤트 등. Thread-Safe.</summary>
    public static void Post(Action a)
    {
        if (a != null) _q.Enqueue(a);
    }

    void Update()
    {
        // [RTT Fix] 우선순위 큐 먼저 전부 소비 (Beat 패킷 → 판정 로직보다 선행)
        while (_priorityQ.TryDequeue(out var a))
        {
            try { a(); } catch (Exception ex) { Debug.LogException(ex); }
        }

        // 일반 큐 처리 (씬 오브젝트 조작 등)
        while (_q.TryDequeue(out var a))
        {
            try { a(); } catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}
