using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

internal enum MonsterAttackImpactStyle
{
    PhysicalSlash,
    EarthSlam,
    ArcaneBurst,
    ShadowRend,
    FireBreath,
    CrystalBreak,
    VenomBloom,
    WindCut,
    WaterSplash,
    BossInferno
}

internal readonly struct MonsterAttackImpactPalette
{
    public readonly Color Primary;
    public readonly Color Secondary;
    public readonly Color Accent;
    public readonly Color Smoke;
    public readonly float Glow;

    public MonsterAttackImpactPalette(Color primary, Color secondary, Color accent, Color smoke, float glow)
    {
        Primary = primary;
        Secondary = secondary;
        Accent = accent;
        Smoke = smoke;
        Glow = glow;
    }
}

public static class MonsterAttackImpactVfx
{
    internal const int MaxCellBursts = 18;
    private const string RootName = "__MonsterAttackImpactVfx";

    private static GameObject _root;
    private static Material _lineMaterial;
    private static Material _particleMaterial;
    private static Material _meshMaterial;
    private static MaterialPropertyBlock _propertyBlock;

    public static void Play(
        BoardView boardView,
        int actorId,
        string skillId,
        float casterRotation,
        IReadOnlyList<Vector2Int> cells,
        int damageAmount,
        int knockbackDistance,
        int stunDurationTicks)
    {
        if (boardView == null || cells == null || cells.Count == 0)
            return;

        if (ClientGameState.Instance == null
            || !ClientGameState.Instance.TryGetEntity(actorId, out var caster)
            || caster.EntityType != (int)EntityType.Monster)
        {
            return;
        }

        var uniqueCells = BuildUniqueCells(cells);
        if (uniqueCells.Count == 0)
            return;

        var root = EnsureRoot();
        var style = ResolveStyle(caster.AppearanceId, skillId);

        var instanceObject = new GameObject($"MonsterImpact_{style}_{caster.EntityId}_{skillId}");
        instanceObject.transform.SetParent(root.transform, false);
        var instance = instanceObject.AddComponent<MonsterAttackImpactVfxInstance>();
        instance.Initialize(
            boardView,
            caster,
            style,
            ResolvePalette(style),
            skillId,
            casterRotation,
            uniqueCells,
            damageAmount,
            knockbackDistance,
            stunDurationTicks);
    }

    private static List<Vector2Int> BuildUniqueCells(IReadOnlyList<Vector2Int> cells)
    {
        var result = new List<Vector2Int>(cells.Count);
        var seen = new HashSet<Vector2Int>();

        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (seen.Add(cell))
                result.Add(cell);
        }

