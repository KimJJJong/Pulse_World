using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BeatDebugUI_TMP : MonoBehaviour
{
    [Header("Optional: 수동 할당 안 하면 자동 생성")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private TextMeshProUGUI _beatText;
    [SerializeField] private Image _progressBar;

    [SerializeField] private float judgeWindowMs = 80f;

    [Header("Metronome Sound")]
    [SerializeField] private AudioSource _audioSource;   // 없으면 자동 생성
    [SerializeField] private AudioClip _beatClip;        // 1 beat마다 울릴 사운드
    [SerializeField] private float _beatVolume = 0.8f;   // OneShot 볼륨
    [SerializeField] private float soundOffsetMs = 60f;   // "비트보다 몇 ms 먼저" 울릴지(양수=더 일찍)
    private long _scheduledBeatIndex = long.MinValue;
    private RhythmClient Rhythm => RhythmClient.Instance;

    private long _lastBeatIndex = long.MinValue;

    void Awake()
    {
        //// (선택) DontDestroyOnLoad 중복 방지: 씬 전환 시 여러 개 생기는 경우 대비
        //var existing = FindFirstObjectByType<BeatDebugUI_TMP>();
        //if (existing == true)
        //{
        //    Destroy(gameObject);
        //    return;
        //}
        DontDestroyOnLoad(gameObject);

        if (_canvas == null)
            CreateCanvasAndUI();

        EnsureAudio();
    }

void Update()
{
        if (judgeWindowMs == 0)
            judgeWindowMs = RhythmClient.Instance.judgeWindowMs;

        if (Rhythm == null) return;

    long beatIndex = Rhythm.GetCurrentBeatIndex();
    double progress = Rhythm.GetCurrentBeatProgress01();
    long serverNow = Rhythm.GetCurrentServerTimeMs();
    double beatMs = Rhythm.GetBeatDurationMs();

    // 다음 비트까지 남은 시간(ms)
    double timeToNextBeatMs = (1.0 - progress) * beatMs;

    // 다음 비트 소리를 "남은 시간이 soundOffsetMs 이하일 때" 미리 1회 울리기
    if (_beatClip != null && _audioSource != null)
    {
        long nextBeatIndex = beatIndex + 1;

        // 이미 이번 nextBeatIndex에 대해 울렸으면 스킵
        if (_scheduledBeatIndex != nextBeatIndex)
        {
            if (timeToNextBeatMs <= soundOffsetMs)
            {
                // 첫 시작 프레임에서 갑자기 울리는 것 방지 옵션
                if (_lastBeatIndex != long.MinValue)
                    _audioSource.PlayOneShot(_beatClip, _beatVolume);

                _scheduledBeatIndex = nextBeatIndex;
            }
        }
    }

        if (_beatText != null)
        {
            _beatText.text =
                $"Beat: {beatIndex}\n" +
                $"Progress: {progress:0.00}\n" +
                $"Server: {serverNow} ms\n" +
                $"BeatMs: {beatMs:0.0}";
        }

        if (_progressBar != null)
        {
            _progressBar.fillAmount = (float)progress;

            double distToBeatMs = Mathf.Min(
                (float)(progress * beatMs),
                (float)((1.0 - progress) * beatMs)
            );

            _progressBar.color = distToBeatMs <= judgeWindowMs ? Color.green : Color.red;
        }
    }

    private void OnBeat(long beatIndex)
    {
        // 첫 프레임(초기화)에서 한 번 울리는 게 싫으면 여기서 막아도 됨
        // if (_lastBeatIndex == long.MinValue) return;

        if (_beatClip == null || _audioSource == null)
            return;

        _audioSource.PlayOneShot(_beatClip, _beatVolume);
    }

    private void EnsureAudio()
    {
        if (_audioSource != null)
            return;

        // AudioSource 자동 생성 (3D 영향 없게)
        _audioSource = gameObject.GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f; // 2D
        _audioSource.loop = false;
    }

    private void CreateCanvasAndUI()
    {
        // Canvas
        var canvasGo = new GameObject("BeatDebugCanvas_TMP");
        canvasGo.layer = LayerMask.NameToLayer("UI");
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        DontDestroyOnLoad(canvasGo);

        // Panel
        var panelGo = new GameObject("BeatPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0, 0, 0, 0.5f);

        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(10, -10);
        panelRect.sizeDelta = new Vector2(260, 90);

        // Text TMP
        var textGo = new GameObject("BeatText_TMP");
        textGo.transform.SetParent(panelGo.transform, false);
        _beatText = textGo.AddComponent<TextMeshProUGUI>();
        _beatText.alignment = TextAlignmentOptions.TopLeft;
        _beatText.fontSize = 18f;
        _beatText.color = Color.white;

        var textRect = _beatText.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0.35f);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.offsetMin = new Vector2(5, 5);
        textRect.offsetMax = new Vector2(-5, -5);

        // ProgressBar BG
        var barBgGo = new GameObject("BeatProgressBg");
        barBgGo.transform.SetParent(panelGo.transform, false);
        var barBgImg = barBgGo.AddComponent<Image>();
        barBgImg.color = new Color(1, 1, 1, 0.1f);

        var barBgRect = barBgGo.GetComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0, 0);
        barBgRect.anchorMax = new Vector2(1, 0.35f);
        barBgRect.offsetMin = new Vector2(5, 5);
        barBgRect.offsetMax = new Vector2(-5, -5);

        // ProgressBar
        var barGo = new GameObject("BeatProgress");
        barGo.transform.SetParent(barBgGo.transform, false);
        _progressBar = barGo.AddComponent<Image>();
        _progressBar.color = Color.green;
        _progressBar.type = Image.Type.Filled;
        _progressBar.fillMethod = Image.FillMethod.Horizontal;
        _progressBar.fillOrigin = (int)Image.OriginHorizontal.Left;

        var barRect = _progressBar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0, 0);
        barRect.anchorMax = new Vector2(1, 1);
        barRect.offsetMin = Vector2.zero;
        barRect.offsetMax = Vector2.zero;
    }
}
