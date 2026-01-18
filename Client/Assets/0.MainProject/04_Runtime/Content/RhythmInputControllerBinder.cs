using UnityEngine;

public class RhythmInputControllerBinder : MonoBehaviour
{
    public static RhythmInputControllerBinder Instance { get; private set; }

    public RhythmInputController Controller;

    private void Awake()
    {


        Instance = this;

        if (Controller == null)
            Controller = FindFirstObjectByType<RhythmInputController>();

    }

    /// <summary>
    /// 현재 조종 대상(보통 플레이어 Actor)을 바인딩
    /// </summary>
    public void Bind(GameObject owner)
    {
        if (Controller == null || owner == null)
            return;

        Controller.targetObject = owner;

    }

    /// <summary>
    /// 입력 비활성화(씬 전환, 로딩, 관전 등)
    /// </summary>
    public void Unbind()
    {
        if (Controller == null)
            return;

        Controller.transform.SetParent(null);
    }
}