        return result;
    }

    private static GameObject EnsureRoot()
    {
        if (_root != null)
            return _root;

        _root = GameObject.Find(RootName);
        if (_root == null)
        {
            _root = new GameObject(RootName);
            UnityEngine.Object.DontDestroyOnLoad(_root);
        }

        return _root;
    }

    private static MonsterAttackImpactStyle ResolveStyle(int appearanceId, string skillId)
    {
        string key = string.IsNullOrWhiteSpace(skillId) ? "" : skillId.ToLowerInvariant();

        if (key.Contains("fire") || key.Contains("breath"))
            return MonsterAttackImpactStyle.FireBreath;

        if (key.Contains("demonking") || appearanceId == 1034)
            return MonsterAttackImpactStyle.BossInferno;

        if (key.Contains("crystal") || key.Contains("blackknight") || key.Contains("shieldbash") || appearanceId == 1021)
            return MonsterAttackImpactStyle.CrystalBreak;

        if (key.Contains("evilmage") || key.Contains("naga") || appearanceId == 1012 || appearanceId == 1020 || appearanceId == 1038)
            return MonsterAttackImpactStyle.ArcaneBurst;

        if (key.Contains("dash3block") || key.Contains("specter") || appearanceId == 1027)
            return MonsterAttackImpactStyle.ShadowRend;

        if (appearanceId == 1013 || appearanceId == 1019 || appearanceId == 1023 || appearanceId == 1029 || appearanceId == 1032)
            return MonsterAttackImpactStyle.EarthSlam;

        if (appearanceId == 1014 || appearanceId == 1017 || appearanceId == 1018 || appearanceId == 1030 || appearanceId == 1036 || appearanceId == 1037)
            return MonsterAttackImpactStyle.VenomBloom;

        if (appearanceId == 1010 || appearanceId == 1024 || appearanceId == 1028 || appearanceId == 1030)
            return MonsterAttackImpactStyle.WindCut;

        if (appearanceId == 1035 || appearanceId == 1040)
            return MonsterAttackImpactStyle.WaterSplash;

        if (appearanceId == 1011 || appearanceId == 1039)
            return MonsterAttackImpactStyle.FireBreath;

        return MonsterAttackImpactStyle.PhysicalSlash;
    }

    private static MonsterAttackImpactPalette ResolvePalette(MonsterAttackImpactStyle style)
    {
        return style switch
        {
            MonsterAttackImpactStyle.EarthSlam => new MonsterAttackImpactPalette(
                new Color(0.78f, 0.58f, 0.32f, 0.95f),
                new Color(0.38f, 0.30f, 0.22f, 0.58f),
                new Color(0.92f, 0.82f, 0.55f, 0.88f),
                new Color(0.34f, 0.28f, 0.22f, 0.34f),
                1.15f),
            MonsterAttackImpactStyle.ArcaneBurst => new MonsterAttackImpactPalette(
                new Color(0.42f, 0.96f, 1f, 0.92f),
                new Color(0.28f, 0.15f, 0.72f, 0.68f),
                new Color(0.9f, 0.5f, 1f, 0.86f),
                new Color(0.14f, 0.08f, 0.24f, 0.3f),
                1.6f),
            MonsterAttackImpactStyle.ShadowRend => new MonsterAttackImpactPalette(
                new Color(0.58f, 0.25f, 0.92f, 0.9f),
                new Color(0.05f, 0.03f, 0.1f, 0.76f),
                new Color(0.12f, 0.88f, 0.72f, 0.82f),
                new Color(0.03f, 0.025f, 0.05f, 0.38f),
                1.35f),
            MonsterAttackImpactStyle.FireBreath => new MonsterAttackImpactPalette(
                new Color(1f, 0.36f, 0.06f, 0.96f),
                new Color(1f, 0.78f, 0.18f, 0.82f),
                new Color(1f, 0.1f, 0.02f, 0.88f),
                new Color(0.22f, 0.12f, 0.08f, 0.36f),
                1.85f),
            MonsterAttackImpactStyle.CrystalBreak => new MonsterAttackImpactPalette(
                new Color(0.28f, 0.96f, 1f, 0.9f),
                new Color(0.08f, 0.34f, 0.56f, 0.7f),
                new Color(0.84f, 1f, 1f, 0.9f),
                new Color(0.08f, 0.12f, 0.18f, 0.32f),
                1.55f),
            MonsterAttackImpactStyle.VenomBloom => new MonsterAttackImpactPalette(
                new Color(0.52f, 1f, 0.22f, 0.9f),
                new Color(0.12f, 0.44f, 0.18f, 0.64f),
                new Color(0.94f, 0.92f, 0.32f, 0.78f),
                new Color(0.09f, 0.18f, 0.08f, 0.34f),
                1.25f),
            MonsterAttackImpactStyle.WindCut => new MonsterAttackImpactPalette(
                new Color(0.74f, 0.98f, 1f, 0.82f),
                new Color(0.42f, 0.72f, 0.58f, 0.58f),
                new Color(1f, 1f, 0.86f, 0.75f),
                new Color(0.18f, 0.28f, 0.22f, 0.22f),
                1.1f),
            MonsterAttackImpactStyle.WaterSplash => new MonsterAttackImpactPalette(
                new Color(0.28f, 0.8f, 1f, 0.88f),
                new Color(0.05f, 0.28f, 0.5f, 0.62f),
                new Color(0.72f, 1f, 1f, 0.82f),
                new Color(0.08f, 0.18f, 0.24f, 0.28f),
                1.25f),
            MonsterAttackImpactStyle.BossInferno => new MonsterAttackImpactPalette(
                new Color(1f, 0.18f, 0.04f, 0.98f),
                new Color(0.34f, 0.04f, 0.02f, 0.78f),
                new Color(1f, 0.85f, 0.22f, 0.9f),
                new Color(0.08f, 0.04f, 0.035f, 0.44f),
                2.2f),
            _ => new MonsterAttackImpactPalette(
                new Color(1f, 0.64f, 0.26f, 0.92f),
                new Color(0.35f, 0.22f, 0.16f, 0.48f),
                new Color(0.82f, 1f, 0.68f, 0.7f),
                new Color(0.18f, 0.16f, 0.12f, 0.28f),
                1.2f),
        };
    }

    internal static Material GetLineMaterial()
    {
        if (_lineMaterial != null)
            return _lineMaterial;

        _lineMaterial = CreateTransparentMaterial(
            "__MonsterImpact_Line",
            "Sprites/Default",
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Hidden/Internal-Colored");
        return _lineMaterial;
    }

    internal static Material GetParticleMaterial()
    {
        if (_particleMaterial != null)
            return _particleMaterial;

        _particleMaterial = CreateTransparentMaterial(
            "__MonsterImpact_Particle",
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Sprites/Default",
            "Hidden/Internal-Colored");
        return _particleMaterial;
    }

    internal static Material GetMeshMaterial()
    {
        if (_meshMaterial != null)
            return _meshMaterial;

        Shader shader = FindShader(
            "Universal Render Pipeline/Unlit",
            "Unlit/Color",
            "Sprites/Default",
            "Hidden/Internal-Colored");

        _meshMaterial = new Material(shader)
        {
            name = "__MonsterImpact_Mesh"
        };
        _meshMaterial.enableInstancing = true;
        SetMaterialColor(_meshMaterial, Color.white);
        return _meshMaterial;
    }

    private static Material CreateTransparentMaterial(string name, params string[] shaderNames)
    {
        var material = new Material(FindShader(shaderNames))
        {
            name = name,
            renderQueue = (int)RenderQueue.Transparent
        };

        material.enableInstancing = true;
        SetMaterialColor(material, Color.white);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);

        return material;
    }

    private static Shader FindShader(params string[] shaderNames)
    {
        for (int i = 0; i < shaderNames.Length; i++)
        {
            var shader = Shader.Find(shaderNames[i]);
            if (shader != null)
                return shader;
        }

        return Shader.Find("Hidden/InternalErrorShader");
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", color);
    }

    internal static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    internal static void SetRendererColor(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;

        _propertyBlock ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor("_BaseColor", color);
        _propertyBlock.SetColor("_Color", color);
        _propertyBlock.SetColor("_TintColor", color);
        renderer.SetPropertyBlock(_propertyBlock);
    }

}

