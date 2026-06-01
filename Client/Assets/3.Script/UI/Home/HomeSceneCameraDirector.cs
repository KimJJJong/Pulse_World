using UnityEngine;

public sealed class HomeSceneCameraDirector : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private Transform _modelRoot;
    [SerializeField] private float _blendSpeed = 4.5f;
    [SerializeField] private float _modelScreenLeftOffset = 1.65f;
    [SerializeField] private float _appearanceDistance = 8f;
    [SerializeField] private float _appearanceHeightOffset = 1.1f;
    [SerializeField] private float _presentationCameraHeightOffset;
    [SerializeField] private bool _useModelFacingForPresentation;
    [SerializeField] private bool _invertModelFacingForPresentation = true;
    [SerializeField] private bool _useCurrentCameraOppositeForPresentation;
    [SerializeField] private bool _useStagedPresentationEnter;
    [SerializeField] private float _entryApproachDuration = 0.32f;
    [SerializeField] private float _entryRotateDuration = 1.05f;
    [SerializeField] private float _entryApproachDistanceMultiplier = 0.78f;

    private Vector3 _homePosition;
    private Quaternion _homeRotation;
    private float _homeFov;
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private float _targetFov;
    private bool _captured;
    private bool _presentationActive;
    private bool _entryTransitionActive;
    private float _entryElapsed;
    private Vector3 _entryLookAt;
    private Vector3 _entryOrbitCenter;
    private Vector3 _entryStartPosition;
    private Vector3 _entryApproachPosition;
    private Vector3 _entryFinalPosition;
    private Quaternion _entryStartRotation;
    private Quaternion _entryApproachRotation;
    private Quaternion _entryFinalRotation;
    private float _entryStartFov;
    private float _entryApproachFov;
    private float _entryFinalFov;
    private bool _entryIsExit;

    private void Awake()
    {
        CaptureHomePose();
        if (enabled)
            SetAppearancePresentation(false, immediate: true);
    }

    private void LateUpdate()
    {
        if (_camera == null)
            return;

        if (_entryTransitionActive)
        {
            UpdateStagedPresentationEnter();
            return;
        }

        var blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, _blendSpeed) * Time.unscaledDeltaTime);
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, _targetPosition, blend);
        _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, _targetRotation, blend);
        _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _targetFov, blend);
    }

    public void SetAppearancePresentation(bool active)
    {
        SetAppearancePresentation(active, immediate: false);
    }

    public void CaptureCurrentPose()
    {
        _captured = false;
        CaptureHomePose();
    }

    public void SetModelRoot(Transform modelRoot)
    {
        _modelRoot = modelRoot;
    }

    public void SetModelScreenLeftOffset(float offset)
    {
        _modelScreenLeftOffset = offset;
    }

    public void SetUseModelFacingForPresentation(bool useModelFacing)
    {
        _useModelFacingForPresentation = useModelFacing;
    }

    public void SetInvertModelFacingForPresentation(bool invertModelFacing)
    {
        _invertModelFacingForPresentation = invertModelFacing;
    }

    public void SetUseCurrentCameraOppositeForPresentation(bool useCurrentCameraOpposite)
    {
        _useCurrentCameraOppositeForPresentation = useCurrentCameraOpposite;
    }

    public float GetStagedTransitionDuration()
    {
        if (!_useStagedPresentationEnter)
            return 0f;

        return Mathf.Max(0.01f, _entryApproachDuration) + Mathf.Max(0.01f, _entryRotateDuration);
    }

    public void SetAppearancePresentation(bool active, bool immediate)
    {
        CaptureHomePose();
        var wasPresentationActive = _presentationActive;
        _presentationActive = active;

        if (active && _camera != null && _modelRoot != null)
        {
            CalculatePresentationTarget(out var lookAt, out var orbitCenter, out var distance);

            if (!wasPresentationActive && !immediate && _useStagedPresentationEnter)
            {
                StartStagedPresentationEnter(lookAt, orbitCenter, distance);
            }
            else if (_entryTransitionActive && HasEntryFinalTargetChanged())
            {
                _entryTransitionActive = false;
            }
        }
        else
        {
            var canStageExit = wasPresentationActive &&
                               !immediate &&
                               _useStagedPresentationEnter &&
                               _camera != null &&
                               _modelRoot != null;

            Vector3 exitLookAt = default;
            float exitDistance = 0f;
            Vector3 exitOrbitCenter = default;
            if (canStageExit)
                CalculatePresentationTarget(out exitLookAt, out exitOrbitCenter, out exitDistance);

            _targetPosition = _homePosition;
            _targetRotation = _homeRotation;
            _targetFov = _homeFov;

            if (canStageExit)
                StartStagedPresentationExit(exitLookAt, exitOrbitCenter, exitDistance);
            else
                _entryTransitionActive = false;
        }

        if (immediate && _camera != null)
        {
            _entryTransitionActive = false;
            _camera.transform.SetPositionAndRotation(_targetPosition, _targetRotation);
            _camera.fieldOfView = _targetFov;
        }
    }

    private void CalculatePresentationTarget(out Vector3 lookAt, out Vector3 orbitCenter, out float distance)
    {
        var bounds = CalculateModelBounds();
        orbitCenter = bounds.center + Vector3.up * _appearanceHeightOffset;
        var presentationForward = ResolvePresentationForward(bounds.center);
        var presentationRotation = Quaternion.LookRotation(presentationForward, Vector3.up);
        var presentationRight = presentationRotation * Vector3.right;
        lookAt = orbitCenter + presentationRight * _modelScreenLeftOffset;
        distance = Mathf.Max(_appearanceDistance, bounds.extents.magnitude * 2.6f);

        _targetPosition = orbitCenter - presentationForward * distance + Vector3.up * _presentationCameraHeightOffset;
        _targetRotation = SafeLookRotation(lookAt - _targetPosition, _homeRotation);
        _targetFov = Mathf.Min(_homeFov, 48f);
    }

    private void StartStagedPresentationEnter(Vector3 lookAt, Vector3 orbitCenter, float finalDistance)
    {
        if (_camera == null)
            return;

        _entryLookAt = lookAt;
        _entryOrbitCenter = orbitCenter;
        _entryStartPosition = _camera.transform.position;
        _entryStartRotation = _camera.transform.rotation;
        _entryStartFov = _camera.fieldOfView;
        _entryFinalPosition = _targetPosition;
        _entryFinalRotation = _targetRotation;
        _entryFinalFov = _targetFov;

        var fromOrbitCenterToCamera = _entryStartPosition - orbitCenter;
        var approachDirection = Vector3.ProjectOnPlane(fromOrbitCenterToCamera, Vector3.up);
        if (approachDirection.sqrMagnitude <= 0.001f)
            approachDirection = Vector3.ProjectOnPlane(_entryFinalPosition - orbitCenter, Vector3.up);
        if (approachDirection.sqrMagnitude <= 0.001f)
            approachDirection = Vector3.ProjectOnPlane(-_camera.transform.forward, Vector3.up);
        if (approachDirection.sqrMagnitude <= 0.001f)
            approachDirection = Vector3.back;

        approachDirection.Normalize();

        var currentHorizontalDistance = Vector3.ProjectOnPlane(fromOrbitCenterToCamera, Vector3.up).magnitude;
        var approachDistance = Mathf.Max(2.2f, finalDistance * Mathf.Clamp(_entryApproachDistanceMultiplier, 0.35f, 0.98f));
        if (currentHorizontalDistance > 0.01f)
            approachDistance = Mathf.Min(approachDistance, Mathf.Max(2.2f, currentHorizontalDistance * 0.88f));

        _entryApproachPosition = orbitCenter + approachDirection * approachDistance;
        _entryApproachPosition.y = _entryFinalPosition.y;
        _entryApproachRotation = SafeLookRotation(orbitCenter - _entryApproachPosition, _entryStartRotation);
        _entryApproachFov = Mathf.Lerp(_entryStartFov, _entryFinalFov, 0.72f);
        _entryElapsed = 0f;
        _entryIsExit = false;
        _entryTransitionActive = true;
    }

    private void StartStagedPresentationExit(Vector3 lookAt, Vector3 orbitCenter, float finalDistance)
    {
        if (_camera == null)
            return;

        _entryLookAt = lookAt;
        _entryOrbitCenter = orbitCenter;
        _entryStartPosition = _camera.transform.position;
        _entryStartRotation = _camera.transform.rotation;
        _entryStartFov = _camera.fieldOfView;
        _entryFinalPosition = _targetPosition;
        _entryFinalRotation = _targetRotation;
        _entryFinalFov = _targetFov;

        var fromOrbitCenterToHome = _homePosition - orbitCenter;
        var approachDirection = Vector3.ProjectOnPlane(fromOrbitCenterToHome, Vector3.up);
        if (approachDirection.sqrMagnitude <= 0.001f)
            approachDirection = Vector3.ProjectOnPlane(_entryStartPosition - orbitCenter, Vector3.up);
        if (approachDirection.sqrMagnitude <= 0.001f)
            approachDirection = Vector3.ProjectOnPlane(-_camera.transform.forward, Vector3.up);
        if (approachDirection.sqrMagnitude <= 0.001f)
            approachDirection = Vector3.back;

        approachDirection.Normalize();

        var homeHorizontalDistance = Vector3.ProjectOnPlane(fromOrbitCenterToHome, Vector3.up).magnitude;
        var approachDistance = Mathf.Max(2.2f, finalDistance * Mathf.Clamp(_entryApproachDistanceMultiplier, 0.35f, 0.98f));
        if (homeHorizontalDistance > 0.01f)
            approachDistance = Mathf.Min(approachDistance, Mathf.Max(2.2f, homeHorizontalDistance * 0.88f));

        _entryApproachPosition = orbitCenter + approachDirection * approachDistance;
        _entryApproachPosition.y = _entryStartPosition.y;
        _entryApproachRotation = SafeLookRotation(orbitCenter - _entryApproachPosition, _entryStartRotation);
        _entryApproachFov = Mathf.Lerp(_entryStartFov, _entryFinalFov, 0.28f);
        _entryElapsed = 0f;
        _entryIsExit = true;
        _entryTransitionActive = true;
    }

    private void UpdateStagedPresentationEnter()
    {
        var approachDuration = Mathf.Max(0.01f, _entryApproachDuration);
        var rotateDuration = Mathf.Max(0.01f, _entryRotateDuration);

        _entryElapsed += Time.unscaledDeltaTime;
        if (_entryIsExit)
        {
            UpdateStagedPresentationExit(approachDuration, rotateDuration);
            return;
        }

        if (_entryElapsed < approachDuration)
        {
            var t = Smooth01(_entryElapsed / approachDuration);
            var approachPosition = Vector3.Lerp(_entryStartPosition, _entryApproachPosition, t);
            _camera.transform.position = approachPosition;
            _camera.transform.rotation = SafeLookRotation(_entryOrbitCenter - approachPosition, _entryApproachRotation);
            _camera.fieldOfView = Mathf.Lerp(_entryStartFov, _entryApproachFov, t);
            return;
        }

        var rotateT = Smooth01((_entryElapsed - approachDuration) / rotateDuration);
        var position = BlendAroundLookAtHorizontal(_entryApproachPosition, _entryFinalPosition, _entryOrbitCenter, rotateT);

        _camera.transform.position = position;
        _camera.transform.rotation = SafeLookRotation(_entryLookAt - position, _entryFinalRotation);
        _camera.fieldOfView = Mathf.Lerp(_entryApproachFov, _entryFinalFov, rotateT);

        if (rotateT >= 1f)
        {
            _entryTransitionActive = false;
            _camera.transform.SetPositionAndRotation(_targetPosition, _targetRotation);
            _camera.fieldOfView = _targetFov;
        }
    }

    private void UpdateStagedPresentationExit(float approachDuration, float rotateDuration)
    {
        if (_entryElapsed < rotateDuration)
        {
            var t = Smooth01(_entryElapsed / rotateDuration);
            var position = BlendAroundLookAtHorizontal(_entryStartPosition, _entryApproachPosition, _entryOrbitCenter, t);
            _camera.transform.position = position;
            _camera.transform.rotation = SafeLookRotation(_entryOrbitCenter - position, _entryApproachRotation);
            _camera.fieldOfView = Mathf.Lerp(_entryStartFov, _entryApproachFov, t);
            return;
        }

        var pullT = Smooth01((_entryElapsed - rotateDuration) / approachDuration);
        _camera.transform.position = Vector3.Lerp(_entryApproachPosition, _entryFinalPosition, pullT);
        _camera.transform.rotation = Quaternion.Slerp(_entryApproachRotation, _entryFinalRotation, pullT);
        _camera.fieldOfView = Mathf.Lerp(_entryApproachFov, _entryFinalFov, pullT);

        if (pullT >= 1f)
        {
            _entryTransitionActive = false;
            _camera.transform.SetPositionAndRotation(_targetPosition, _targetRotation);
            _camera.fieldOfView = _targetFov;
        }
    }

    private bool HasEntryFinalTargetChanged()
    {
        return Vector3.Distance(_entryFinalPosition, _targetPosition) > 0.05f ||
               Quaternion.Angle(_entryFinalRotation, _targetRotation) > 2f ||
               Mathf.Abs(_entryFinalFov - _targetFov) > 0.1f;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static Quaternion SafeLookRotation(Vector3 forward, Quaternion fallback)
    {
        if (forward.sqrMagnitude <= 0.001f)
            return fallback;

        return Quaternion.LookRotation(forward, Vector3.up);
    }

    private static Vector3 BlendAroundLookAtHorizontal(Vector3 fromPosition, Vector3 toPosition, Vector3 orbitCenter, float t)
    {
        var from = fromPosition - orbitCenter;
        var to = toPosition - orbitCenter;
        var fromHorizontal = new Vector3(from.x, 0f, from.z);
        var toHorizontal = new Vector3(to.x, 0f, to.z);

        if (fromHorizontal.sqrMagnitude <= 0.001f || toHorizontal.sqrMagnitude <= 0.001f)
            return Vector3.Lerp(fromPosition, toPosition, t);

        var signedAngle = Vector3.SignedAngle(fromHorizontal, toHorizontal, Vector3.up);
        var direction = Quaternion.AngleAxis(signedAngle * t, Vector3.up) * fromHorizontal.normalized;
        var distance = Mathf.Lerp(fromHorizontal.magnitude, toHorizontal.magnitude, t);
        var y = Mathf.Lerp(fromPosition.y, toPosition.y, t);
        return new Vector3(
            orbitCenter.x + direction.x * distance,
            y,
            orbitCenter.z + direction.z * distance);
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

    private Vector3 ResolvePresentationForward(Vector3 modelCenter)
    {
        if (_useCurrentCameraOppositeForPresentation && _camera != null)
        {
            var fromModelToHomeCamera = Vector3.ProjectOnPlane(_homePosition - modelCenter, Vector3.up);
            if (fromModelToHomeCamera.sqrMagnitude > 0.001f)
                return fromModelToHomeCamera.normalized;
        }

        if (_useModelFacingForPresentation && _modelRoot != null)
        {
            var forward = Vector3.ProjectOnPlane(_modelRoot.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.001f)
            {
                forward.Normalize();
                return _invertModelFacingForPresentation ? -forward : forward;
            }
        }

        return _homeRotation * Vector3.forward;
    }
}
