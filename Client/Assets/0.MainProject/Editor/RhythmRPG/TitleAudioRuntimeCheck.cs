#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

public static class TitleAudioRuntimeCheck
{
    private const string BootstrapScenePath = "Assets/0.MainProject/Scenes/Bootstrap.unity";

    [MenuItem("RhythmRPG/Editors/Audio/Start Title Audio Runtime Check")]
    public static void StartRuntimeCheck()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
    }

    [MenuItem("RhythmRPG/Editors/Audio/Stop Title Audio Runtime Check")]
    public static void StopRuntimeCheck()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
