using System.Collections.Generic;
using System.Threading.Tasks;
using RhythmRPG.Visual;
using UnityEngine;

public sealed class HomeAppearancePreviewController : MonoBehaviour
{
    private readonly Dictionary<int, string> _entityPathMap = new();

    private Renderer[] _sourceRenderers;
    private Animator _sourceAnimator;
    private CharacterVisualController _sourceVisualController;
    private GameObject _spawnedPreview;
    private int _currentAppearanceId = int.MinValue;
    private bool _fetchingInitialAppearance;

    private void Awake()
    {
        CacheSourceComponents();
        LoadEntityMapping();
    }

    private void OnEnable()
    {
        HomeAppearanceSelectorUI.AppearanceAppliedChanged += HandleAppearanceChanged;
        ApplyAppearance(HomeAppearanceSelectorUI.LastAppliedAppearanceId);
    }

    private async void Start()
    {
        if (!Application.isPlaying)
            return;

        await RefreshInitialAppearanceAsync();
    }

    private void OnDisable()
    {
        HomeAppearanceSelectorUI.AppearanceAppliedChanged -= HandleAppearanceChanged;
        DestroySpawnedPreview();
        SetSourceVisible(true);
    }

    private void HandleAppearanceChanged(int savedAppearanceId, int appliedAppearanceId)
    {
        ApplyAppearance(appliedAppearanceId);
    }

    private async Task RefreshInitialAppearanceAsync()
    {
        if (_fetchingInitialAppearance)
            return;

        var uid = GetCurrentUid();
        var api = AppBootstrap.Instance?.Root?.PlayerStateApi;
        if (string.IsNullOrWhiteSpace(uid) || api == null)
            return;

        _fetchingInitialAppearance = true;
        var res = await api.GetPlayerStateAsync(uid);
        _fetchingInitialAppearance = false;

        if (!this || !isActiveAndEnabled || !res.Ok || res.Data == null)
            return;

        HomeAppearanceSelectorUI.PublishAppearanceAppliedChanged(res.Data.SavedAppearanceId, res.Data.AppearanceId);
        ApplyAppearance(res.Data.AppearanceId);
    }

    private void CacheSourceComponents()
    {
        _sourceRenderers = GetComponentsInChildren<Renderer>(true);
        _sourceAnimator = GetComponent<Animator>();
        _sourceVisualController = GetComponent<CharacterVisualController>();
    }

    private void LoadEntityMapping()
    {
        var textAsset = Resources.Load<TextAsset>("Data/EntityData");
        if (textAsset == null)
            return;

        try
        {
            var root = JsonUtility.FromJson<EntityDataRoot>(textAsset.text);
            if (root?.Entities == null)
                return;

            foreach (var e in root.Entities)
            {
                if (!string.IsNullOrWhiteSpace(e.ResourcePath))
                    _entityPathMap[e.EntityId] = e.ResourcePath;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[HomeAppearancePreviewController] Failed to parse EntityData.json: {ex.Message}");
        }
    }

    private void ApplyAppearance(int appearanceId)
    {
        if (_currentAppearanceId == appearanceId && (_spawnedPreview != null || appearanceId <= 0))
            return;

        _currentAppearanceId = appearanceId;

        if (appearanceId <= 0)
        {
            DestroySpawnedPreview();
            SetSourceVisible(true);
            return;
        }

        var prefab = ResolveAppearancePrefab(appearanceId);
        if (prefab == null)
        {
            Debug.LogWarning($"[HomeAppearancePreviewController] Appearance prefab not found for id={appearanceId}. Keeping base character visible.");
            DestroySpawnedPreview();
            SetSourceVisible(true);
            return;
        }

        DestroySpawnedPreview();
        SetSourceVisible(false);

        var parent = transform.parent;
        if (parent != null)
        {
            _spawnedPreview = Instantiate(prefab, parent);
            _spawnedPreview.transform.localPosition = transform.localPosition;
            _spawnedPreview.transform.localRotation = transform.localRotation;
            _spawnedPreview.transform.localScale = transform.localScale;
        }
        else
        {
            _spawnedPreview = Instantiate(prefab, transform.position, transform.rotation);
            _spawnedPreview.transform.localScale = transform.localScale;
        }

        _spawnedPreview.name = $"{prefab.name}_HomePreview";

        if (_spawnedPreview.TryGetComponent<CharacterVisualController>(out var visualCtrl))
        {
            visualCtrl.SetContext(CharacterContext.Home);
            visualCtrl.SetLocalPlayer(false);
        }
    }

    private GameObject ResolveAppearancePrefab(int appearanceId)
    {
        if (AppearanceCatalog.TryGetDefinitionPath(appearanceId, out var definitionPath))
        {
            var def = Resources.Load<RhythmRPG.Editor.StageBuilder.EntityDefinitionSO>(definitionPath);
            if (def != null && def.Prefab != null)
                return def.Prefab;
        }

        if (_entityPathMap.TryGetValue(appearanceId, out var resourcePath))
        {
            var def = Resources.Load<RhythmRPG.Editor.StageBuilder.EntityDefinitionSO>(resourcePath);
            if (def != null && def.Prefab != null)
                return def.Prefab;
        }

        return null;
    }

    private void SetSourceVisible(bool visible)
    {
        if (_sourceRenderers != null)
        {
            foreach (var renderer in _sourceRenderers)
            {
                if (renderer != null)
                    renderer.enabled = visible;
            }
        }

        if (_sourceAnimator != null)
            _sourceAnimator.enabled = visible;

        if (_sourceVisualController != null)
        {
            _sourceVisualController.SetContext(CharacterContext.Home);
            _sourceVisualController.SetLocalPlayer(false);
        }
    }

    private void DestroySpawnedPreview()
    {
        if (_spawnedPreview != null)
        {
            Destroy(_spawnedPreview);
            _spawnedPreview = null;
        }
    }

    private static string GetCurrentUid()
    {
        if (SessionContext.Instance != null && !string.IsNullOrWhiteSpace(SessionContext.Instance.Uid))
            return SessionContext.Instance.Uid;

        var root = AppBootstrap.Instance?.Root;
        if (root != null && !string.IsNullOrWhiteSpace(root.Tokens.Uid))
            return root.Tokens.Uid;

        return "";
    }

    [System.Serializable]
    private class EntityDataRoot
    {
        public List<EntityDataEntry> Entities;
    }

    [System.Serializable]
    private class EntityDataEntry
    {
        public int EntityId;
        public string ResourcePath;
    }
}
