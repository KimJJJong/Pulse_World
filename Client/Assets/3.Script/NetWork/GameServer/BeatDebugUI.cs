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
    // Window Band: Left(0.0) -> Right(within 0~1) and Right(1.0) -> Left
    private Image _windowBandLeft;
    private Image _windowBandRight;
    
    private Image _centerLineLeft;
    private Image _centerLineRight;

    private Sprite _defaultSprite;
    private bool _lastDebugOverlayVisible = true;

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

        ApplyDebugOverlayVisibility(force: true);
    }

    void Update()
    {
        P2PDebugViewConfig.PollRuntimeToggles();
        if (!ApplyDebugOverlayVisibility())
            return;

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
            //  BeatCenter(0.5) 가 아니라 이제 0.0/1.0 이지만,
            //  BgmSyncPlayer의 AlignToBeatCenter 로직이 서버와 클라의 '기준'을 어디로 잡느냐에 따라 다를 수 있음.
            //  일단 단순 디버그 텍스트는 유지.
            
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
                $"Enabled: {_autoCalib.Enabled} ({AudioOffsetAutoCalibrator.ToggleHotkeyName} toggle)\n" +
                $"Samples: {_autoCalib.SampleCount}\n" +
                $"LastMedianDiff: {_autoCalib.LastMedianDiffMs}ms\n" +
                $"LastAppliedDelta: {_autoCalib.LastAppliedDeltaMs}ms\n" +
                $"Apply/Clear: {AudioOffsetAutoCalibrator.ApplyNowHotkeyName} / {AudioOffsetAutoCalibrator.ClearHotkeyName}\n";
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
                $"Window: ±{judgeWindowMs:0}ms (Target: Beat Edge)\n" +
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

    private bool ApplyDebugOverlayVisibility(bool force = false)
    {
        bool visible = P2PDebugViewConfig.ShowNetworkSyncOverlay;
        if (_canvas != null && (force || _lastDebugOverlayVisible != visible || _canvas.enabled != visible))
            _canvas.enabled = visible;

        _lastDebugOverlayVisible = visible;
        return visible;
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

        // Nearest Beat Logic:
        // 0.0 ~ 0.5 -> Target 0.0 (Current Beat) -> Late (+)
        // 0.5 ~ 1.0 -> Target 1.0 (Next Beat) -> Early (-)
        
        float signedMs;
        if (p < 0.5f)
        {
             // 0.1 -> 0.1 * beatMs (Late)
             signedMs = p * (float)beatMs;
        }
        else
        {
             // 0.9 -> (0.9 - 1.0) * beatMs = -0.1 * beatMs (Early)
             signedMs = (p - 1.0f) * (float)beatMs;
        }

        int diffMs = Mathf.RoundToInt(signedMs);
        float distMs = Mathf.Abs(signedMs);
        bool inWindow = distMs <= judgeWindowMs;

        // window 밖 초과분 정규화
        float outMs = Mathf.Max(0f, distMs - judgeWindowMs);

        // 시각적 정규화 기준 (0.5비트 거리까지)
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

        // Visual Width
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
        // 0.0 = Left, 1.0 = Right
        // AnchorMin/Max가 (0,0) (0,1) 이라 가정하면:
        // Actually typical UI anchor:
        // If Anchor (0,0) (1,1) -> offsetMin/Max
        // If Anchor (0,0) -> anchoredPosition gives offset from Bottom-Left.
        // Let's assume Anchors are set properly in CreateCanvasAndUI?
        // Wait, _barBg Layout:
        // AnchorMin: 0,0
        // AnchorMax: 1, 0.15f
        
        // Markers use:
        // AnchorMin 0.5, 0
        // AnchorMax 0.5, 1
        // So x=0 is center.
        
        // Refactoring: Marker Parent is _barBg.
        // Let's change Anchor to (0,0)-(0,1) Left Aligned so x=progress*width works easily?
        // Or keep Centered Anchor and calculate offset from center?
        // (progress - 0.5) * width is correct for Centered Anchor.
        
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
        if (_barBg == null) return;
        
        if (_windowBandLeft == null && _windowBandRight == null) return;

        // judgeWindowMs / beatMs
        float ratio = (float)(judgeWindowMs / beatMs); // One-side ratio
        ratio = Mathf.Clamp01(ratio);

        float barWidthPx = _barBg.rectTransform.rect.width;
        float bandWidthPx = ratio * barWidthPx; // width for One Side

        // Left Band: covers [0, ratio]
        // Anchor is Center (0.5). So Left Edge is -0.5*W.
        // We want it at Left Edge. 
         if (_windowBandLeft != null)
        {
            var rt = _windowBandLeft.rectTransform;
            rt.sizeDelta = new Vector2(bandWidthPx, 0f);
            
            // Position: Left Edge + halfWidth
            // Left Edge of Bar relative to Center is -barWidth/2.
            // Pos = -barWidth/2 + bandWidth/2
            float x = (-barWidthPx * 0.5f) + (bandWidthPx * 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
        }

        // Right Band: covers [1-ratio, 1]
         if (_windowBandRight != null)
        {
            var rt = _windowBandRight.rectTransform;
            rt.sizeDelta = new Vector2(bandWidthPx, 0f);
            
            // Position: Right Edge - halfWidth
            // Right Edge = +barWidth/2
            float x = (barWidthPx * 0.5f) - (bandWidthPx * 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
        }
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
        //DontDestroyOnLoad(canvasGo);

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

        // window band LEFT
        var winGoL = new GameObject("JudgeWindowBandL");
        winGoL.transform.SetParent(barBgGo.transform, false);
        _windowBandLeft = winGoL.AddComponent<Image>();
        ApplySprite(_windowBandLeft, sliced: true);
        _windowBandLeft.color = new Color(0f, 1f, 0f, 0.30f);
        var winRectL = _windowBandLeft.rectTransform;
        winRectL.anchorMin = new Vector2(0.5f, 0f);
        winRectL.anchorMax = new Vector2(0.5f, 1f);
        winRectL.pivot = new Vector2(0.5f, 0.5f); 
        winRectL.anchoredPosition = Vector2.zero;

        // window band RIGHT
        var winGoR = new GameObject("JudgeWindowBandR");
        winGoR.transform.SetParent(barBgGo.transform, false);
        _windowBandRight = winGoR.AddComponent<Image>();
        ApplySprite(_windowBandRight, sliced: true);
        _windowBandRight.color = new Color(0f, 1f, 0f, 0.30f);
        var winRectR = _windowBandRight.rectTransform;
        winRectR.anchorMin = new Vector2(0.5f, 0f);
        winRectR.anchorMax = new Vector2(0.5f, 1f);
        winRectR.pivot = new Vector2(0.5f, 0.5f);
        winRectR.anchoredPosition = Vector2.zero;

        // center line LEFT
        var centerGoL = new GameObject("CenterLineL");
        centerGoL.transform.SetParent(barBgGo.transform, false);
        _centerLineLeft = centerGoL.AddComponent<Image>();
        ApplySprite(_centerLineLeft, sliced: true);
        _centerLineLeft.color = new Color(1f, 1f, 1f, 0.85f);
        var cLRect = _centerLineLeft.rectTransform;
        cLRect.anchorMin = new Vector2(0f, 0f); // Left Edge
        cLRect.anchorMax = new Vector2(0f, 1f);
        cLRect.pivot = new Vector2(0.5f, 0.5f);
        cLRect.anchoredPosition = Vector2.zero;
        cLRect.sizeDelta = new Vector2(2f, 0f);

        // center line RIGHT
        var centerGoR = new GameObject("CenterLineR");
        centerGoR.transform.SetParent(barBgGo.transform, false);
        _centerLineRight = centerGoR.AddComponent<Image>();
        ApplySprite(_centerLineRight, sliced: true);
        _centerLineRight.color = new Color(1f, 1f, 1f, 0.85f);
        var cRRect = _centerLineRight.rectTransform;
        cRRect.anchorMin = new Vector2(1f, 0f); // Right Edge
        cRRect.anchorMax = new Vector2(1f, 1f);
        cRRect.pivot = new Vector2(0.5f, 0.5f);
        cRRect.anchoredPosition = Vector2.zero;
        cRRect.sizeDelta = new Vector2(2f, 0f);
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
