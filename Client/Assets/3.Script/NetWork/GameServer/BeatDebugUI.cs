using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BeatDebugUI_TMP : MonoBehaviour
{
    public static BeatDebugUI_TMP Instance { get; private set; }

    [Header("Optional: 수동 할당 안 하면 자동 생성")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private TextMeshProUGUI _beatText;

    [Header("Judge")]
    [Tooltip("0이면 RhythmClient.judgeWindowMs 사용")]
    [SerializeField] private float judgeWindowMs = 0f;

    [Header("Hit Markers")]
    [SerializeField] private int maxHitMarkers = 5;
    [SerializeField] private float hitMarkerLifeSec = 1.2f;
    [SerializeField] private float hitMarkerBaseWidthPx = 3f;
    [SerializeField] private float maxExtraWidthPx = 6f;

    [Header("Hit Label")]
    [SerializeField] private float labelYOffsetPx = 10f;
    [SerializeField] private int labelFontSize = 16;
    [SerializeField] private bool normalizeByHalfBeat = true;

    [Header("BGM Debug (optional)")]
    [Tooltip("비워두면 자동 탐색")]
    [SerializeField] private BgmSyncPlayer _bgm;

    [Header("AutoCalib Debug (optional)")]
    [Tooltip("비워두면 자동 탐색")]
    [SerializeField] private AudioOffsetAutoCalibrator _autoCalib;

    [Header("Server Diff Debug (optional)")]
    [Tooltip("SC_CalibResult/SC_ActionResult로 받은 diff를 여기에 기록해 보여줌")]
    [SerializeField] private bool showLastServerDiff = true;

    // UI parts
    private Image _barBg;
    private Image _progressFill;
    private Image _windowBand;
    private Image _centerLine;

    private Sprite _defaultSprite;

    private RhythmClient Rhythm => RhythmClient.Instance;

    private readonly List<HitMarker> _markers = new();

    // 서버에서 마지막으로 받은 diff(early -, late +)
    public int LastServerDiffMs { get; private set; }
    public long LastServerDiffBeat { get; private set; }
    public long LastServerDiffRecvServerNowMs { get; private set; }

    private sealed class HitMarker
    {
        public Image Img;
        public TextMeshProUGUI Label;
        public float Life;
        public float Progress; // 0~1
        public float WidthPx;
        public Color BaseColor;
        public int DiffMs; // 표시용(로컬 progress 기반)
    }

    void Awake()
    {
        Instance = this;


        _defaultSprite = CreateWhiteSprite();

        if (_canvas == null)
            CreateCanvasAndUI();
    }

    void Update()
    {
        if (Rhythm == null) return;

        if (_bgm == null)
            _bgm = FindFirstObjectByType<BgmSyncPlayer>();

        if (_autoCalib == null)
            _autoCalib = AudioOffsetAutoCalibrator.Instance ?? FindFirstObjectByType<AudioOffsetAutoCalibrator>();

        if (judgeWindowMs <= 0f)
            judgeWindowMs = Rhythm.judgeWindowMs;

        long beatIndex = Rhythm.GetCurrentBeatIndex();
        double progress = Rhythm.GetCurrentBeatProgress01();
        long serverNow = Rhythm.GetCurrentServerTimeMs();
        double beatMs = Rhythm.GetBeatDurationMs();

        // ---- BGM debug (있을 때만) ----
        string bgmText = "";
        if (_bgm != null)
        {
            //  새 구조: DeviceOffset + AutoAlignOffset (+ optional AlignCenter)
            double centerMs = _bgm.AlignToBeatCenter ? (beatMs * 0.5) : 0.0;
            double audioOffsetMs = centerMs + _bgm.DeviceOffsetMs + _bgm.AutoAlignOffsetMs;

            double expectedSec = (serverNow - Rhythm.ServerSongStartMs + audioOffsetMs) / 1000.0;

            float actualSec = 0f;
            var src = _bgm.GetComponent<AudioSource>(); // 같은 오브젝트에 AudioSource가 붙어있는 구성이 가장 안전
            if (src != null && src.clip != null)
                actualSec = src.time;

            double driftSec = actualSec - expectedSec;
            int driftMs = Mathf.RoundToInt((float)(driftSec * 1000.0));

            bgmText =
                $"\n--- BGM ---\n" +
                $"State: {_bgm.State}\n" +
                $"AlignCenter: {_bgm.AlignToBeatCenter}\n" +
                $"DeviceOffsetMs: {_bgm.DeviceOffsetMs}\n" +
                $"AutoAlignOffsetMs: {_bgm.AutoAlignOffsetMs}\n" +
                $"TotalAudioOffsetMs: {_bgm.TotalAudioOffsetMs}\n" +
                $"Expected: {expectedSec:0.000}s  Actual: {actualSec:0.000}s\n" +
                $"Drift: {driftMs}ms";
        }

        // ---- AutoCalib debug (있을 때만) ----
        string calibText = "";
        if (_autoCalib != null)
        {
            // 버전 차이를 고려해 try-catch 없이 "있는 것만" 찍고 싶으면 필드명 통일 권장.
            // 여기서는 최신(16개 모아서 median -> delta -> OFF) 기준
            calibText =
                $"\n--- AutoCalib ---\n" +
                $"Enabled: {_autoCalib.Enabled} (F8 toggle)\n" +
                $"Samples: {_autoCalib.SampleCount}\n" +
                $"LastMedianDiff: {_autoCalib.LastMedianDiffMs}ms\n" +
                $"LastAppliedDelta: {_autoCalib.LastAppliedDeltaMs}ms\n";
        }

        // ---- Server diff debug ----
        string serverDiffText = "";
        if (showLastServerDiff)
        {
            serverDiffText =
                $"\n--- ServerDiff ---\n" +
                $"LastDiffMs: {LastServerDiffMs}ms\n" +
                $"LastDiffBeat: {LastServerDiffBeat}\n" +
                $"LastRecvServerNow: {LastServerDiffRecvServerNowMs}ms";
        }

        if (_beatText != null)
        {
            _beatText.text =
                $"Beat: {beatIndex}\n" +
                $"Progress: {progress:0.000}\n" +
                $"ServerNow: {serverNow} ms\n" +
                $"BeatMs: {beatMs:0.0}\n" +
                $"Window: ±{judgeWindowMs:0}ms (center=0.5)\n" +
                $"HitMarkers: {_markers.Count}/{maxHitMarkers}" +
                bgmText +
                calibText +
                serverDiffText;
        }

        if (_progressFill != null)
            _progressFill.fillAmount = (float)progress;

        UpdateWindowBand(beatMs);
        UpdateHitMarkers();
    }

    /// <summary>
    /// 서버에서 받은 diff를 디버그 UI에 찍기 위해 저장
    /// (SC_CalibResult / SC_ActionResult 등 수신 핸들러에서 호출)
    /// </summary>
    public void RecordServerDiff(int diffMs, long beatIndex, long recvServerNowMs)
    {
        LastServerDiffMs = diffMs;
        LastServerDiffBeat = beatIndex;
        LastServerDiffRecvServerNowMs = recvServerNowMs;
    }

    /// <summary>
    /// 입력 보낸 순간 호출:
    /// - 현재 progress 위치에 마커 + diffMs 라벨 추가(로컬 progress 기반)
    /// </summary>
    public void MarkHitNow()
    {
        if (Rhythm == null || _barBg == null) return;

        double beatMs = Rhythm.GetBeatDurationMs();
        float p = (float)Rhythm.GetCurrentBeatProgress01(); // 0~1

        // center=0.5 기준 diffMs(부호 포함)
        float signedMs = (p - 0.5f) * (float)beatMs;
        int diffMs = Mathf.RoundToInt(signedMs);

        float distMs = Mathf.Abs(signedMs);
        bool inWindow = distMs <= judgeWindowMs;

        // window 밖 초과분 정규화
        float outMs = Mathf.Max(0f, distMs - judgeWindowMs);

        float denomMs;
        if (normalizeByHalfBeat)
            denomMs = Mathf.Max(1f, (float)beatMs * 0.5f - judgeWindowMs);
        else
            denomMs = Mathf.Max(1f, judgeWindowMs);

        float t = Mathf.Clamp01(outMs / denomMs);

        // 색: IN=초록, OUT=노랑->주황->빨강
        Color color;
        if (inWindow)
        {
            color = new Color(0f, 1f, 0f, 0.95f);
        }
        else
        {
            if (t < 0.5f)
            {
                float u = t / 0.5f;
                color = Color.Lerp(new Color(1f, 1f, 0f, 0.95f), new Color(1f, 0.5f, 0f, 0.95f), u);
            }
            else
            {
                float u = (t - 0.5f) / 0.5f;
                color = Color.Lerp(new Color(1f, 0.5f, 0f, 0.95f), new Color(1f, 0f, 0f, 0.95f), u);
            }
        }

        float width = hitMarkerBaseWidthPx + (inWindow ? 0f : (t * maxExtraWidthPx));

        var marker = GetOrCreateMarker();
        marker.Progress = p;
        marker.Life = hitMarkerLifeSec;
        marker.WidthPx = width;
        marker.BaseColor = color;
        marker.DiffMs = diffMs;

        marker.Img.enabled = true;
        marker.Img.color = color;

        var rt = marker.Img.rectTransform;
        rt.sizeDelta = new Vector2(width, 0f);

        SetMarkerX(rt, p);
        marker.Img.transform.SetAsLastSibling();

        if (marker.Label != null)
        {
            marker.Label.enabled = true;
            marker.Label.text = (diffMs >= 0) ? $"+{diffMs}ms" : $"{diffMs}ms";
            marker.Label.color = new Color(1f, 1f, 1f, 0.95f);
            SetLabelPos(marker.Label.rectTransform, rt.anchoredPosition.x);
            marker.Label.transform.SetAsLastSibling();
        }
    }

    // ---------------- Hit Markers ----------------

    private HitMarker GetOrCreateMarker()
    {
        if (_markers.Count < maxHitMarkers)
        {
            var (img, label) = CreateHitMarkerWithLabel(_barBg.transform);
            var m = new HitMarker { Img = img, Label = label };
            _markers.Add(m);
            return m;
        }

        int best = 0;
        float bestLife = float.MaxValue;
        for (int i = 0; i < _markers.Count; i++)
        {
            if (_markers[i].Life < bestLife)
            {
                bestLife = _markers[i].Life;
                best = i;
            }
        }
        return _markers[best];
    }

    private void UpdateHitMarkers()
    {
        if (_markers.Count == 0) return;

        for (int i = 0; i < _markers.Count; i++)
        {
            var m = _markers[i];
            if (m.Life <= 0f)
            {
                if (m.Img != null) m.Img.enabled = false;
                if (m.Label != null) m.Label.enabled = false;
                continue;
            }

            m.Life -= Time.deltaTime;
            if (m.Life <= 0f)
            {
                if (m.Img != null) m.Img.enabled = false;
                if (m.Label != null) m.Label.enabled = false;
                continue;
            }

            float a = Mathf.Clamp01(m.Life / hitMarkerLifeSec);

            var mc = m.BaseColor;
            mc.a = Mathf.Lerp(0.05f, 0.95f, a);
            m.Img.color = mc;

            if (m.Label != null)
            {
                var lc = m.Label.color;
                lc.a = Mathf.Lerp(0.05f, 0.95f, a);
                m.Label.color = lc;
            }

            SetMarkerX(m.Img.rectTransform, m.Progress);
            if (m.Label != null)
                SetLabelPos(m.Label.rectTransform, m.Img.rectTransform.anchoredPosition.x);
        }
    }

    private void SetMarkerX(RectTransform rt, float progress01)
    {
        if (_barBg == null) return;
        float barWidthPx = _barBg.rectTransform.rect.width;
        float x = (progress01 - 0.5f) * barWidthPx;
        rt.anchoredPosition = new Vector2(x, 0f);
    }

    private void SetLabelPos(RectTransform rt, float markerX)
    {
        rt.anchoredPosition = new Vector2(markerX, labelYOffsetPx);
    }

    // ---------------- Window Band ----------------

    private void UpdateWindowBand(double beatMs)
    {
        if (_windowBand == null || _barBg == null) return;

        float halfRatio = (float)(judgeWindowMs / beatMs);
        halfRatio = Mathf.Clamp01(halfRatio);

        float barWidthPx = _barBg.rectTransform.rect.width;
        float bandWidthPx = (halfRatio * 2f) * barWidthPx;

        var rt = _windowBand.rectTransform;
        rt.sizeDelta = new Vector2(bandWidthPx, 0f);
        rt.anchoredPosition = Vector2.zero;
    }

    // ---------------- UI Creation ----------------

    private void CreateCanvasAndUI()
    {
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
        ApplySprite(panelImg, sliced: true);
        panelImg.color = new Color(0, 0, 0, 0.55f);

        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(10, -10);
        panelRect.sizeDelta = new Vector2(520, 650);

        var textGo = new GameObject("BeatText_TMP");
        textGo.transform.SetParent(panelGo.transform, false);
        _beatText = textGo.AddComponent<TextMeshProUGUI>();
        _beatText.alignment = TextAlignmentOptions.TopLeft;
        _beatText.fontSize = 18f;
        _beatText.color = Color.white;

        var textRect = _beatText.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0.25f);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);

        var barBgGo = new GameObject("BeatBarBG");
        barBgGo.transform.SetParent(panelGo.transform, false);
        _barBg = barBgGo.AddComponent<Image>();
        ApplySprite(_barBg, sliced: true);
        _barBg.color = new Color(1, 1, 1, 0.12f);

        var barBgRect = _barBg.rectTransform;
        barBgRect.anchorMin = new Vector2(0, 0);
        barBgRect.anchorMax = new Vector2(1, 0.15f);
        barBgRect.offsetMin = new Vector2(10, 10);
        barBgRect.offsetMax = new Vector2(-10, -10);

        // progress fill
        var fillGo = new GameObject("ProgressFill");
        fillGo.transform.SetParent(barBgGo.transform, false);
        _progressFill = fillGo.AddComponent<Image>();
        ApplySprite(_progressFill, sliced: false);
        _progressFill.type = Image.Type.Filled;
        _progressFill.fillMethod = Image.FillMethod.Horizontal;
        _progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _progressFill.color = new Color(0.2f, 0.8f, 1f, 0.85f);

        var fillRect = _progressFill.rectTransform;
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(1, 1);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        // window band
        var winGo = new GameObject("JudgeWindowBand");
        winGo.transform.SetParent(barBgGo.transform, false);
        _windowBand = winGo.AddComponent<Image>();
        ApplySprite(_windowBand, sliced: true);
        _windowBand.color = new Color(0f, 1f, 0f, 0.30f);

        var winRect = _windowBand.rectTransform;
        winRect.anchorMin = new Vector2(0.5f, 0f);
        winRect.anchorMax = new Vector2(0.5f, 1f);
        winRect.pivot = new Vector2(0.5f, 0.5f);
        winRect.anchoredPosition = Vector2.zero;
        winRect.sizeDelta = new Vector2(10f, 0f);

        // center line
        var centerGo = new GameObject("CenterLine");
        centerGo.transform.SetParent(barBgGo.transform, false);
        _centerLine = centerGo.AddComponent<Image>();
        ApplySprite(_centerLine, sliced: true);
        _centerLine.color = new Color(1f, 1f, 1f, 0.85f);

        var centerRect = _centerLine.rectTransform;
        centerRect.anchorMin = new Vector2(0.5f, 0f);
        centerRect.anchorMax = new Vector2(0.5f, 1f);
        centerRect.pivot = new Vector2(0.5f, 0.5f);
        centerRect.anchoredPosition = Vector2.zero;
        centerRect.sizeDelta = new Vector2(2f, 0f);
    }

    private (Image img, TextMeshProUGUI label) CreateHitMarkerWithLabel(Transform parent)
    {
        var root = new GameObject("HitMarkerRoot");
        root.transform.SetParent(parent, false);

        var markerGo = new GameObject("HitMarker");
        markerGo.transform.SetParent(root.transform, false);

        var img = markerGo.AddComponent<Image>();
        ApplySprite(img, sliced: true);
        img.raycastTarget = false;
        img.enabled = false;

        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(hitMarkerBaseWidthPx, 0f);

        var labelGo = new GameObject("HitLabel_TMP");
        labelGo.transform.SetParent(root.transform, false);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.enabled = false;
        label.fontSize = labelFontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 1f, 1f, 0.95f);
        label.raycastTarget = false;

        var lrt = label.rectTransform;
        lrt.anchorMin = new Vector2(0.5f, 1f);
        lrt.anchorMax = new Vector2(0.5f, 1f);
        lrt.pivot = new Vector2(0.5f, 0f);
        lrt.anchoredPosition = new Vector2(0f, labelYOffsetPx);
        lrt.sizeDelta = new Vector2(120f, 24f);

        return (img, label);
    }

    private void ApplySprite(Image img, bool sliced)
    {
        img.sprite = _defaultSprite;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = false;
    }

    private Sprite CreateWhiteSprite()
    {
        var tex = Texture2D.whiteTexture;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }
}
