using System;
using UnityEngine;

public sealed class UiRefs : MonoBehaviour
{
    [Serializable]
    public struct Entry
    {
        public string key;
        public UnityEngine.Object obj;
    }

    public Entry[] entries = Array.Empty<Entry>();

    public T Get<T>(string key) where T : UnityEngine.Object
    {
        foreach (var e in entries)
            if (e.key == key)
                return e.obj as T;
        return null!;
    }
}
