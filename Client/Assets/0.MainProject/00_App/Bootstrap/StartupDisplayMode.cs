using UnityEngine;

public static class StartupDisplayMode
{
    private const string FullscreenKey = "Options.Fullscreen";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
#if UNITY_STANDALONE && !UNITY_EDITOR
        PlayerPrefs.SetInt(FullscreenKey, 1);
        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;

        var resolution = Screen.currentResolution;
        if (resolution.width > 0 && resolution.height > 0)
            Screen.SetResolution(resolution.width, resolution.height, FullScreenMode.FullScreenWindow);

        Screen.fullScreen = true;
#endif
    }
}
