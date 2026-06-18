#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class BuildIconApplier
{
    private const string MenuRoot = "RhythmRPG/Editors/Setup";
    private const string IconAssetPath = "Assets/0.MainProject/05_Art/Generated/AppIcon/RhythmRPG_AppIcon.png";

    private static readonly NamedBuildTarget[] BuildTargets =
    {
        NamedBuildTarget.Unknown,
        NamedBuildTarget.Standalone,
        NamedBuildTarget.Android,
        NamedBuildTarget.iOS,
        NamedBuildTarget.WebGL,
        NamedBuildTarget.WindowsStoreApps
    };

    private static readonly IconKind[] IconKinds =
    {
        IconKind.Application,
        IconKind.Store,
        IconKind.Settings,
        IconKind.Spotlight,
        IconKind.Notification
    };

    [MenuItem(MenuRoot + "/Apply Generated Build Icon")]
    public static void ApplyGeneratedBuildIcon()
    {
        ConfigureTextureImporter();
        Texture2D icon = LoadIcon();

        int assignedSlots = 0;
        assignedSlots += ApplyIconKinds(NamedBuildTarget.Unknown, icon, true);

        for (int i = 1; i < BuildTargets.Length; i++)
            assignedSlots += ApplyIconKinds(BuildTargets[i], icon, false);

        if (assignedSlots <= 0)
            throw new InvalidOperationException("No PlayerSettings icon slots were assigned.");

        AssetDatabase.SaveAssets();
        Debug.Log(
            $"[BuildIconApplier] Applied build icon to {assignedSlots} PlayerSettings slots. " +
            $"Path: {IconAssetPath}, GUID: {AssetDatabase.AssetPathToGUID(IconAssetPath)}");
    }

    [MenuItem(MenuRoot + "/Verify Generated Build Icon")]
    public static void VerifyGeneratedBuildIcon()
    {
        Texture2D icon = LoadIcon();
        var failures = new List<string>();
        int verifiedSlots = 0;

        foreach (NamedBuildTarget target in BuildTargets)
        {
            foreach (IconKind kind in IconKinds)
            {
                int[] sizes = GetIconSizes(target, kind);
                if (sizes.Length == 0)
                    continue;

                Texture2D[] assigned = GetIcons(target, kind);
                if (assigned.Length != sizes.Length)
                {
                    failures.Add($"{target.TargetName}/{kind}: expected {sizes.Length}, got {assigned.Length}");
                    continue;
                }

                for (int i = 0; i < assigned.Length; i++)
                {
                    if (assigned[i] != icon)
                        failures.Add($"{target.TargetName}/{kind}[{i}] is not {IconAssetPath}");
                    else
                        verifiedSlots++;
                }
            }
        }

        if (failures.Count > 0)
            throw new InvalidOperationException(
                "[BuildIconApplier] Build icon verification failed:\n" + string.Join("\n", failures));

        Debug.Log($"[BuildIconApplier] Build icon verification passed for {verifiedSlots} PlayerSettings slots.");
    }

    private static Texture2D LoadIcon()
    {
        AssetDatabase.ImportAsset(IconAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAssetPath);
        if (icon == null)
            throw new InvalidOperationException($"Build icon texture is missing or failed to import: {IconAssetPath}");

        return icon;
    }

    private static void ConfigureTextureImporter()
    {
        AssetDatabase.ImportAsset(IconAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(IconAssetPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"TextureImporter not found for build icon: {IconAssetPath}");

        bool changed = false;
        if (importer.textureType != TextureImporterType.Default)
        {
            importer.textureType = TextureImporterType.Default;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (importer.alphaSource != TextureImporterAlphaSource.None)
        {
            importer.alphaSource = TextureImporterAlphaSource.None;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (importer.maxTextureSize < 1024)
        {
            importer.maxTextureSize = 2048;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
    }

    private static int ApplyIconKinds(NamedBuildTarget target, Texture2D icon, bool useSingleApplicationFallback)
    {
        int assigned = 0;

        foreach (IconKind kind in IconKinds)
        {
            int[] sizes = GetIconSizes(target, kind);
            if (sizes.Length == 0)
            {
                if (useSingleApplicationFallback && kind == IconKind.Application)
                {
                    PlayerSettings.SetIcons(target, new[] { icon }, kind);
                    assigned++;
                }

                continue;
            }

            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < icons.Length; i++)
                icons[i] = icon;

            PlayerSettings.SetIcons(target, icons, kind);
            assigned += icons.Length;
        }

        return assigned;
    }

    private static int[] GetIconSizes(NamedBuildTarget target, IconKind kind)
    {
        try
        {
            return PlayerSettings.GetIconSizes(target, kind) ?? Array.Empty<int>();
        }
        catch (Exception)
        {
            return Array.Empty<int>();
        }
    }

    private static Texture2D[] GetIcons(NamedBuildTarget target, IconKind kind)
    {
        try
        {
            return PlayerSettings.GetIcons(target, kind) ?? Array.Empty<Texture2D>();
        }
        catch (Exception)
        {
            return Array.Empty<Texture2D>();
        }
    }
}
#endif
