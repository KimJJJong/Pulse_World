using RhythmRPG.Visual;
using UnityEngine;

public sealed class TownAppearancePreviewController : MonoBehaviour
{
    private Renderer[] _sourceRenderers;
    private Animator _sourceAnimator;
    private EntityVisual _entityVisual;
    private CharacterVisualController _sourceVisualController;
    private GameObject _spawnedPreview;
    private int _currentAppearanceId = int.MinValue;

    private void Awake()
    {
        CacheSourceComponents();
    }

    private void OnEnable()
    {
        HomeAppearanceSelectorUI.AppearanceAppliedChanged += HandleAppearanceChanged;
        ApplyAppearance(HomeAppearanceSelectorUI.LastAppliedAppearanceId);
    }

    private void OnDisable()
    {
        HomeAppearanceSelectorUI.AppearanceAppliedChanged -= HandleAppearanceChanged;
    }

    public void ApplyCurrent()
    {
        ApplyAppearance(HomeAppearanceSelectorUI.LastAppliedAppearanceId);
    }

    public bool RefreshEquipmentNow()
    {
        var visual = _spawnedPreview != null
            ? _spawnedPreview.GetComponentInChildren<CharacterVisualController>(true)
            : _sourceVisualController;

        if (visual == null)
            return false;

        visual.RefreshLocalPlayerEquipmentNow();
        return true;
    }

    private void HandleAppearanceChanged(int savedAppearanceId, int appliedAppearanceId)
    {
        ApplyAppearance(appliedAppearanceId);
    }

    private void CacheSourceComponents()
    {
        _sourceRenderers = GetComponentsInChildren<Renderer>(true);
        _sourceAnimator = GetComponentInChildren<Animator>(true);
        _entityVisual = GetComponent<EntityVisual>();
        if (_entityVisual == null)
            _entityVisual = GetComponentInParent<EntityVisual>();

        _sourceVisualController = GetComponent<CharacterVisualController>();
        if (_sourceVisualController == null)
            _sourceVisualController = GetComponentInChildren<CharacterVisualController>(true);
    }

    private void ApplyAppearance(int appearanceId)
    {
        if (_currentAppearanceId == appearanceId && (appearanceId <= 0 || _spawnedPreview != null))
            return;

        _currentAppearanceId = appearanceId;

        if (appearanceId <= 0)
        {
            DestroySpawnedPreview();
            SetSourceVisible(true);
            RefreshEquipmentNow();
            return;
        }

        var prefab = ResolveAppearancePrefab(appearanceId);
        if (prefab == null)
        {
            Debug.LogWarning($"[TownAppearancePreviewController] Appearance prefab not found for id={appearanceId}.");
            return;
        }

        DestroySpawnedPreview();
        SetSourceVisible(false);

        _spawnedPreview = Instantiate(prefab, transform);
        _spawnedPreview.name = $"{prefab.name}_TownPreview";
        _spawnedPreview.transform.localPosition = Vector3.zero;
        _spawnedPreview.transform.localRotation = Quaternion.identity;
        _spawnedPreview.transform.localScale = Vector3.one;

        var visual = _spawnedPreview.GetComponentInChildren<CharacterVisualController>(true);
        if (visual != null)
        {
            visual.SetContext(CharacterContext.Game);
            visual.SetLocalPlayer(true);
            visual.RefreshLocalPlayerEquipmentNow();
        }

        var animator = _spawnedPreview.GetComponentInChildren<Animator>(true);
        if (_entityVisual != null && animator != null)
            _entityVisual.Bind(animator);
    }

    private static GameObject ResolveAppearancePrefab(int appearanceId)
    {
        if (!AppearanceCatalog.TryGetDefinitionPath(appearanceId, out var definitionPath))
            return null;

        var definition = Resources.Load<RhythmRPG.Editor.StageBuilder.EntityDefinitionSO>(definitionPath);
        return definition != null ? definition.Prefab : null;
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
            if (visible)
            {
                _sourceVisualController.SetContext(CharacterContext.Game);
                _sourceVisualController.SetLocalPlayer(true);
            }
            else
            {
                _sourceVisualController.SetLocalPlayer(false);
            }
        }

        if (visible && _entityVisual != null && _sourceAnimator != null)
            _entityVisual.Bind(_sourceAnimator);
    }

    private void DestroySpawnedPreview()
    {
        if (_spawnedPreview == null)
            return;

        Destroy(_spawnedPreview);
        _spawnedPreview = null;
    }
}
