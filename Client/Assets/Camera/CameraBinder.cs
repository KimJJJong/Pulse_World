using UnityEngine;

public class CameraBinder : MonoBehaviour
{
    public static CameraBinder Instance { get; private set; }
    public CameraFollow Follow;

    void Awake()
    {
        Instance = this;
        // 1. Try assigned
        if (Follow != null) return;

        // 2. Try FindFirstObjectByType (Better for Additive scenes where MainCamera might be ambiguous)
        Follow = FindFirstObjectByType<CameraFollow>();

        // 3. Fallback to Camera.main
        if (Follow == null && Camera.main != null)
        {
             Follow = Camera.main.GetComponent<CameraFollow>();
        }
    }

    public void Bind(Transform target)
    {
        // 늦게 로드되거나, 레퍼런스가 깨졌을 경우 재탐색
        if (Follow == null)
            Follow = FindFirstObjectByType<CameraFollow>();

        if (Follow != null)
        {
            Follow.target = target;
        }
        else
        {
            Debug.LogWarning("[CameraBinder] CameraFollow not found!");
        }
    }
}