internal sealed class MonsterAttackImpactVfxInstance : MonoBehaviour
{
        private const int CircleSegments = 56;
        private const float BaseLifetime = 1.25f;

        private readonly List<LineFx> _lines = new();
        private readonly List<MeshFx> _meshes = new();
        private readonly List<LightFx> _lights = new();
        private readonly List<Vector3> _sampledWorldCells = new();

        private BoardView _boardView;
        private ClientEntityInfo _caster;
        private MonsterAttackImpactStyle _style;
        private MonsterAttackImpactPalette _palette;
        private string _skillId;
        private float _rotation;
        private float _cellSize;
        private int _damageAmount;
        private int _knockbackDistance;
        private int _stunDurationTicks;

        public void Initialize(
            BoardView boardView,
            ClientEntityInfo caster,
            MonsterAttackImpactStyle style,
            MonsterAttackImpactPalette palette,
            string skillId,
            float casterRotation,
            IReadOnlyList<Vector2Int> cells,
            int damageAmount,
            int knockbackDistance,
            int stunDurationTicks)
        {
            _boardView = boardView;
            _caster = caster;
            _style = style;
            _palette = palette;
            _skillId = skillId ?? "";
            _rotation = casterRotation;
            _cellSize = Mathf.Max(0.5f, boardView.cellSize);
            _damageAmount = damageAmount;
            _knockbackDistance = knockbackDistance;
            _stunDurationTicks = stunDurationTicks;

            BuildSampledWorldCells(cells);
            BuildEffect();
            StartCoroutine(CoLifetime());
        }

        private void BuildSampledWorldCells(IReadOnlyList<Vector2Int> cells)
        {
            _sampledWorldCells.Clear();
            int step = Mathf.Max(1, Mathf.CeilToInt(cells.Count / (float)MonsterAttackImpactVfx.MaxCellBursts));

            for (int i = 0; i < cells.Count; i += step)
            {
                Vector2Int cell = cells[i];
                Vector3 position = _boardView.GridToWorldPublic(cell.x, cell.y);
                position.y += 0.08f;
                _sampledWorldCells.Add(position);
            }

            if (_sampledWorldCells.Count == 0 && cells.Count > 0)
            {
                Vector2Int cell = cells[0];
                Vector3 position = _boardView.GridToWorldPublic(cell.x, cell.y);
                position.y += 0.08f;
                _sampledWorldCells.Add(position);
            }
        }

