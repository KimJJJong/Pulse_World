using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using RhythmRPG.Visual;

public sealed class TownHomeUiController : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private HomeUiPageNavigator _navigator;
    [SerializeField] private HomeSceneCameraDirector _cameraDirector;
    [SerializeField] private TownInventoryUI _townInventoryUi;
    [SerializeField] private bool _blockRhythmInput = true;
    [SerializeField] private bool _hideOtherSceneCanvasesWhileOpen = true;
    [SerializeField] private float _restoreCameraFollowDelay = 0.35f;

    private readonly List<GameObject> _hiddenSceneCanvases = new();
    private CameraFollow _cameraFollow;
    private bool _hadCameraFollow;
    private bool _wasCameraFollowEnabled;
    private RhythmInputController _rhythmInputController;
    private bool _hadRhythmInputController;
    private bool _wasRhythmInputControllerEnabled;
    private InventoryManager _subscribedInventory;
    private Coroutine _restoreCameraFollowRoutine;
    private int _lastToggleFrame = -1;
    private bool _inventoryWindowOpenFromHomeUi;

    public bool IsOpen => (_root != null && _root.activeSelf) || _inventoryWindowOpenFromHomeUi;
    public bool ConsumedToggleThisFrame => _lastToggleFrame == Time.frameCount;

    private void Awake()
    {
        ResolveReferences();
        SetRootActive(false);
        SetCameraDirectorActive(false);
    }

    private void OnDisable()
    {
        if (_restoreCameraFollowRoutine != null)
        {
            StopCoroutine(_restoreCameraFollowRoutine);
            _restoreCameraFollowRoutine = null;
        }

        ForceCameraHomeImmediate();
        RestoreCameraFollow();
        RestoreRhythmInput();
        UnhookInventoryRefresh();
        RestoreHiddenSceneCanvases();
        SetCameraDirectorActive(false);
        _inventoryWindowOpenFromHomeUi = false;
    }

    private void Update()
    {
        if (ConsumedToggleThisFrame)
            return;

        if (_inventoryWindowOpenFromHomeUi)
        {
            var inventory = ResolveTownInventoryUi();
            if (inventory == null || !inventory.IsOpen)
            {
                CloseHomeUi();
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleHomeUi();
            return;
        }
    }

    public void ToggleHomeUi()
    {
        if (IsOpen)
            CloseHomeUi();
        else
            OpenHomeUi();
    }

    public bool OpenTownInventoryWindow()
    {
        ResolveReferences();
        var inventory = ResolveTownInventoryUi();
        if (inventory == null)
            return false;

        _lastToggleFrame = Time.frameCount;
        if (IsOpen)
        {
            _inventoryWindowOpenFromHomeUi = true;
            SetRootActive(false);
        }

        inventory.OpenInventory();
        return true;
    }

    public void OpenHomeUi()
    {
        _lastToggleFrame = Time.frameCount;
        ResolveReferences();
        CloseTownInventory();
        _inventoryWindowOpenFromHomeUi = false;

        var target = ResolvePresentationTarget();
        SetCameraDirectorActive(true);
        if (_cameraDirector != null)
        {
            _cameraDirector.CaptureCurrentPose();
            _cameraDirector.SetModelRoot(target);
            _cameraDirector.SetUseModelFacingForPresentation(true);
            _cameraDirector.SetInvertModelFacingForPresentation(true);
            _cameraDirector.SetUseCurrentCameraOppositeForPresentation(true);
            _cameraDirector.SetUseStagedPresentationEnter(true);
        }
        if (_navigator != null)
            _navigator.SetCameraDirector(_cameraDirector);

        EnsureAppearancePreview(target);
        SuspendRhythmInput();
        SuspendCameraFollow();
        HideOtherSceneCanvases();
        SetRootActive(true);
        EnsureUiInputReady();
        HookInventoryRefresh();
        RefreshLocalCharacterEquipment();

        if (_navigator != null)
        {
            _navigator.ShowHome();
            _navigator.SetForceCameraPresentation(true);
        }
        else
        {
            _cameraDirector?.SetAppearancePresentation(true);
        }

    }

    public void CloseHomeUi()
    {
        _lastToggleFrame = Time.frameCount;
        ResolveReferences();
        CloseTownInventory();
        _inventoryWindowOpenFromHomeUi = false;

        if (_navigator != null)
        {
            _navigator.ShowHome();
            _navigator.SetForceCameraPresentation(false);
        }
        else
        {
            _cameraDirector?.SetAppearancePresentation(false);
        }
        SetRootActive(false);
        RestoreHiddenSceneCanvases();
        UnhookInventoryRefresh();

        if (_restoreCameraFollowRoutine != null)
            StopCoroutine(_restoreCameraFollowRoutine);

        var restoreDelay = _restoreCameraFollowDelay;
        if (_cameraDirector != null)
            restoreDelay = Mathf.Max(restoreDelay, _cameraDirector.GetStagedTransitionDuration());

        _restoreCameraFollowRoutine = StartCoroutine(RestoreGameplayControlAfterDelay(restoreDelay));
    }

    private void ResolveReferences()
    {
        if (_root == null)
        {
            var child = transform.Find("Canvas_TownHomeOverlay");
            if (child != null)
                _root = child.gameObject;
        }

        if (_root == null)
        {
            var canvas = FindSceneObjectByName<Canvas>("Canvas_TownHomeOverlay");
            if (canvas != null)
                _root = canvas.gameObject;
        }

        if (_navigator == null && _root != null)
            _navigator = _root.GetComponent<HomeUiPageNavigator>();

        if (_cameraDirector == null)
            _cameraDirector = GetComponent<HomeSceneCameraDirector>();

        if (_cameraDirector == null && _root != null)
            _cameraDirector = _root.GetComponent<HomeSceneCameraDirector>();
    }

    private void SetRootActive(bool active)
    {
        if (_root != null && _root.activeSelf != active)
            _root.SetActive(active);
    }

    private void HideOtherSceneCanvases()
    {
        RestoreHiddenSceneCanvases();

        if (!_hideOtherSceneCanvasesWhileOpen || _root == null)
            return;

        var rootCanvas = _root.GetComponent<Canvas>();
        var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (canvas == null || !canvas.gameObject.scene.IsValid())
                continue;

            if (rootCanvas != null && (canvas == rootCanvas || canvas.transform.IsChildOf(rootCanvas.transform)))
                continue;

            var canvasGo = canvas.gameObject;
            if (!canvasGo.activeSelf)
                continue;

            _hiddenSceneCanvases.Add(canvasGo);
            canvasGo.SetActive(false);
        }
    }

    private void RestoreHiddenSceneCanvases()
    {
        for (int i = 0; i < _hiddenSceneCanvases.Count; i++)
        {
            var canvasGo = _hiddenSceneCanvases[i];
            if (canvasGo != null)
                canvasGo.SetActive(true);
        }

        _hiddenSceneCanvases.Clear();
    }

    private void SuspendCameraFollow()
    {
        if (_restoreCameraFollowRoutine != null)
        {
            StopCoroutine(_restoreCameraFollowRoutine);
            _restoreCameraFollowRoutine = null;
        }

        if (_hadCameraFollow && _cameraFollow != null)
        {
            _cameraFollow.enabled = false;
            return;
        }

        _cameraFollow = ResolveCameraFollow();
        _hadCameraFollow = _cameraFollow != null;
        if (!_hadCameraFollow)
            return;

        _wasCameraFollowEnabled = _cameraFollow.enabled;
        _cameraFollow.enabled = false;
    }

    private IEnumerator RestoreGameplayControlAfterDelay(float delay)
    {
        delay = Mathf.Max(0f, delay);
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        ForceCameraHomeImmediate();
        RestoreCameraFollow();
        SetCameraDirectorActive(false);
        RestoreRhythmInput();
        _restoreCameraFollowRoutine = null;
    }

    private void RestoreCameraFollow()
    {
        if (_hadCameraFollow && _cameraFollow != null)
        {
            _cameraFollow.enabled = _wasCameraFollowEnabled;
            if (_wasCameraFollowEnabled)
                _cameraFollow.SnapToTarget();
        }

        _hadCameraFollow = false;
        _cameraFollow = null;
    }

    private void ForceCameraHomeImmediate()
    {
        if (_cameraDirector == null)
            return;

        _cameraDirector.SetAppearancePresentation(false, immediate: true);
    }

    private void SuspendRhythmInput()
    {
        if (!_blockRhythmInput)
            return;

        if (_hadRhythmInputController && _rhythmInputController != null)
        {
            _rhythmInputController.IsInputBlocked = true;
            if (_rhythmInputController.enabled)
                _rhythmInputController.enabled = false;
            return;
        }

        _rhythmInputController = RhythmInputController.Instance;
        _hadRhythmInputController = _rhythmInputController != null;
        if (!_hadRhythmInputController)
            return;

        _wasRhythmInputControllerEnabled = _rhythmInputController.enabled;
        _rhythmInputController.IsInputBlocked = true;
        if (_rhythmInputController.enabled)
            _rhythmInputController.enabled = false;
    }

    private void RestoreRhythmInput()
    {
        if (_hadRhythmInputController && _rhythmInputController != null)
        {
            _rhythmInputController.IsInputBlocked = false;
            _rhythmInputController.enabled = _wasRhythmInputControllerEnabled;
        }
        else if (RhythmInputController.Instance != null)
        {
            RhythmInputController.Instance.IsInputBlocked = false;
        }

        _hadRhythmInputController = false;
        _rhythmInputController = null;
    }

    private void SetCameraDirectorActive(bool active)
    {
        if (_cameraDirector != null && _cameraDirector.enabled != active)
            _cameraDirector.enabled = active;
    }

    private Transform ResolvePresentationTarget()
    {
        var actorId = ClientGameState.Instance != null
            ? ClientGameState.Instance.MyActorId
            : (SessionContext.Instance?.MyActorId ?? 0);
        if (actorId > 0)
        {
            var actor = GameObject.Find($"Entity_{actorId}");
            if (actor != null)
                return ResolveRenderableRoot(actor.transform);
        }

        var follow = ResolveCameraFollow();
        if (follow != null && follow.target != null)
            return ResolveRenderableRoot(follow.target);

        return null;
    }

    private static Transform ResolveRenderableRoot(Transform target)
    {
        if (target == null)
            return null;

        if (target.GetComponentInChildren<Renderer>(true) != null)
            return target;

        var parent = target.parent;
        while (parent != null)
        {
            if (parent.GetComponentInChildren<Renderer>(true) != null)
                return parent;

            parent = parent.parent;
        }

        return target;
    }

    private static CameraFollow ResolveCameraFollow()
    {
        if (CameraBinder.Instance != null && CameraBinder.Instance.Follow != null)
            return CameraBinder.Instance.Follow;

        var mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.TryGetComponent<CameraFollow>(out var follow))
            return follow;

        return FindSceneObject<CameraFollow>();
    }

    private void CloseTownInventory()
    {
        var inventory = ResolveTownInventoryUi();
        if (inventory != null && inventory.IsOpen)
            inventory.CloseInventory();
    }

    private TownInventoryUI ResolveTownInventoryUi()
    {
        if (_townInventoryUi != null && _townInventoryUi.gameObject.scene.IsValid())
            return _townInventoryUi;

        TownInventoryUI fallback = null;
        TownInventoryUI overlayFallback = null;
        var inventories = Resources.FindObjectsOfTypeAll<TownInventoryUI>();
        for (int i = 0; i < inventories.Length; i++)
        {
            var inventory = inventories[i];
            if (inventory == null || !inventory.gameObject.scene.IsValid())
                continue;

            var isHomeOverlayChild = _root != null && inventory.transform.IsChildOf(_root.transform);
            if (isHomeOverlayChild)
            {
                overlayFallback ??= inventory;
                continue;
            }

            if (inventory.gameObject.name == "TownInventory_UI")
            {
                _townInventoryUi = inventory;
                return _townInventoryUi;
            }

            fallback ??= inventory;
        }

        _townInventoryUi = fallback != null ? fallback : overlayFallback;
        return _townInventoryUi;
    }

    private void HookInventoryRefresh()
    {
        UnhookInventoryRefresh();

        _subscribedInventory = InventoryManager.Instance;
        if (_subscribedInventory != null)
            _subscribedInventory.OnInventoryUpdated += HandleInventoryUpdated;
    }

    private void UnhookInventoryRefresh()
    {
        if (_subscribedInventory != null)
            _subscribedInventory.OnInventoryUpdated -= HandleInventoryUpdated;

        _subscribedInventory = null;
    }

    private void HandleInventoryUpdated()
    {
        RefreshLocalCharacterEquipment();
    }

    private void RefreshLocalCharacterEquipment()
    {
        var target = ResolvePresentationTarget();
        var preview = target != null ? target.GetComponent<TownAppearancePreviewController>() : null;
        if (preview != null && preview.RefreshEquipmentNow())
            return;

        var visual = ResolveCharacterVisual(target);
        if (visual != null)
            visual.RefreshLocalPlayerEquipmentNow();
    }

    private static void EnsureAppearancePreview(Transform target)
    {
        if (target == null)
            return;

        var preview = target.GetComponent<TownAppearancePreviewController>();
        if (preview == null)
            preview = target.gameObject.AddComponent<TownAppearancePreviewController>();

        preview.ApplyCurrent();
    }

    private static CharacterVisualController ResolveCharacterVisual(Transform target)
    {
        if (target == null)
            return null;

        var visual = target.GetComponentInChildren<CharacterVisualController>(true);
        if (visual != null)
            return visual;

        var parent = target.parent;
        while (parent != null)
        {
            visual = parent.GetComponentInChildren<CharacterVisualController>(true);
            if (visual != null)
                return visual;

            parent = parent.parent;
        }

        return null;
    }

    private void EnsureUiInputReady()
    {
        var eventSystem = FindSceneObject<EventSystem>();
        if (eventSystem != null)
        {
            if (!eventSystem.gameObject.activeSelf)
                eventSystem.gameObject.SetActive(true);

            eventSystem.enabled = true;
            var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule != null)
                inputModule.enabled = true;

            var standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneModule != null && inputModule != null)
                standaloneModule.enabled = false;
        }

        if (_root == null)
            return;

        _root.transform.SetAsLastSibling();
        var canvas = _root.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = true;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
        }

        var raycaster = _root.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = true;
    }

    private static T FindSceneObject<T>() where T : UnityEngine.Object
    {
        var objects = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < objects.Length; i++)
        {
            var obj = objects[i];
            if (obj == null)
                continue;

            if (obj is Component component && component.gameObject.scene.IsValid())
                return obj;

            if (obj is GameObject go && go.scene.IsValid())
                return obj;
        }

        return null;
    }

    private static T FindSceneObjectByName<T>(string objectName) where T : Component
    {
        var objects = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < objects.Length; i++)
        {
            var obj = objects[i];
            if (obj == null || !obj.gameObject.scene.IsValid())
                continue;

            if (obj.gameObject.name == objectName)
                return obj;
        }

        return null;
    }
}
