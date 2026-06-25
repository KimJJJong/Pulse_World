using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MonsterDamagePopupView : MonoBehaviour
{
    private const string ObjectName = "__MonsterDamagePopup";
    private const float Lifetime = 0.82f;
    private const float WorldScale = 0.011f;

    private readonly List<SparkFx> _sparks = new();

    private RectTransform _rect;
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private TextMeshProUGUI _text;
    private Image _flashImage;
    private Vector3 _startPosition;
    private Vector3 _travelOffset;
    private float _startTime;
    private Camera _cachedCamera;

    public static void Play(Transform target, int damage)
    {
        if (target == null || damage <= 0)
            return;

        GameObject go = new GameObject(ObjectName, typeof(RectTransform));
        MonsterDamagePopupView popup = go.AddComponent<MonsterDamagePopupView>();
        popup.Initialize(target, damage);
    }

    private void Initialize(Transform target, int damage)
    {
        _rect = GetComponent<RectTransform>();
        _rect.sizeDelta = new Vector2(132f, 54f);
        _rect.localScale = Vector3.one * WorldScale;

        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 70;
        _canvas.worldCamera = ResolveCamera();

        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        _startPosition = ResolveAnchorPosition(target);
        Camera camera = ResolveCamera();
        Vector3 side = camera != null ? camera.transform.right : Vector3.right;
        _travelOffset = Vector3.up * 0.62f + side * Random.Range(-0.16f, 0.16f);
        transform.position = _startPosition;

        BuildFlash();
        BuildText(damage);
        BuildSparks();

        _startTime = Time.unscaledTime;
        FaceCamera();
    }

    private void LateUpdate()
    {
        float progress = Mathf.Clamp01((Time.unscaledTime - _startTime) / Lifetime);
        if (progress >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        FaceCamera();

        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        transform.position = _startPosition + _travelOffset * eased;

        float pop = progress < 0.16f
            ? Mathf.Lerp(0.75f, 1.18f, progress / 0.16f)
            : Mathf.Lerp(1.18f, 0.92f, (progress - 0.16f) / 0.84f);
        _rect.localScale = Vector3.one * (WorldScale * pop);

        _canvasGroup.alpha = 1f - Mathf.SmoothStep(0.68f, 1f, progress);

        if (_flashImage != null)
        {
            Color color = _flashImage.color;
            color.a = 0.42f * Mathf.Pow(1f - progress, 2f);
            _flashImage.color = color;
        }

        for (int i = 0; i < _sparks.Count; i++)
        {
            SparkFx spark = _sparks[i];
            if (spark.Rect == null || spark.Image == null)
                continue;

            spark.Rect.anchoredPosition = spark.Start + spark.Delta * eased;
            spark.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.25f, progress);

            Color color = spark.Color;
            color.a *= 1f - progress;
            spark.Image.color = color;
        }
    }

    private void BuildFlash()
    {
        RectTransform rect = CreateImage("DamageFlash", transform, new Color(1f, 0.16f, 0.03f, 0.42f));
        Anchor(rect, Vector2.zero, new Vector2(78f, 26f));
        _flashImage = rect.GetComponent<Image>();
    }

    private void BuildText(int damage)
    {
        GameObject go = new GameObject("DamageText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(Shadow));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        Anchor(rect, Vector2.zero, new Vector2(132f, 48f));

        _text = go.GetComponent<TextMeshProUGUI>();
        _text.text = damage.ToString();
        _text.alignment = TextAlignmentOptions.Center;
        _text.enableWordWrapping = false;
        _text.fontSize = 31f;
        _text.fontStyle = FontStyles.Bold;
        _text.color = new Color(1f, 0.88f, 0.42f, 1f);
        _text.outlineWidth = 0.22f;
        _text.outlineColor = new Color(0.12f, 0.015f, 0f, 0.95f);
        _text.raycastTarget = false;

        Shadow shadow = go.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(1.6f, -1.8f);
    }

    private void BuildSparks()
    {
        AddSpark(new Vector2(-42f, 8f), new Vector2(-22f, 18f), 28f);
        AddSpark(new Vector2(42f, 8f), new Vector2(22f, 18f), -28f);
        AddSpark(new Vector2(-31f, -9f), new Vector2(-18f, -12f), -18f);
        AddSpark(new Vector2(31f, -9f), new Vector2(18f, -12f), 18f);
    }

    private void AddSpark(Vector2 start, Vector2 delta, float rotation)
    {
        RectTransform rect = CreateImage("DamageSpark", transform, new Color(1f, 0.58f, 0.08f, 0.82f));
        Anchor(rect, start, new Vector2(5f, 20f));
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);

        Image image = rect.GetComponent<Image>();
        _sparks.Add(new SparkFx
        {
            Rect = rect,
            Image = image,
            Start = start,
            Delta = delta,
            Color = image.color
        });
    }

    private void FaceCamera()
    {
        Camera camera = ResolveCamera();
        if (camera == null)
            return;

        transform.rotation = camera.transform.rotation;
        if (_canvas != null)
            _canvas.worldCamera = camera;
    }

    private Camera ResolveCamera()
    {
        if (_cachedCamera != null)
            return _cachedCamera;

        _cachedCamera = Camera.main;
        return _cachedCamera;
    }

    private static Vector3 ResolveAnchorPosition(Transform target)
    {
        if (TryGetRendererBounds(target, out Bounds bounds))
            return new Vector3(bounds.center.x, bounds.max.y + 0.46f, bounds.center.z);

        return target.position + Vector3.up * 2.2f;
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null
                || renderer is ParticleSystemRenderer
                || renderer is TrailRenderer
                || renderer is LineRenderer)
            {
                continue;
            }

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    private static RectTransform CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static void Anchor(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private struct SparkFx
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 Start;
        public Vector2 Delta;
        public Color Color;
    }
}