        private void BuildEffect()
        {
            if (_sampledWorldCells.Count == 0)
                return;

            Vector3 center = GetCenter(_sampledWorldCells);
            Vector3 forward = RotationToForward(_rotation);
            float areaScale = Mathf.Clamp01(_sampledWorldCells.Count / 10f);
            float impactScale = Mathf.Clamp(0.85f + _damageAmount / 50f + _knockbackDistance * 0.08f, 0.9f, 1.75f);

            switch (_style)
            {
                case MonsterAttackImpactStyle.FireBreath:
                    BuildFire(center, forward, impactScale, areaScale);
                    break;
                case MonsterAttackImpactStyle.BossInferno:
                    BuildBossInferno(center, forward, impactScale, areaScale);
                    break;
                case MonsterAttackImpactStyle.CrystalBreak:
                    BuildCrystal(center, forward, impactScale, areaScale);
                    break;
                case MonsterAttackImpactStyle.ArcaneBurst:
                    BuildArcane(center, impactScale, areaScale);
                    break;
                case MonsterAttackImpactStyle.ShadowRend:
                    BuildShadow(center, forward, impactScale, areaScale);
                    break;
                case MonsterAttackImpactStyle.EarthSlam:
                    BuildEarth(center, impactScale, areaScale);
                    break;
                case MonsterAttackImpactStyle.VenomBloom:
                    BuildVenom(center, impactScale, areaScale);
                    break;
                case MonsterAttackImpactStyle.WindCut:
                    BuildWind(center, forward, impactScale, areaScale);
                    break;
                case MonsterAttackImpactStyle.WaterSplash:
                    BuildWater(center, impactScale, areaScale);
                    break;
                default:
                    BuildPhysical(center, forward, impactScale, areaScale);
                    break;
            }

            if (_stunDurationTicks > 0)
                CreateShockRing(center, _cellSize * (1.2f + areaScale), _cellSize * 0.045f, _palette.Accent, 0.05f, 0.5f, 1.55f);
        }

        private void BuildPhysical(Vector3 center, Vector3 forward, float impactScale, float areaScale)
        {
            CreateImpactLight(center, _palette.Primary, 1.4f * _palette.Glow, 0f, 0.3f);
            CreateShockRing(center, _cellSize * (0.95f + areaScale), _cellSize * 0.04f, _palette.Accent, 0f, 0.42f, 1.45f);

            foreach (var cell in _sampledWorldCells)
            {
                CreateParticleBurst("ForestDust", cell, _palette.Smoke, _palette.Accent, 12, 18, 0.35f, 0.8f, 0.12f, 0.4f, 0.02f, 0.06f, 0.12f);
                CreateWorldSlash(cell + Vector3.up * 0.32f, forward, _cellSize * 0.95f * impactScale, _cellSize * 0.045f, _palette.Primary, 0f, 0.28f);
            }
        }

        private void BuildEarth(Vector3 center, float impactScale, float areaScale)
        {
            CreateImpactLight(center, _palette.Accent, 1.1f * _palette.Glow, 0f, 0.24f);
            CreateShockRing(center, _cellSize * (1.05f + areaScale), _cellSize * 0.065f, _palette.Primary, 0f, 0.55f, 1.85f);
            CreateParticleBurst("GroundCrackDust", center, _palette.Smoke, _palette.Secondary, 36, 52, 0.55f, 1.15f, 0.25f, 0.8f, 0.04f, 0.13f, 0.45f);

            for (int i = 0; i < _sampledWorldCells.Count; i++)
            {
                Vector3 cell = _sampledWorldCells[i];
                CreateShockRing(cell, _cellSize * 0.5f, _cellSize * 0.035f, _palette.Secondary, i * 0.015f, 0.45f, 1.45f);
                if (i % 2 == 0)
                    CreateShard(cell, _palette.Primary, 0.12f, 0.42f, 0.05f + i * 0.01f);
            }
        }

        private void BuildArcane(Vector3 center, float impactScale, float areaScale)
        {
            CreateImpactLight(center, _palette.Primary, 1.7f * _palette.Glow, 0f, 0.42f);
            CreateShockRing(center, _cellSize * (0.9f + areaScale), _cellSize * 0.035f, _palette.Primary, 0f, 0.55f, 1.35f);
            CreateShockRing(center, _cellSize * (0.55f + areaScale * 0.55f), _cellSize * 0.022f, _palette.Accent, 0.08f, 0.62f, 1.75f);
            CreateParticleBurst("ArcaneMotes", center, _palette.Primary, _palette.Accent, 42, 58, 0.45f, 1.05f, 0.08f, 0.34f, 0.025f, 0.06f, -0.04f);

            foreach (var cell in _sampledWorldCells)
                CreateVerticalRune(cell, _palette.Secondary, _palette.Primary);
        }

