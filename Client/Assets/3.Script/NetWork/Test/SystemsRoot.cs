using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
public sealed class SystemsRoot : MonoBehaviour
{
	void Awake()
	{
		if (transform.parent != null) transform.SetParent(null);
		if (gameObject.scene.name != "DontDestroyOnLoad")
			DontDestroyOnLoad(gameObject);
	}
	void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
	void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
	void OnSceneLoaded(Scene s, LoadSceneMode m)
	{
		if (gameObject.scene.name != "DontDestroyOnLoad")
			DontDestroyOnLoad(gameObject);
	}
}
