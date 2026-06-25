using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class HeadphonesRecommendedSceneBuilder
{
    private const string ScenePath = "Assets/0.MainProject/Scenes/HeadphonesRecommended.unity";
    private const string MenuPath = "Tools/RhythmRPG/Build Headphones Recommended Scene";

    [MenuItem(MenuPath)]
    public static void Build()
    {
        string directory = Path.GetDirectoryName(ScenePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        Scene scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        ClearScene();
        CreateCamera();
        CreateEventSystem();

        GameObject controllerObject = new GameObject("HeadphonesRecommendedSceneController");
        HeadphonesRecommendedSceneController controller = controllerObject.AddComponent<HeadphonesRecommendedSceneController>();
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("nextSceneName").stringValue = SceneNames.Login;
        serialized.FindProperty("startDelay").floatValue = 0.15f;
        serialized.FindProperty("fadeInDuration").floatValue = 0.7f;
        serialized.FindProperty("holdDuration").floatValue = 2.0f;
        serialized.FindProperty("fadeOutDuration").floatValue = 0.75f;
        serialized.FindProperty("continueThroughLoadingScene").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureSceneInBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[HeadphonesRecommendedSceneBuilder] Scene built and registered: " + ScenePath);
    }

    private static void ClearScene()
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject root in roots)
            Object.DestroyImmediate(root);
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.depth = -1f;
        cameraObject.tag = "MainCamera";
        cameraObject.AddComponent<AudioListener>();
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private static void EnsureSceneInBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path != ScenePath)
                scenes.Add(scene);
        }

        var entry = new EditorBuildSettingsScene(ScenePath, true);
        int insertIndex = FindSceneIndex(scenes, "Assets/0.MainProject/Scenes/Login.unity");
        if (insertIndex < 0 || insertIndex > scenes.Count)
            scenes.Add(entry);
        else
            scenes.Insert(insertIndex, entry);

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static int FindSceneIndex(List<EditorBuildSettingsScene> scenes, string path)
    {
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == path)
                return i;
        }

        return -1;
    }
}