        private void BuildShadow(Vector3 center, Vector3 forward, float impactScale, float areaScale)
        {
            CreateImpactLight(center, _palette.Primary, 1.2f * _palette.Glow, 0f, 0.28f);
            CreateParticleBurst("ShadowSmoke", center, _palette.Smoke, _palette.Primary, 42, 64, 0.55f, 1.25f, 0.05f, 0.25f, 0.08f, 0.18f, -0.08f);
            CreateShockRing(center, _cellSize * (1.0f + areaScale), _cellSize * 0.05f, _palette.Secondary, 0f, 0.46f, 1.6f);

            for (int i = 0; i < _sampledWorldCells.Count; i++)
            {
                Vector3 offsetForward = Quaternion.Euler(0f, i % 2 == 0 ? -22f : 22f, 0f) * forward;
                CreateWorldSlash(_sampledWorldCells[i] + Vector3.up * 0.34f, offsetForward, _cellSize * 1.1f * impactScale, _cellSize * 0.05f, _palette.Primary, i * 0.012f, 0.33f);
            }
        }

        private void BuildFire(Vector3 center, Vector3 forward, float impactScale, float areaScale)
        {
            CreateImpactLight(center, _palette.Primary, 2.0f * _palette.Glow, 0f, 0.36f);
            CreateShockRing(center, _cellSize * (0.9f + areaScale), _cellSize * 0.055f, _palette.Secondary, 0f, 0.46f, 1.55f);
            CreateDirectionalStreaks(center, forward, _palette.Primary, _cellSize * (1.2f + areaScale) * impactScale);

            foreach (var cell in _sampledWorldCells)
            {
                CreateParticleBurst("FlameTongue", cell, _palette.Primary, _palette.Secondary, 18, 28, 0.3f, 0.72f, 0.35f, 1.15f, 0.055f, 0.16f, -0.05f);
                CreateParticleBurst("Embers", cell + Vector3.up * 0.12f, _palette.Accent, _palette.Primary, 8, 14, 0.5f, 1.05f, 0.5f, 1.35f, 0.018f, 0.05f, -0.18f);
            }
        }

        private void BuildBossInferno(Vector3 center, Vector3 forward, float impactScale, float areaScale)
        {
            CreateImpactLight(center, _palette.Primary, 2.7f * _palette.Glow, 0f, 0.48f);
            CreateShockRing(center, _cellSize * (1.15f + areaScale), _cellSize * 0.08f, _palette.Primary, 0f, 0.62f, 2.05f);
            CreateShockRing(center, _cellSize * (0.72f + areaScale * 0.8f), _cellSize * 0.045f, _palette.Accent, 0.07f, 0.52f, 1.65f);
            CreateDirectionalStreaks(center, forward, _palette.Accent, _cellSize * (1.6f + areaScale) * impactScale);
            CreateParticleBurst("InfernoCore", center, _palette.Primary, _palette.Accent, 58, 86, 0.45f, 1.1f, 0.35f, 1.35f, 0.045f, 0.15f, -0.1f);

            for (int i = 0; i < _sampledWorldCells.Count; i += 2)
                CreateShard(_sampledWorldCells[i], _palette.Secondary, 0.14f, 0.55f, i * 0.012f);
        }

        private void BuildCrystal(Vector3 center, Vector3 forward, float impactScale, float areaScale)
        {
            CreateImpactLight(center, _palette.Primary, 1.75f * _palette.Glow, 0f, 0.4f);
            CreateShockRing(center, _cellSize * (0.95f + areaScale), _cellSize * 0.045f, _palette.Primary, 0f, 0.5f, 1.45f);
            CreateWorldSlash(center + Vector3.up * 0.45f, forward, _cellSize * (1.65f + areaScale) * impactScale, _cellSize * 0.06f, _palette.Accent, 0f, 0.35f);
            CreateParticleBurst("CrystalSplinters", center, _palette.Accent, _palette.Primary, 30, 46, 0.45f, 0.95f, 0.35f, 1.2f, 0.018f, 0.07f, 0.05f);

            for (int i = 0; i < _sampledWorldCells.Count; i++)
                CreateShard(_sampledWorldCells[i], i % 2 == 0 ? _palette.Primary : _palette.Accent, 0.1f, 0.48f, i * 0.012f);
        }

