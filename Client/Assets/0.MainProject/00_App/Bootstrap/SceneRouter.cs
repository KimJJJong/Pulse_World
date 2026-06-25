using System.Threading.Tasks;
using UnityEngine.SceneManagement;

public static class SceneRouter
{
    // [LoadingScene] 적용
    public static void ChangeSceneAsync(string targetSceneName)
    {
        // 1. LoadingSceneController에 타겟 설정
        LoadingSceneController.TargetSceneName = targetSceneName;

        // 2. LoadingScene 로드 (LoadingScene이 Start에서 TargetScene을 Additive로 로드함)
        SceneManager.LoadScene("LoadingScene");
    }

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
