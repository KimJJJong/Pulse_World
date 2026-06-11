using System.Collections;
using System.Collections.Generic;
using GameServer.InGame.Director.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StageAreaProgressHud : MonoBehaviour
{
    private static StageAreaProgressHud _instance;

    private CanvasGroup _group;
    private RectTransform _root;
    private TextMeshProUGUI _text;
    private Coroutine _hideRoutine;

    public static void Show(StageAreaProgressData data)
    {
        data ??= new StageAreaProgressData();
        EnsureInstance().ShowInternal(data);
        StageAreaOutlineVisual.SetVisible(data);
    }

    private static StageAreaProgressHud EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        var go = new GameObject(nameof(StageAreaProgressHud));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<StageAreaProgressHud>();
        _instance.BuildUi();
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void BuildUi()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 520;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var textGo = new GameObject("AreaProgressText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(CanvasGroup), typeof(Shadow));
        textGo.transform.SetParent(transform, false);

        _root = textGo.GetComponent<RectTransform>();
        _root.anchorMin = new Vector2(0.5f, 0.5f);
        _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = new Vector2(0.5f, 0.5f);
        _root.anchoredPosition = new Vector2(0f, 86f);
        _root.sizeDelta = new Vector2(720f, 120f);

        _group = textGo.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var shadow = textGo.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(0f, -3f);

        _text = textGo.GetComponent<TextMeshProUGUI>();
        _text.alignment = TextAlignmentOptions.Center;
        _text.fontSize = 48f;
        _text.fontStyle = FontStyles.Bold;
        _text.enableWordWrapping = false;
        _text.overflowMode = TextOverflowModes.Ellipsis;
        _text.color = new Color(0.86f, 1f, 1f, 1f);
        _text.raycastTarget = false;

        _root.gameObject.SetActive(false);
    }

    private void ShowInternal(StageAreaProgressData data)
    {
        if (_root == null)
            BuildUi();

        string label = string.IsNullOrWhiteSpace(data.Label) ? "Area" : data.Label.Trim();
        _text.text = $"{label}  {data.CurrentCount}/{Mathf.Max(1, data.RequiredCount)}";

        _root.gameObject.SetActive(true);
        _group.alpha = 1f;

        if (_hideRoutine != null)
            StopCoroutine(_hideRoutine);

        int durationMs = data.DurationMs > 0 ? data.DurationMs : 1200;
        _hideRoutine = StartCoroutine(CoFadeOut(durationMs / 1000f));
    }

    private IEnumerator CoFadeOut(float holdSeconds)
    {
        yield return new WaitForSeconds(holdSeconds);

        const float fadeSeconds = 0.35f;
        float elapsed = 0f;
        while (elapsed < fadeSeconds)
        {
            elapsed += Time.deltaTime;
            _group.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeSeconds);
            yield return null;
        }

        _group.alpha = 0f;
        _root.gameObject.SetActive(false);
        _hideRoutine = null;
    }
}

internal sealed class StageAreaOutlineVisual : MonoBehaviour
{
    private const float OutlineHeight = 0.09f;
    private const float CoreWidth = 0.045f;
    private const float GlowWidth = 0.14f;

    private static readonly Dictionary<string, StageAreaOutlineVisual> ActiveOutlines = new();
    private static Material _lineMaterial;

    public static void SetVisible(StageAreaProgressData data)
    {
        if (data == null || data.Area == null)
            return;

        string key = BuildKey(data);
        if (!data.ShowAreaOutline)
        {
            Hide(key);
            return;
        }

        if (!ActiveOutlines.TryGetValue(key, out var outline) || outline == null)
        {
            var go = new GameObject($"StageAreaOutline_{SanitizeName(data.Label)}");
            outline = go.AddComponent<StageAreaOutlineVisual>();
            ActiveOutlines[key] = outline;
        }

        outline.Build(data.Area);
    }

    private static void Hide(string key)
    {
        if (!ActiveOutlines.TryGetValue(key, out var outline))
            return;

        ActiveOutlines.Remove(key);
        if (outline != null)
            Destroy(outline.gameObject);
    }

