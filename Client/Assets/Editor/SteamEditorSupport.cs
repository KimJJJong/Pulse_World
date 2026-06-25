#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class SteamEditorSupport
{
    private const string FacepunchDefine = "RHYTHM_USE_FACEPUNCH_STEAMWORKS";
    private static readonly string FacepunchDllPath = Path.Combine(Application.dataPath, "Plugins", "Facepunch.Steamworks", "Facepunch.Steamworks.Win64.dll");

    static SteamEditorSupport()
    {
        EditorApplication.delayCall += SyncFacepunchDefineAndAppIdFile;
    }

    [MenuItem("Tools/Steam/Sync Facepunch Support")]
    public static void SyncFacepunchDefineAndAppIdFile()
    {
        SyncStandaloneDefine();
        EnsureProjectSteamAppIdFile();
    }

    private static void SyncStandaloneDefine()
    {
        bool hasFacepunch = File.Exists(FacepunchDllPath);
#if UNITY_2021_2_OR_NEWER
        var namedTarget = NamedBuildTarget.Standalone;
        var raw = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
        var symbols = raw.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
#else
        var raw = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
        var symbols = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
#endif

        bool contains = symbols.Contains(FacepunchDefine, StringComparer.Ordinal);
        if (hasFacepunch && !contains)
            symbols.Add(FacepunchDefine);
        else if (!hasFacepunch && contains)
            symbols.RemoveAll(x => string.Equals(x, FacepunchDefine, StringComparison.Ordinal));
        else
            return;

        var joined = string.Join(";", symbols.Distinct(StringComparer.Ordinal));
#if UNITY_2021_2_OR_NEWER
        PlayerSettings.SetScriptingDefineSymbols(namedTarget, joined);
#else
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, joined);
#endif
        Debug.Log($"[SteamEditorSupport] Updated Standalone scripting defines: {joined}");
    }

    private static void EnsureProjectSteamAppIdFile()
    {
        var config = Resources.Load<AppConfig>("AppConfig");
        if (config == null || string.IsNullOrWhiteSpace(config.SteamAppId))
            return;

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? "";
        if (string.IsNullOrWhiteSpace(projectRoot))
            return;

        string path = Path.Combine(projectRoot, "steam_appid.txt");
        WriteIfDifferent(path, config.SteamAppId.Trim() + Environment.NewLine);
    }

    internal static void CopySteamAppIdToBuild(string outputPath)
    {
        var config = Resources.Load<AppConfig>("AppConfig");
        if (config == null || string.IsNullOrWhiteSpace(config.SteamAppId))
            return;

        string targetDirectory = Directory.Exists(outputPath)
            ? outputPath
            : Path.GetDirectoryName(outputPath) ?? "";

        if (string.IsNullOrWhiteSpace(targetDirectory))
            return;

        string path = Path.Combine(targetDirectory, "steam_appid.txt");
        WriteIfDifferent(path, config.SteamAppId.Trim() + Environment.NewLine);
        Debug.Log($"[SteamEditorSupport] Wrote steam_appid.txt to {path}");
    }

    private static void WriteIfDifferent(string path, string content)
    {
        if (File.Exists(path) && string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal))
            return;

        File.WriteAllText(path, content);
    }
}

public sealed class SteamAppIdPostBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        SteamEditorSupport.CopySteamAppIdToBuild(report.summary.outputPath);
    }
}
#endif