        private void BuildVenom(Vector3 center, float impactScale, float areaScale)
        {
            CreateImpactLight(center, _palette.Primary, 1.35f * _palette.Glow, 0f, 0.35f);
            CreateShockRing(center, _cellSize * (0.82f + areaScale), _cellSize * 0.045f, _palette.Primary, 0f, 0.5f, 1.55f);
            CreateParticleBurst("Spores", center, _palette.Primary, _palette.Accent, 46, 68, 0.65f, 1.45f, 0.04f, 0.24f, 0.025f, 0.07f, -0.12f);

            foreach (var cell in _sampledWorldCells)
            {
                CreateParticleBurst("PollenHit", cell, _palette.Secondary, _palette.Primary, 10, 16, 0.5f, 1f, 0.06f, 0.35f, 0.035f, 0.095f, -0.02f);
                CreateShockRing(cell, _cellSize * 0.38f, _cellSize * 0.025f, _palette.Accent, 0.02f, 0.35f, 1.5f);
            }
        }

        private void BuildWind(Vector3 center, Vector3 forward, float impactScale, float areaScale)
        {
            CreateImpactLight(center, _palette.Primary, 0.9f * _palette.Glow, 0f, 0.22f);
            CreateShockRing(center, _cellSize * (0.95f + areaScale), _cellSize * 0.032f, _palette.Primary, 0f, 0.36f, 1.75f);
            CreateDirectionalStreaks(center, forward, _palette.Primary, _cellSize * (1.4f + areaScale) * impactScale);

            for (int i = 0; i < _sampledWorldCells.Count; i++)
            {
                Vector3 angledForward = Quaternion.Euler(0f, -35f + i * 11f, 0f) * forward;
                CreateWorldSlash(_sampledWorldCells[i] + Vector3.up * 0.36f, angledForward, _cellSize * 0.9f, _cellSize * 0.025f, _palette.Accent, i * 0.01f, 0.24f);
                CreateParticleBurst("AirMotes", _sampledWorldCells[i], _palette.Primary, _palette.Accent, 6, 12, 0.4f, 0.85f, 0.18f, 0.55f, 0.012f, 0.035f, -0.16f);
            }
        }

        private void BuildWater(Vector3 center, float impactScale, float areaScale)
        {
            CreateImpactLight(center, _palette.Primary, 1.15f * _palette.Glow, 0f, 0.28f);
            CreateShockRing(center, _cellSize * (0.85f + areaScale), _cellSize * 0.04f, _palette.Primary, 0f, 0.42f, 1.65f);
            CreateParticleBurst("WaterSplash", center, _palette.Accent, _palette.Primary, 44, 64, 0.35f, 0.9f, 0.45f, 1.25f, 0.025f, 0.085f, 0.25f);

            foreach (var cell in _sampledWorldCells)
                CreateShockRing(cell, _cellSize * 0.42f, _cellSize * 0.025f, _palette.Accent, 0.02f, 0.34f, 1.7f);
        }

        private void CreateDirectionalStreaks(Vector3 center, Vector3 forward, Color color, float length)
        {
            CreateWorldSlash(center + Vector3.up * 0.38f, forward, length, _cellSize * 0.05f, color, 0f, 0.28f);
            CreateWorldSlash(center + Vector3.up * 0.28f, Quaternion.Euler(0f, 18f, 0f) * forward, length * 0.75f, _cellSize * 0.03f, _palette.Accent, 0.04f, 0.24f);
            CreateWorldSlash(center + Vector3.up * 0.22f, Quaternion.Euler(0f, -22f, 0f) * forward, length * 0.68f, _cellSize * 0.026f, _palette.Secondary, 0.08f, 0.22f);
        }