    private void Build(RectData area)
    {
        ClearChildren();

        List<Vector2Int> cells = BuildCells(area);
        if (cells.Count == 0)
            return;

        var cellSet = new HashSet<Vector2Int>(cells);
        foreach (var cell in cells)
        {
            if (!cellSet.Contains(new Vector2Int(cell.x - 1, cell.y)))
                AddEdge(cell.x, cell.y, cell.x, cell.y + 1);

            if (!cellSet.Contains(new Vector2Int(cell.x + 1, cell.y)))
                AddEdge(cell.x + 1, cell.y, cell.x + 1, cell.y + 1);

            if (!cellSet.Contains(new Vector2Int(cell.x, cell.y - 1)))
                AddEdge(cell.x, cell.y, cell.x + 1, cell.y);

            if (!cellSet.Contains(new Vector2Int(cell.x, cell.y + 1)))
                AddEdge(cell.x, cell.y + 1, cell.x + 1, cell.y + 1);
        }
    }

    private void AddEdge(float x0, float y0, float x1, float y1)
    {
        Vector3 start = ResolveWorldPoint(x0, y0);
        Vector3 end = ResolveWorldPoint(x1, y1);
        CreateLine("Glow", start, end, GlowWidth, new Color(0.18f, 0.95f, 1f, 0.20f));
        CreateLine("Core", start, end, CoreWidth, new Color(0.68f, 1f, 1f, 0.98f));
    }

    private void CreateLine(string name, Vector3 start, Vector3 end, float width, Color color)
    {
        var go = new GameObject(name, typeof(LineRenderer));
        go.transform.SetParent(transform, false);

        var line = go.GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetLineMaterial();
    }

    private static Vector3 ResolveWorldPoint(float gridX, float gridY)
    {
        float cellSize = BoardView.Instance != null ? BoardView.Instance.cellSize : 1f;
        float worldX = gridX * cellSize;
        float worldZ = gridY * cellSize;
        float y = OutlineHeight;

        if (Physics.Raycast(new Vector3(worldX, 50f, worldZ), Vector3.down, out var hit, 100f, ~0, QueryTriggerInteraction.Ignore))
            y = hit.point.y + OutlineHeight;

        return new Vector3(worldX, y, worldZ);
    }

    private static Material GetLineMaterial()
    {
        if (_lineMaterial != null)
            return _lineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        _lineMaterial = shader != null ? new Material(shader) : null;
        if (_lineMaterial != null)
            _lineMaterial.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        return _lineMaterial;
    }

    private static List<Vector2Int> BuildCells(RectData area)
    {
        var cells = new List<Vector2Int>();
        if (area == null)
            return cells;

        var seen = new HashSet<Vector2Int>();
        if (area.Cells != null && area.Cells.Count > 0)
        {
            foreach (var cell in area.Cells)
            {
                if (cell == null)
                    continue;

                var point = new Vector2Int(cell.X, cell.Y);
                if (seen.Add(point))
                    cells.Add(point);
            }
            return cells;
        }

        int width = Mathf.Max(1, area.W);
        int height = Mathf.Max(1, area.H);
        for (int y = area.Y; y < area.Y + height; y++)
        {
            for (int x = area.X; x < area.X + width; x++)
            {
                var point = new Vector2Int(x, y);
                if (seen.Add(point))
                    cells.Add(point);
            }
        }

        return cells;
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    private static string BuildKey(StageAreaProgressData data)
    {
        RectData area = data.Area ?? new RectData();
        return $"{data.Label}|{area.X},{area.Y},{area.W},{area.H}|{EncodeCells(area)}";
    }

    private static string EncodeCells(RectData area)
    {
        if (area?.Cells == null || area.Cells.Count == 0)
            return string.Empty;

        var parts = new List<string>(area.Cells.Count);
        foreach (var cell in area.Cells)
        {
            if (cell != null)
                parts.Add($"{cell.X}:{cell.Y}");
        }

        parts.Sort(System.StringComparer.Ordinal);
        return string.Join(";", parts);
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Area";

        return value.Trim().Replace(' ', '_');
    }
}
