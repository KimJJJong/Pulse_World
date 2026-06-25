using UnityEngine;

public class RhythmInputControllerBinder : MonoBehaviour
{
    public static RhythmInputControllerBinder Instance { get; private set; }

    public RhythmInputController Controller;

    private void Awake()
    {
        Instance = this;
        ResolveController();

        if (Controller == null)
            Debug.LogWarning("[RhythmInputBinder] RhythmInputController not found during Awake");

    }

    /// <summary>
    /// 현재 조종 대상(보통 플레이어 Actor)을 바인딩
    /// </summary>
    public void Bind(GameObject owner)
    {
        ResolveController();

        if (Controller == null || owner == null)
        {
            Debug.LogWarning($"[RhythmInputBinder] Bind skipped controller={(Controller != null)} owner={(owner != null)}");
            return;
        }

        Controller.targetObject = owner;
        Debug.Log($"[RhythmInputBinder] Bound target={owner.name} controllerState={Controller.GetDebugState()}");

    }

    /// <summary>
    /// 입력 비활성화(씬 전환, 로딩, 관전 등)
    /// </summary>
    public void Unbind()
    {
        ResolveController();

        if (Controller == null)
            return;

        Controller.targetObject = null;
        Controller.transform.SetParent(null);
        Debug.Log("[RhythmInputBinder] Unbound target");
    }

    private void ResolveController()
    {
        if (Controller != null)
            return;

        Controller = RhythmInputController.Instance;
        if (Controller == null)
            Controller = FindFirstObjectByType<RhythmInputController>();
    }
}
