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

    private RhythmClient Rhythm => RhythmClient.Instance;

    void Awake()
    {
        if (_canvas == null)
            CreateCanvasAndUI();
    }

    void Update()
    {
        if (Rhythm == null)
            return;

        long beatIndex = Rhythm.GetCurrentBeatIndex();
        double progress = Rhythm.GetCurrentBeatProgress01();
        long serverNow = Rhythm.GetCurrentServerTimeMs();
        double beatMs = Rhythm.GetBeatDurationMs();

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

            // Beat 한가운데 근처에 있으면 판정 OK 라고 보고 색 변경
            double distToBeatMs = Mathf.Min(
                (float)(progress * beatMs),
                (float)((1.0 - progress) * beatMs)
            );

            _progressBar.color = distToBeatMs <= judgeWindowMs ? Color.green : Color.red;
        }
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
