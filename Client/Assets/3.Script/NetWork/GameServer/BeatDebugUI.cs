using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BeatDebugUI_TMP : MonoBehaviour
{
    [Header("Optional: 수동 할당 안 하면 자동 생성")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private TextMeshProUGUI _beatText;
    [SerializeField] private Image _progressBar;

    [Header("Judge")]
    [SerializeField] private float judgeWindowMs = 0f;

    [Header("Metronome Sound")]
    [SerializeField] private AudioSource _audioSource;   // 없으면 자동 생성
    [SerializeField] private AudioClip _beatClip;        // 1 beat마다 울릴 사운드
    [Range(0f, 1f)]
    [SerializeField] private float _beatVolume = 0.8f;

    [Header("Sound Offset")]
    [Tooltip("켜면 RTT 기반으로 소리 오프셋(ms)을 자동 계산")]
    [SerializeField] private bool autoOffsetFromRtt = true;

    [Tooltip("자동 오프셋 = -(oneWay + baseLeadMs). baseLeadMs는 사람 반응용 선행치")]
    [SerializeField] private float baseLeadMs = 60f;  // 40~80 추천

    [Tooltip("자동 오프셋의 최소/최대(음수 범위). 너무 과하게 미리 울리는 것 방지")]
    [SerializeField] private float minAutoOffsetMs = -200f;
    [SerializeField] private float maxAutoOffsetMs = -20f;

    [Tooltip("수동 오프셋(ms). autoOffsetFromRtt가 꺼져있을 때 사용. 음수면 미리 울림")]
    [SerializeField] private float manualSoundOffsetMs = -80f;

    private RhythmClient Rhythm => RhythmClient.Instance;

    // green 진입 감지용
    private bool _wasGreen = false;
    private long _lastBeepBeat = long.MinValue;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);

        if (_canvas == null)
            CreateCanvasAndUI();

        EnsureAudio();
    }

    void Update()
    {
        if (Rhythm == null)
            return;

        if (judgeWindowMs == 0) judgeWindowMs = Rhythm.judgeWindowMs;

        long beatIndex = Rhythm.GetCurrentBeatIndex();
        double progress = Rhythm.GetCurrentBeatProgress01();
        long serverNow = Rhythm.GetCurrentServerTimeMs();
        double beatMs = Rhythm.GetBeatDurationMs();

        float soundOffsetMs = GetSoundOffsetMs();

        // progress -> dist 계산 (가까운 비트 경계까지 거리)
        double distToBeatMs = Mathf.Min(
            (float)(progress * beatMs),
            (float)((1.0 - progress) * beatMs)
        );

        bool isGreen = distToBeatMs <= judgeWindowMs;

        // UI
        if (_beatText != null)
        {
            _beatText.text =
                $"Beat: {beatIndex}\n" +
                $"Progress: {progress:0.00}\n" +
                $"Server: {serverNow} ms\n" +
                $"BeatMs: {beatMs:0.0}\n" +
                $"RTT: {TimeSync.EstimatedRttMs:0} ms\n" +
                $"SoundOffset: {soundOffsetMs:0} ms\n" +
                $"Window: {(isGreen ? "GREEN" : "RED")}";
        }

        if (_progressBar != null)
        {
            _progressBar.fillAmount = (float)progress;
            _progressBar.color = isGreen ? Color.green : Color.red;
        }

        // ✅ 초록(판정 윈도우) "진입" 순간에만 소리 재생
        // - 같은 beatIndex에서 중복 방지
        // - beatIndex < 0(시작 전)이면 그냥 무시
        if (beatIndex >= 0 && !_wasGreen && isGreen)
        {
            if (_lastBeepBeat != beatIndex) // 같은 비트에서 중복 울림 방지
            {
                PlayBeepWithOffset(soundOffsetMs);
                _lastBeepBeat = beatIndex;
            }
        }

        _wasGreen = isGreen;
    }

    private void PlayBeepWithOffset(float soundOffsetMs)
    {
        if (_beatClip == null || _audioSource == null)
            return;

        // offset이 음수면 "미리 울리기"인데,
        // 이미 green 진입 시점은 '근처'라서 음수 오프셋을 그대로 적용하면 너무 앞서갈 수 있음.
        // 그래서 여기서는 보수적으로:
        // - 음수는 0으로 clamp (즉시 울림)
        // - 양수는 delay로 반영
        float delaySec = Mathf.Max(0f, soundOffsetMs / 1000f);

        if (delaySec <= 0f)
        {
            _audioSource.PlayOneShot(_beatClip, _beatVolume);
        }
        else
        {
            // 지연 재생
            StartCoroutine(PlayOneShotDelayed(delaySec));
        }
    }

    private System.Collections.IEnumerator PlayOneShotDelayed(float delaySec)
    {
        yield return new WaitForSeconds(delaySec);
        if (_audioSource != null && _beatClip != null)
            _audioSource.PlayOneShot(_beatClip, _beatVolume);
    }

    private float GetSoundOffsetMs()
    {
        if (!autoOffsetFromRtt)
            return manualSoundOffsetMs;

        double oneWayMs = TimeSync.EstimatedRttMs * 0.5;
        double raw = -(oneWayMs + baseLeadMs);
        raw = Mathf.Clamp((float)raw, minAutoOffsetMs, maxAutoOffsetMs);
        return (float)raw;
    }

    private void EnsureAudio()
    {
        if (_audioSource == null)
        {
            _audioSource = gameObject.GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
        }

        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f; // 2D
        _audioSource.loop = false;
        _audioSource.dopplerLevel = 0f;
    }

    private void CreateCanvasAndUI()
    {
        // (원본 그대로) ...
        var canvasGo = new GameObject("BeatDebugCanvas_TMP");
        canvasGo.layer = LayerMask.NameToLayer("UI");
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGo);

        var panelGo = new GameObject("BeatPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0, 0, 0, 0.5f);

        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(10, -10);
        panelRect.sizeDelta = new Vector2(320, 135);

        var textGo = new GameObject("BeatText_TMP");
        textGo.transform.SetParent(panelGo.transform, false);
        _beatText = textGo.AddComponent<TextMeshProUGUI>();
        _beatText.alignment = TextAlignmentOptions.TopLeft;
        _beatText.fontSize = 18f;
        _beatText.color = Color.white;

        var textRect = _beatText.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0.35f);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.offsetMin = new Vector2(6, 6);
        textRect.offsetMax = new Vector2(-6, -6);

        var barBgGo = new GameObject("BeatProgressBg");
        barBgGo.transform.SetParent(panelGo.transform, false);
        var barBgImg = barBgGo.AddComponent<Image>();
        barBgImg.color = new Color(1, 1, 1, 0.1f);

        var barBgRect = barBgGo.GetComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0, 0);
        barBgRect.anchorMax = new Vector2(1, 0.35f);
        barBgRect.offsetMin = new Vector2(6, 6);
        barBgRect.offsetMax = new Vector2(-6, -6);

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
