using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LoadingSceneController : MonoBehaviour
{
    public static string TargetSceneName = "GameScene"; // Default or set before loading

    [Header("UI References")]
    public Slider ProgressBar;
    public TextMeshProUGUI ProgressText;
    public CanvasGroup LoadingCanvasGroup;

    [Header("Settings")]
    public float MinimumLoadingTime = 1.0f; // 최소 로딩 시간 (너무 빨라서 깜빡임 방지)

    private void Start()
    {
        StartCoroutine(Co_LoadSceneSequence());
    }

    private IEnumerator Co_LoadSceneSequence()
    {
        // 1. 초기화
        SetProgress(0f);
        float startTime = Time.time;

        // 2. 씬 로딩 (Additive)
        // 기존 씬이 있다면 언로드해야 할 수도 있지만, 
        // 보통 LoadingScene은 단독으로 로드된 상태에서 TargetScene을 추가로 로드하거나,
        // SceneRouter에서 이전 씬을 다 내리고 LoadingScene만 남긴 상태라고 가정.
        
        AsyncOperation op = SceneManager.LoadSceneAsync(TargetSceneName, LoadSceneMode.Additive);
        op.allowSceneActivation = false; // 90%에서 대기

        while (!op.isDone)
        {
            // 씬 로딩 진행률 (0.0 ~ 0.9) -> 전체의 50%로 매핑
            float sceneProgress = Mathf.Clamp01(op.progress / 0.9f);
            float totalProgress = sceneProgress * 0.5f; 

            SetProgress(totalProgress);

            // 로딩 완료 임박
            if (op.progress >= 0.9f)
            {
                op.allowSceneActivation = true;
            }

            yield return null;
        }

        // 3. 씬 활성화 후 초기화 대기 (Map Generation 등)
        // 씬이 로드되었다고 바로 객체들이 Awake/Start 된 것은 아닐 수 있음 (프레임 대기)
        yield return null; 

        // 4. ClientGameState 맵 생성 대기
        // GameScene에 ClientGameState가 있다고 가정.
        // 없으면(타이틀 등) 그냥 패스
        
        if (ClientGameState.Instance != null)
        {
            // 아직 맵 생성 시작 전일 수 있으므로 잠시 대기하거나 플래그 확인
            // 네트워킹 게임이라면 서버에서 InitMap 패킷이 와야 생성이 시작됨.
            // 즉, "서버 연결 & 맵 패킷 수신 & 맵 생성 완료" 까지 기다려야 함.
            
            // 타임아웃 설정 (무한 로딩 방지, 예: 30초)
            float waitMapStartTime = Time.time;
            
            while (!ClientGameState.Instance.IsMapGenerationComplete)
            {
                // 맵 생성 중 진행률 (ClientGameState에서 제공한다고 가정)
                // 0.5 ~ 1.0 구간 매핑
                float mapProgress = ClientGameState.Instance.MapGenProgress; 
                float totalProgress = 0.5f + (mapProgress * 0.5f);

                SetProgress(totalProgress);
                
                yield return null;

                // (옵션) 타임아웃 처리
                // if (Time.time - waitMapStartTime > 30f) break; 
            }
        }
        
        // 5. 완료 및 마무리
        SetProgress(1.0f);

        // 최소 로딩 시간 준수
        while (Time.time - startTime < MinimumLoadingTime)
        {
            yield return null;
        }

        // 6. 로딩 씬 종료 (언로드 또는 UI 숨김)
        // Additive로 로드했으니 LoadingScene을 언로드해야 GameScene만 남음
        SceneManager.UnloadSceneAsync("LoadingScene");
    }

    private void SetProgress(float value)
    {
        if (ProgressBar != null) ProgressBar.value = value;
        if (ProgressText != null) ProgressText.text = $"{(int)(value * 100)}%";
    }
}
