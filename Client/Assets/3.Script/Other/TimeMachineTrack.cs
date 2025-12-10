using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

public class TimeMachineTrack : MonoBehaviour
{
    private float originalTimeScale = 1f;
    [SerializeField]
    public float transitionTime;
    [SerializeField]
    public float slowFactor;
    // timeScale을 부드럽게 변경하는 내부 함수
    private async UniTask ChangeTimeScale(float targetTimeScale, float transitionTime)
    {
        float currentTimeScale = Time.timeScale;
        float startTime = Time.unscaledTime;
        float elapsedTime = 0f;

        while (elapsedTime < transitionTime)
        {
            elapsedTime = Time.unscaledTime - startTime;
            Time.timeScale = Mathf.Lerp(currentTimeScale, targetTimeScale, elapsedTime / transitionTime);
            await UniTask.Yield(); // 매 프레임 대기 (timeScale의 영향을 받지 않음)
        }

        Time.timeScale = targetTimeScale;
    }

    public void  PlaySlowMotion()
    {
        Time.timeScale = slowFactor;
    }

    public void EndSlowMotion()
    {
        ChangeTimeScale(originalTimeScale, transitionTime).Forget();
    }

}
