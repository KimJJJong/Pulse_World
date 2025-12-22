using UnityEngine;

public class CameraBinder : MonoBehaviour
{
    public static CameraBinder Instance { get; private set; }
    public CameraFollow Follow;

    void Awake()
    {
        Instance = this;
        if (Follow == null) Follow = Camera.main.GetComponent<CameraFollow>();
    }

    public void Bind(Transform target)
    {
        if (Follow == null) return;
        Follow.target = target;
    }
}
