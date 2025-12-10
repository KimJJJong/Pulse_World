using UnityEngine;

public static class Json
{
    public static string Dump<T>(T v) => JsonUtility.ToJson(v);
    public static T Parse<T>(string s) => JsonUtility.FromJson<T>(s);
}