        private ParticleSystem CreateParticleBurst(
            string name,
            Vector3 position,
            Color startColor,
            Color endColor,
            int minBurst,
            int maxBurst,
            float lifetimeMin,
            float lifetimeMax,
            float speedMin,
            float speedMax,
            float sizeMin,
            float sizeMax,
            float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;

            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = Mathf.Max(0.2f, lifetimeMax);
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(startColor, endColor);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(maxBurst * 3, 48);

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)Mathf.Max(1, minBurst), (short)Mathf.Max(minBurst, maxBurst))
            });

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = _cellSize * 0.26f;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateFadeGradient(startColor, endColor));

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.65f, 1f, 0.05f));

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = MonsterAttackImpactVfx.GetParticleMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            particles.Play(true);
            return particles;
        }

        private void CreateShockRing(Vector3 position, float radius, float width, Color color, float delay, float duration, float scaleTo)
        {
            var go = new GameObject("ImpactRing");
            go.transform.SetParent(transform, false);
            go.transform.position = position;

            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = MonsterAttackImpactVfx.GetLineMaterial();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = CircleSegments;
            line.numCornerVertices = 4;
            line.numCapVertices = 4;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.widthMultiplier = width;
            line.startColor = color;
            line.endColor = color;

            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i / (float)CircleSegments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            _lines.Add(new LineFx(line, color, width, delay, duration, 1f, Mathf.Max(1f, scaleTo), true));
        }

        private void CreateVerticalRune(Vector3 position, Color outerColor, Color innerColor)
        {
            CreateShockRing(position, _cellSize * 0.34f, _cellSize * 0.018f, outerColor, 0.02f, 0.58f, 1.3f);
            CreateShockRing(position + Vector3.up * 0.015f, _cellSize * 0.18f, _cellSize * 0.014f, innerColor, 0.08f, 0.48f, 1.6f);
        }

        private void CreateWorldSlash(Vector3 center, Vector3 forward, float length, float width, Color color, float delay, float duration)
        {
            forward = Flatten(forward);
            Vector3 slashDir = Quaternion.Euler(0f, -32f, 0f) * forward;
            Vector3 side = Quaternion.Euler(0f, 90f, 0f) * slashDir;
            float half = Mathf.Max(0.1f, length * 0.5f);

            var go = new GameObject("ImpactSlash");
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = MonsterAttackImpactVfx.GetLineMaterial();
            line.useWorldSpace = true;
            line.positionCount = 3;
            line.numCornerVertices = 6;
            line.numCapVertices = 8;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.widthMultiplier = width;
            line.startColor = color;
            line.endColor = MonsterAttackImpactVfx.WithAlpha(color, color.a * 0.2f);
            line.SetPosition(0, center - slashDir * half + side * 0.12f);
            line.SetPosition(1, center + Vector3.up * 0.16f);
            line.SetPosition(2, center + slashDir * half - side * 0.12f);

            _lines.Add(new LineFx(line, color, width, delay, duration, 1f, 1f, false));
        }

        private void CreateShard(Vector3 position, Color color, float width, float height, float delay)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "ImpactShard";
            shard.transform.SetParent(transform, false);
            shard.transform.position = position + new Vector3(
                UnityEngine.Random.Range(-0.18f, 0.18f),
                height * 0.35f,
                UnityEngine.Random.Range(-0.18f, 0.18f));
            shard.transform.rotation = Quaternion.Euler(
                UnityEngine.Random.Range(-18f, 18f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(-24f, 24f));
            shard.transform.localScale = new Vector3(width, height, width * 0.62f);

            if (shard.TryGetComponent<Collider>(out var collider))
                Destroy(collider);

            var renderer = shard.GetComponent<Renderer>();
            renderer.sharedMaterial = MonsterAttackImpactVfx.GetMeshMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            MonsterAttackImpactVfx.SetRendererColor(renderer, color);

            _meshes.Add(new MeshFx(shard.transform, renderer, color, shard.transform.localScale, delay, 0.72f));
        }

        private void CreateImpactLight(Vector3 position, Color color, float intensity, float delay, float duration)
        {
            var go = new GameObject("ImpactLight");
            go.transform.SetParent(transform, false);
            go.transform.position = position + Vector3.up * 0.6f;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = Mathf.Clamp(_cellSize * (2.2f + _sampledWorldCells.Count * 0.08f), 1.8f, 5.2f);
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            _lights.Add(new LightFx(light, Mathf.Max(0.1f, intensity), delay, duration));
        }

        private IEnumerator CoLifetime()
        {
            float totalDuration = BaseLifetime + Mathf.Clamp(_sampledWorldCells.Count * 0.015f, 0f, 0.35f);
            float elapsed = 0f;

            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                UpdateLines(elapsed);
                UpdateMeshes(elapsed);
                UpdateLights(elapsed);
                yield return null;
            }

            Destroy(gameObject);
        }

        private void UpdateLines(float elapsed)
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                var lineFx = _lines[i];
                if (lineFx.Renderer == null)
                    continue;

                float t = Mathf.InverseLerp(lineFx.Delay, lineFx.Delay + lineFx.Duration, elapsed);
                bool active = elapsed >= lineFx.Delay && t <= 1f;
                lineFx.Renderer.enabled = active;
                if (!active)
                    continue;

                float fade = Mathf.Pow(1f - Mathf.Clamp01(t), 1.35f);
                Color color = MonsterAttackImpactVfx.WithAlpha(lineFx.Color, lineFx.Color.a * fade);
                lineFx.Renderer.startColor = color;
                lineFx.Renderer.endColor = MonsterAttackImpactVfx.WithAlpha(color, color.a * 0.35f);
                lineFx.Renderer.widthMultiplier = lineFx.Width * Mathf.Lerp(1f, 0.42f, t);

                if (lineFx.ScaleTransform)
                {
                    float scale = Mathf.Lerp(lineFx.ScaleFrom, lineFx.ScaleTo, Mathf.SmoothStep(0f, 1f, t));
                    lineFx.Renderer.transform.localScale = new Vector3(scale, scale, scale);
                }
            }
        }

        private void UpdateMeshes(float elapsed)
        {
            for (int i = 0; i < _meshes.Count; i++)
            {
                var meshFx = _meshes[i];
                if (meshFx.Transform == null)
                    continue;

                float t = Mathf.InverseLerp(meshFx.Delay, meshFx.Delay + meshFx.Duration, elapsed);
                bool active = elapsed >= meshFx.Delay && t <= 1f;
                meshFx.Transform.gameObject.SetActive(active);
                if (!active)
                    continue;

                float grow = Mathf.Clamp01(t / 0.25f);
                float fade = Mathf.Clamp01((1f - t) / 0.4f);
                float scale = Mathf.Lerp(0.25f, 1f, grow) * Mathf.Lerp(0.45f, 1f, fade);
                meshFx.Transform.localScale = meshFx.BaseScale * scale;
                MonsterAttackImpactVfx.SetRendererColor(meshFx.Renderer, MonsterAttackImpactVfx.WithAlpha(meshFx.Color, meshFx.Color.a * fade));
            }
        }

        private void UpdateLights(float elapsed)
        {
            for (int i = 0; i < _lights.Count; i++)
            {
                var lightFx = _lights[i];
                if (lightFx.Light == null)
                    continue;

                float t = Mathf.InverseLerp(lightFx.Delay, lightFx.Delay + lightFx.Duration, elapsed);
                if (elapsed < lightFx.Delay || t > 1f)
                {
                    lightFx.Light.intensity = 0f;
                    continue;
                }

                float pulse = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                lightFx.Light.intensity = lightFx.Intensity * pulse;
            }
        }

        private static Vector3 GetCenter(IReadOnlyList<Vector3> points)
        {
            Vector3 center = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
                center += points[i];
            return center / Mathf.Max(1, points.Count);
        }

        private static Vector3 RotationToForward(float rotation)
        {
            return Flatten(Quaternion.Euler(0f, rotation, 0f) * Vector3.forward);
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            if (value.sqrMagnitude < 0.0001f)
                return Vector3.forward;
            return value.normalized;
        }

        private static Gradient CreateFadeGradient(Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 0.65f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(start.a, 0f),
                    new GradientAlphaKey(end.a * 0.72f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private readonly struct LineFx
        {
            public readonly LineRenderer Renderer;
            public readonly Color Color;
            public readonly float Width;
            public readonly float Delay;
            public readonly float Duration;
            public readonly float ScaleFrom;
            public readonly float ScaleTo;
            public readonly bool ScaleTransform;

            public LineFx(LineRenderer renderer, Color color, float width, float delay, float duration, float scaleFrom, float scaleTo, bool scaleTransform)
            {
                Renderer = renderer;
                Color = color;
                Width = width;
                Delay = delay;
                Duration = Mathf.Max(0.05f, duration);
                ScaleFrom = scaleFrom;
                ScaleTo = scaleTo;
                ScaleTransform = scaleTransform;
            }
        }

        private readonly struct MeshFx
        {
            public readonly Transform Transform;
            public readonly Renderer Renderer;
            public readonly Color Color;
            public readonly Vector3 BaseScale;
            public readonly float Delay;
            public readonly float Duration;

            public MeshFx(Transform transform, Renderer renderer, Color color, Vector3 baseScale, float delay, float duration)
            {
                Transform = transform;
                Renderer = renderer;
                Color = color;
                BaseScale = baseScale;
                Delay = delay;
                Duration = Mathf.Max(0.05f, duration);
            }
        }

        private readonly struct LightFx
        {
            public readonly Light Light;
            public readonly float Intensity;
            public readonly float Delay;
            public readonly float Duration;

            public LightFx(Light light, float intensity, float delay, float duration)
            {
                Light = light;
                Intensity = intensity;
                Delay = delay;
                Duration = Mathf.Max(0.05f, duration);
            }
        }
    }
