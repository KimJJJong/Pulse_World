using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ProgressController : MonoBehaviour
{
    [SerializeField] Slider progressSlider;
    private SceneTransit sceneTransit;
    async void Awake()
    {
        await LoadSceneAsync();
    }

    private async UniTask LoadSceneAsync()
    {
        await UniTask.Yield();

        await UniTask.WaitUntil(() =>
        {
            sceneTransit = SceneTransit.I;

            return sceneTransit != null;
        });

        var nextScene = sceneTransit.PeekTarget();

        AsyncOperation op = SceneManager.LoadSceneAsync(nextScene);
        op.allowSceneActivation = false;

        float timer = 0.0f;

        while (!op.isDone)
        {
            await UniTask.Yield();
            timer += Time.deltaTime;
            if (op.progress < 0.9f)
            {
                progressSlider.value = Mathf.Lerp(progressSlider.value, op.progress, op.progress);
                if (progressSlider.value >= op.progress)
                {
                    timer = 0f;
                }
            }
            else
            {
                progressSlider.value = Mathf.Lerp(progressSlider.value, 1f, timer);
                if (progressSlider.value == 1.0f)
                {
                    op.allowSceneActivation = true;
                    return;
                }
            }
        }
        SceneManager.LoadScene(nextScene);
    }
}
