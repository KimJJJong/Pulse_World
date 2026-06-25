using System;
using System.Collections.Concurrent;
using UnityEngine;

public class MainThreadDispatcher : MonoBehaviour
{
    static MainThreadDispatcher _inst;
    static readonly ConcurrentQueue<Action> _q = new ConcurrentQueue<Action>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (_inst != null) return;
        var go = new GameObject(nameof(MainThreadDispatcher));
        DontDestroyOnLoad(go);
        _inst = go.AddComponent<MainThreadDispatcher>();
    }

    public static void Post(Action a)
    {
        if (a != null) _q.Enqueue(a);
    }

    void Update()
    {
        while (_q.TryDequeue(out var a))
        {
            try { a(); } catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}
