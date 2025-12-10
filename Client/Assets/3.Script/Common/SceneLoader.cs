using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoader
{
    public static class Names
    {
        public const string Lobby = "Lobby";
        public const string Loading = "Loading";
        public const string Room = "MatchingRoom";
        public const string InGame = "InGame";
    }

    public static async UniTask LoadWithLoading(string targetScene, CancellationToken ct = default)
    {
        await SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single).ToUniTask(cancellationToken: ct);
    }
    public static void LoadWithLoading(string targetScene)
    {
        SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
    }
}
