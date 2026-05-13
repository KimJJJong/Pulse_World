using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LoadingSceneController : MonoBehaviour
{
    public static string TargetSceneName = SceneNames.Game; // Default or set before loading

    [Header("UI References")]
    public Slider ProgressBar;
    public TextMeshProUGUI ProgressText;
    public CanvasGroup LoadingCanvasGroup;
    public TextMeshProUGUI StatusText;
    public TextMeshProUGUI TipTitleText;
    public TextMeshProUGUI TipBodyText;
    public Image ProgressFillImage;
    public RectTransform ProgressMarker;
    public RectTransform ProgressTrack;

    [Header("Settings")]
    public float MinimumLoadingTime = 1.0f; // 최소 로딩 시간 (너무 빨라서 깜빡임 방지)
    public float ExitFadeDuration = 0.65f; // 로딩 종료 후 다음 씬이 자연스럽게 드러나는 시간
    public float IntroProgressDuration = 0.6f; // 실제 로딩 시작 전 0%부터 보여주는 연출 시간
    public float IntroProgressTarget = 0.2f; // 인트로 연출이 도달할 진행률
    public float ProgressDisplaySpeed = 0.8f; // 실제 로딩 값으로 UI가 따라가는 속도
    public float MapGenerationTimeout = 30f; // 무한 로딩 방지

    private static readonly string[] StatusMessages =
    {
        "Preparing Expedition...",
        "Tuning the rhythm...",
        "Opening the tavern doors...",
        "Marking the dungeon route...",
        "Almost ready..."
    };

    private static readonly (string Title, string Body)[] Tips =
    {
        ("Strike on the beat!", "Hitting notes on the beat deals more damage and builds more <color=#38CFE7>Focus</color>."),
        ("Watch enemy tells.", "Strong attacks usually announce themselves before the rhythm window closes."),
        ("Keep the combo alive.", "Clean timing keeps your expedition moving and makes every turn safer."),
        ("Spend Focus wisely.", "Save Focus for burst turns or when the party needs a quick recovery.")
    };

    private float _displayedProgress;

    private void Awake()
    {
        if (LoadingCanvasGroup != null)
        {
            LoadingCanvasGroup.alpha = 1f;
        }

        SetDisplayedProgressImmediate(0f);
    }

    private void Start()
    {
        ApplyTip();
        StartCoroutine(Co_LoadSceneSequence());
    }

    private IEnumerator Co_LoadSceneSequence()
    {
        // 1. 초기화
        SetDisplayedProgressImmediate(0f);
        float startTime = Time.time;

        // LoadingScene 첫 프레임이 반드시 0% 상태로 그려지게 한 뒤,
        // 실제 씬 로딩 전에 짧은 연출 진행을 보여준다.
        yield return null;
        yield return Co_PlayIntroProgress();

        // 2. 씬 로딩 (Additive)
        // 기존 씬이 있다면 언로드해야 할 수도 있지만, 
        // 보통 LoadingScene은 단독으로 로드된 상태에서 TargetScene을 추가로 로드하거나,
        // SceneRouter에서 이전 씬을 다 내리고 LoadingScene만 남긴 상태라고 가정.
        
        AsyncOperation op = SceneManager.LoadSceneAsync(TargetSceneName, LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogError($"[LoadingSceneController] Failed to load target scene: {TargetSceneName}");
            SetStatus("Scene not found.");
            SetDisplayedProgressImmediate(1f);
            yield break;
        }

        op.allowSceneActivation = false; // 90%에서 대기

        while (!op.isDone)
        {
            // 씬 로딩 진행률 (0.0 ~ 0.9) -> 전체의 50%로 매핑
            float sceneProgress = Mathf.Clamp01(op.progress / 0.9f);
            float totalProgress = sceneProgress * 0.5f; 

            MoveDisplayedProgress(totalProgress);

            // 로딩 완료 임박
            if (op.progress >= 0.9f && _displayedProgress >= 0.49f)
            {
                op.allowSceneActivation = true;
            }

            yield return null;
        }

        yield return Co_MoveDisplayedProgressTo(0.5f);

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

                MoveDisplayedProgress(totalProgress);
                
                yield return null;

                if (Time.time - waitMapStartTime > MapGenerationTimeout)
                {
                    Debug.LogWarning("[LoadingSceneController] Map generation wait timed out.");
                    break;
                }
            }
        }
        
        // 5. 완료 및 마무리
        yield return Co_MoveDisplayedProgressTo(1.0f);

        // 최소 로딩 시간 준수
        while (Time.time - startTime < MinimumLoadingTime)
        {
            yield return null;
        }

        yield return FadeOut();

        // 6. 로딩 씬 종료 (언로드 또는 UI 숨김)
        // Additive로 로드했으니 LoadingScene을 언로드해야 GameScene만 남음
        SceneManager.UnloadSceneAsync("LoadingScene");
    }

    private void SetDisplayedProgressImmediate(float value)
    {
        _displayedProgress = Mathf.Clamp01(value);
        SetProgress(_displayedProgress);
    }

    private void MoveDisplayedProgress(float target)
    {
        target = Mathf.Max(_displayedProgress, Mathf.Clamp01(target));
        float speed = Mathf.Max(0.01f, ProgressDisplaySpeed);
        _displayedProgress = Mathf.MoveTowards(_displayedProgress, target, speed * Time.unscaledDeltaTime);
        SetProgress(_displayedProgress);
    }

    private IEnumerator Co_PlayIntroProgress()
    {
        float duration = Mathf.Max(0.01f, IntroProgressDuration);
        float target = Mathf.Clamp01(IntroProgressTarget);
        float start = _displayedProgress;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float eased = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            SetDisplayedProgressImmediate(Mathf.Lerp(start, target, eased));
            yield return null;
        }

        SetDisplayedProgressImmediate(target);
    }

    private IEnumerator Co_MoveDisplayedProgressTo(float target)
    {
        target = Mathf.Clamp01(target);
        while (Mathf.Abs(_displayedProgress - target) > 0.001f)
        {
            MoveDisplayedProgress(target);
            yield return null;
        }

        SetDisplayedProgressImmediate(target);
    }

    private void SetProgress(float value)
    {
        value = Mathf.Clamp01(value);

        if (ProgressBar != null) ProgressBar.value = value;
        if (ProgressText != null) ProgressText.text = $"{(int)(value * 100)}%";
        if (ProgressFillImage != null) ProgressFillImage.fillAmount = value;
        if (ProgressMarker != null && ProgressTrack != null)
        {
            float trackWidth = ProgressTrack.rect.width;
            if (trackWidth <= 0.001f)
            {
                trackWidth = ProgressTrack.sizeDelta.x;
            }

            float halfWidth = trackWidth * 0.5f;
            Vector2 markerPosition = ProgressMarker.anchoredPosition;
            markerPosition.x = Mathf.Lerp(-halfWidth, halfWidth, value);
            ProgressMarker.anchoredPosition = markerPosition;
        }

        SetStatus(GetStatus(value));
    }

    private static string GetStatus(float value)
    {
        int index = Mathf.Clamp(Mathf.FloorToInt(value * StatusMessages.Length), 0, StatusMessages.Length - 1);
        return StatusMessages[index];
    }

    private void SetStatus(string message)
    {
        if (StatusText != null) StatusText.text = message;
    }

    private void ApplyTip()
    {
        if (Tips.Length == 0) return;

        var tip = Tips[Random.Range(0, Tips.Length)];
        if (TipTitleText != null) TipTitleText.text = tip.Title;
        if (TipBodyText != null) TipBodyText.text = tip.Body;
    }

    private IEnumerator FadeOut()
    {
        if (LoadingCanvasGroup == null) yield break;

        float duration = Mathf.Max(0.01f, ExitFadeDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            LoadingCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }

        LoadingCanvasGroup.alpha = 0f;
    }
}
