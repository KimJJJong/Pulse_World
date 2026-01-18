using System.Threading.Tasks;
using UnityEngine.SceneManagement;

public static class SceneRouter
{
    public static async Task LoadAsync(string scene)
    {
        // 필요하면 여기서 LoadingOverlay 켜도 됨
        var op = SceneManager.LoadSceneAsync(scene);
        while (!op.isDone)
            await Task.Yield();
    }
    public static async void Load(string scene)
    {
        // 필요하면 여기서 LoadingOverlay 켜도 됨
        var op = SceneManager.LoadSceneAsync(scene);
        while (!op.isDone)
            await Task.Yield();
    }
}
