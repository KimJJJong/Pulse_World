using UnityEngine;

public sealed class HomeSceneCameraDirector : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private Transform _modelRoot;
    [SerializeField] private float _blendSpeed = 4.5f;
    [SerializeField] private float _modelScreenLeftOffset = 1.65f;
    [SerializeField] private float _appearanceDistance = 8f;
    [SerializeField] private float _appearanceHeightOffset = 1.1f;

    private Vector3 _homePosition;
    private Quaternion _homeRotation;
    private float _homeFov;
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private float _targetFov;
    private bool _captured;

    private void Awake()
    {
        CaptureHomePose();
        SetAppearancePresentation(false, immediate: true);
    }

    private void LateUpdate()
    {
        if (_camera == null)
            return;

        var blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, _blendSpeed) * Time.unscaledDeltaTime);
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, _targetPosition, blend);
        _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, _targetRotation, blend);
        _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _targetFov, blend);
    }

    public void SetAppearancePresentation(bool active)
    {
        SetAppearancePresentation(active, immediate: false);
    }

    public void SetAppearancePresentation(bool active, bool immediate)
    {
        CaptureHomePose();

        if (active && _camera != null && _modelRoot != null)
        {
            var bounds = CalculateModelBounds();
            var center = bounds.center + Vector3.up * _appearanceHeightOffset;
            var homeForward = _homeRotation * Vector3.forward;
            var homeRight = _homeRotation * Vector3.right;
            var lookAt = center + homeRight * _modelScreenLeftOffset;
            var distance = Mathf.Max(_appearanceDistance, bounds.extents.magnitude * 2.6f);

            _targetPosition = lookAt - homeForward * distance;
            _targetRotation = Quaternion.LookRotation(lookAt - _targetPosition, Vector3.up);
            _targetFov = Mathf.Min(_homeFov, 48f);
        }
        else
        {
            _targetPosition = _homePosition;
            _targetRotation = _homeRotation;
            _targetFov = _homeFov;
        }

        if (immediate && _camera != null)
        {
            _camera.transform.SetPositionAndRotation(_targetPosition, _targetRotation);
            _camera.fieldOfView = _targetFov;
        }
    }

    private void CaptureHomePose()
    {
        if (_captured)
            return;

        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
            return;

        _homePosition = _camera.transform.position;
        _homeRotation = _camera.transform.rotation;
        _homeFov = _camera.fieldOfView;
        _targetPosition = _homePosition;
        _targetRotation = _homeRotation;
        _targetFov = _homeFov;
        _captured = true;
    }

    private Bounds CalculateModelBounds()
    {
        var renderers = _modelRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return new Bounds(_modelRoot.position, Vector3.one * 2f);

        var initialized = false;
        var bounds = new Bounds(_modelRoot.position, Vector3.zero);
        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return initialized ? bounds : new Bounds(_modelRoot.position, Vector3.one * 2f);
    }
}
