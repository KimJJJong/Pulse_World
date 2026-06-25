using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerHitSfxOverlay : MonoBehaviour
{
    private const string ObjectName = "__PlayerHitSfxOverlay";
    private const int SortingOrder = 31000;
    private const float VignetteDuration = 0.68f;
    private const float LethalVignetteDuration = 1.05f;
    private const float DamageTextLifetime = 0.84f;
    private const int TextureSize = 192;

    private static PlayerHitSfxOverlay _instance;
    private static Sprite _vignetteSprite;
    private static Sprite _whiteSprite;

    private RectTransform _overlayRect;
    private RectTransform _streakRoot;
    private RectTransform _popupRoot;
    private Image _damageWash;
    private Image _darkVignette;
    private Image _redVignette;
    private Coroutine _vignetteCoroutine;

    public static void Play(Transform target, int damage, int oldHp, int newHp)
    {
        if (damage <= 0)
            return;

        PlayerHitSfxOverlay overlay = EnsureInstance();
        if (overlay == null)
            return;

        overlay.PlayInternal(target, damage, oldHp, newHp);
    }

    private static PlayerHitSfxOverlay EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        PlayerHitSfxOverlay existing = FindFirstObjectByType<PlayerHitSfxOverlay>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            return _instance;
        }

        GameObject root = new GameObject(ObjectName, typeof(RectTransform));
        _instance = root.AddComponent<PlayerHitSfxOverlay>();
        DontDestroyOnLoad(root);
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
        EnsureRuntimeUi();
    }

    private void PlayInternal(Transform target, int damage, int oldHp, int newHp)
    {
        EnsureRuntimeUi();

        float severity = ResolveSeverity(damage, oldHp, newHp);
        bool lethal = newHp <= 0;

        if (_vignetteCoroutine != null)
            StopCoroutine(_vignetteCoroutine);

        _vignetteCoroutine = StartCoroutine(CoVignettePulse(severity, lethal));
        SpawnDamageText(target, damage, severity, lethal);
        SpawnEdgeStreaks(severity, lethal);
        TriggerCameraShake(severity, lethal);
    }

    private void EnsureRuntimeUi()
    {
        if (_overlayRect != null)
            return;

        _overlayRect = GetOrAdd<RectTransform>(gameObject);
        Stretch(_overlayRect);

        Canvas canvas = GetOrAdd<Canvas>(gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = GetOrAdd<CanvasScaler>(gameObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup group = GetOrAdd<CanvasGroup>(gameObject);
        group.blocksRaycasts = false;
        group.interactable = false;

        _damageWash = CreateImage("DamageWash", _overlayRect, VignetteSprite, new Color(0.86f, 0.035f, 0.02f, 0f));
        _darkVignette = CreateImage("DarkVignette", _overlayRect, VignetteSprite, new Color(0f, 0f, 0f, 0f));
        _redVignette = CreateImage("RedVignette", _overlayRect, VignetteSprite, new Color(0.86f, 0.028f, 0.01f, 0f));

        _streakRoot = CreateRect("EdgeStreaks", _overlayRect);
        Stretch(_streakRoot);

        _popupRoot = CreateRect("DamagePopups", _overlayRect);
        Stretch(_popupRoot);
    }

    private IEnumerator CoVignettePulse(float severity, bool lethal)
    {
        float duration = lethal ? LethalVignetteDuration : VignetteDuration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float attack = Mathf.Clamp01(t / 0.08f);
            float decay = Mathf.Pow(1f - t, lethal ? 1.35f : 1.85f);
            float pulse = t < 0.08f ? Mathf.Lerp(0.35f, 1f, attack) : decay;

            SetAlpha(_redVignette, Mathf.Clamp01(0.56f * severity * pulse));
            SetAlpha(_darkVignette, Mathf.Clamp01(0.44f * severity * pulse));

            float wash = t < 0.18f ? Mathf.Lerp(0.065f * severity, 0.006f, t / 0.18f) : 0f;
            SetAlpha(_damageWash, Mathf.Clamp01(wash));

            yield return null;
        }

        ClearOverlayImages();
        _vignetteCoroutine = null;
    }

    private void SpawnDamageText(Transform target, int damage, float severity, bool lethal)
    {
        Vector2 start = ResolvePopupPosition(target);
        start += new Vector2(Random.Range(-12f, 12f), 8f);

        GameObject go = new GameObject("PlayerDamageText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(CanvasGroup), typeof(Shadow));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(_popupRoot, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = start;
        rect.sizeDelta = new Vector2(154f, 58f);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = $"-{damage}";
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.fontSize = lethal ? 36f : Mathf.Lerp(29f, 34f, Mathf.Clamp01((severity - 0.72f) / 0.45f));
        text.fontStyle = FontStyles.Bold;
        text.color = lethal ? new Color(1f, 0.02f, 0.02f, 1f) : new Color(1f, 0.08f, 0.04f, 1f);
        text.outlineWidth = lethal ? 0.28f : 0.22f;
        text.outlineColor = new Color(0.08f, 0f, 0f, 0.98f);
        text.raycastTarget = false;

        Shadow shadow = go.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.76f);
        shadow.effectDistance = new Vector2(1.7f, -2.1f);

        SpawnTextSparks(start, severity, lethal);
        StartCoroutine(CoDamageText(rect, go.GetComponent<CanvasGroup>(), start, severity, lethal));
    }

    private IEnumerator CoDamageText(RectTransform rect, CanvasGroup group, Vector2 start, float severity, bool lethal)
    {
        float elapsed = 0f;
        Vector2 travel = new Vector2(Random.Range(-10f, 10f), lethal ? 58f : 46f) * Mathf.Lerp(0.9f, 1.12f, severity);

        while (elapsed < DamageTextLifetime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / DamageTextLifetime);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float pop = t < 0.16f
                ? Mathf.Lerp(0.74f, lethal ? 1.32f : 1.2f, t / 0.16f)
                : Mathf.Lerp(lethal ? 1.32f : 1.2f, 0.92f, (t - 0.16f) / 0.84f);

            if (rect != null)
            {
                rect.anchoredPosition = start + travel * eased;
                rect.localScale = Vector3.one * pop;
            }

            if (group != null)
                group.alpha = 1f - Mathf.SmoothStep(0.62f, 1f, t);

            yield return null;
        }

        if (rect != null)
            Destroy(rect.gameObject);
    }

    private void SpawnTextSparks(Vector2 center, float severity, bool lethal)
    {
        Color color = lethal
            ? new Color(1f, 0.1f, 0.02f, 0.78f)
            : new Color(1f, 0.23f, 0.08f, 0.72f);

        int count = lethal ? 7 : 5;
        for (int i = 0; i < count; i++)
        {
            float angle = -150f + i * (300f / Mathf.Max(1, count - 1)) + Random.Range(-12f, 12f);
            float distance = Random.Range(24f, 46f) * severity;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector2 start = center + direction * Random.Range(10f, 18f);
            Vector2 delta = direction * distance + Vector2.up * Random.Range(8f, 16f);
            Vector2 size = new Vector2(Random.Range(4f, 6f), Random.Range(18f, 28f));

            RectTransform spark = CreateImage("PlayerDamageSpark", _popupRoot, WhiteSprite, color).rectTransform;
            Anchor(spark, start, size);
            spark.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            StartCoroutine(CoFadingRect(spark, delta, 0.42f, 0.2f));
        }
    }

    private void SpawnEdgeStreaks(float severity, bool lethal)
    {
        Vector2 size = GetOverlaySize();
        Vector2 half = size * 0.5f;
        int count = lethal ? 9 : 6;

        for (int i = 0; i < count; i++)
        {
            int edge = i % 4;
            Vector2 position;
            float rotation;

            if (edge == 0)
            {
                position = new Vector2(-half.x + Random.Range(12f, 44f), Random.Range(-half.y * 0.88f, half.y * 0.88f));
                rotation = Random.Range(-28f, 26f);
            }
            else if (edge == 1)
            {
                position = new Vector2(half.x - Random.Range(12f, 44f), Random.Range(-half.y * 0.88f, half.y * 0.88f));
                rotation = Random.Range(154f, 208f);
            }
            else if (edge == 2)
            {
                position = new Vector2(Random.Range(-half.x * 0.88f, half.x * 0.88f), half.y - Random.Range(12f, 38f));
                rotation = Random.Range(64f, 116f);
            }
            else
            {
                position = new Vector2(Random.Range(-half.x * 0.88f, half.x * 0.88f), -half.y + Random.Range(12f, 38f));
                rotation = Random.Range(-116f, -64f);
            }

            Color color = i % 3 == 0
                ? new Color(0.92f, 0.05f, 0.02f, 0.42f)
                : new Color(0.45f, 0f, 0f, 0.37f);

            RectTransform streak = CreateImage("PlayerHitEdgeStreak", _streakRoot, WhiteSprite, color).rectTransform;
            float length = Random.Range(118f, 220f) * Mathf.Lerp(0.85f, 1.18f, severity);
            float thickness = Random.Range(4f, lethal ? 10f : 8f);
            Anchor(streak, position, new Vector2(length, thickness));
            streak.localRotation = Quaternion.Euler(0f, 0f, rotation);

            Vector2 inward = -position.normalized * Random.Range(10f, 26f);
            StartCoroutine(CoFadingRect(streak, inward, Random.Range(0.3f, 0.46f), 0.34f));
        }
    }

    private IEnumerator CoFadingRect(RectTransform rect, Vector2 delta, float lifetime, float shrinkTo)
    {
        Image image = rect != null ? rect.GetComponent<Image>() : null;
        Color baseColor = image != null ? image.color : Color.clear;
        Vector2 start = rect != null ? rect.anchoredPosition : Vector2.zero;
        Vector3 baseScale = rect != null ? rect.localScale : Vector3.one;
        float elapsed = 0f;

        while (elapsed < lifetime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);
            float eased = 1f - Mathf.Pow(1f - t, 2f);

            if (rect != null)
            {
                rect.anchoredPosition = start + delta * eased;
                rect.localScale = baseScale * Mathf.Lerp(1f, shrinkTo, t);
            }

            if (image != null)
            {
                Color color = baseColor;
                color.a = baseColor.a * (1f - Mathf.SmoothStep(0.22f, 1f, t));
                image.color = color;
            }

            yield return null;
        }

        if (rect != null)
            Destroy(rect.gameObject);
    }

    private Vector2 ResolvePopupPosition(Transform target)
    {
        Vector3 world = target != null ? ResolveAnchorPosition(target) : Vector3.zero;
        Camera camera = Camera.main;
        Vector2 screenPoint;

        if (camera != null && target != null)
        {
            Vector3 projected = camera.WorldToScreenPoint(world);
            if (projected.z > 0f)
                screenPoint = projected;
            else
                screenPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.56f);
        }
        else
        {
            screenPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.56f);
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlayRect, screenPoint, null, out Vector2 localPoint))
            return localPoint;

        return Vector2.zero;
    }

    private static Vector3 ResolveAnchorPosition(Transform target)
    {
        if (TryGetRendererBounds(target, out Bounds bounds))
            return new Vector3(bounds.center.x, bounds.max.y + 0.5f, bounds.center.z);

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

    private void TriggerCameraShake(float severity, bool lethal)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        CameraFollow follow = camera.GetComponent<CameraFollow>();
        if (follow == null)
            follow = camera.GetComponentInParent<CameraFollow>();

        if (follow == null)
            return;

        float duration = lethal ? 0.34f : 0.22f;
        float strength = (lethal ? 0.07f : 0.044f) * Mathf.Clamp(severity, 0.75f, 1.22f);
        follow.Shake(duration, strength, lethal ? 34f : 28f);
    }

    private static float ResolveSeverity(int damage, int oldHp, int newHp)
    {
        if (newHp <= 0)
            return 1.22f;

        float relativeDamage = oldHp > 0 ? damage / (float)oldHp : 0.15f;
        return Mathf.Clamp(0.72f + relativeDamage * 1.45f, 0.74f, 1.12f);
    }

    private Vector2 GetOverlaySize()
    {
        if (_overlayRect != null)
        {
            Vector2 size = _overlayRect.rect.size;
            if (size.x > 1f && size.y > 1f)
                return size;
        }

        return new Vector2(1280f, 720f);
    }

    private void ClearOverlayImages()
    {
        SetAlpha(_damageWash, 0f);
        SetAlpha(_darkVignette, 0f);
        SetAlpha(_redVignette, 0f);
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Stretch(rect);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void Anchor(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
            return;

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        if (!go.TryGetComponent<T>(out T component))
            component = go.AddComponent<T>();

        return component;
    }

    private static Sprite VignetteSprite
    {
        get
        {
            if (_vignetteSprite == null)
                _vignetteSprite = CreateVignetteSprite();

            return _vignetteSprite;
        }
    }

    private static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite == null)
            {
                _whiteSprite = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    1f);
                _whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            }

            return _whiteSprite;
        }
    }

    private static Sprite CreateVignetteSprite()
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "T_PlayerHitSfxVignette",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float center = (TextureSize - 1) * 0.5f;
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float nx = (x - center) / center;
                float ny = (y - center) / center;
                float radial = Mathf.InverseLerp(0.68f, 1.08f, Mathf.Sqrt(nx * nx + ny * ny));
                float edge = Mathf.Max(
                    Mathf.InverseLerp(0.72f, 0.99f, Mathf.Abs(nx)),
                    Mathf.InverseLerp(0.72f, 0.99f, Mathf.Abs(ny)));
                float noise = Mathf.PerlinNoise(x * 0.067f, y * 0.071f);
                float alpha = Mathf.Clamp01(Mathf.Pow(Mathf.Max(radial, edge), 2.25f) * Mathf.Lerp(0.78f, 1.1f, noise));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f),
            TextureSize);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
