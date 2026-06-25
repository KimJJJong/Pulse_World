using System;
using System.IO;
using GameServer.InGame.Director.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StageClearUiAssetBuilder
{
    private const string MenuPath = "Tools/RhythmRPG/UI/Rebuild Stage Clear UI Assets";
    private const string PrefabMenuPath = "Tools/RhythmRPG/UI/Create Stage Clear Result UI Prefab";
    private const string PreviewMenuPath = "Tools/RhythmRPG/UI/Preview Stage Clear Result UI";
    private const string RenderPreviewMenuPath = "Tools/RhythmRPG/UI/Render Stage Clear Result UI Preview PNG";
    private const string ClearPreviewMenuPath = "Tools/RhythmRPG/UI/Clear Stage Clear Result UI Preview";
    private const string AssetRoot = "Assets/Resources/UI/UI_StageClear";
    private const string PrefabAssetPath = AssetRoot + "/StageClearResultHud.prefab";
    private const string ReferenceAssetRoot = "Assets/3.Script/Editor/StageClearReferenceAssets";
    private const string ReferenceButtonsPath = ReferenceAssetRoot + "/StageClear_Buttons_Source.png";
    private const string ReferenceIconsPath = ReferenceAssetRoot + "/StageClear_Icons_Source.png";
    private const string ReferencePanelPath = ReferenceAssetRoot + "/StageClear_Panel_Source.png";
    private const string PreviewScreenshotPath = "Assets/Screenshots/StageClear_UI_UnityRender.png";

    private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
    private static readonly Color Cyan = new Color(0.28f, 0.95f, 1f, 1f);
    private static readonly Color CyanSoft = new Color(0.12f, 0.58f, 0.70f, 0.72f);
    private static readonly Color Silver = new Color(0.72f, 0.86f, 0.92f, 0.82f);
    private static readonly Color Panel = new Color(0.02f, 0.075f, 0.105f, 0.86f);
    private static readonly Color Gold = new Color(0.95f, 0.64f, 0.25f, 1f);
    private static readonly Color Red = new Color(0.94f, 0.24f, 0.20f, 1f);

    [MenuItem(MenuPath)]
    public static void Rebuild()
    {
        if (TryRebuildFromReference())
            return;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        Directory.CreateDirectory(Path.Combine(projectRoot, AssetRoot));

        WriteSprite("Panel_Frame", 720, 400, new Vector4(42f, 42f, 42f, 42f), DrawPanelFrame);
        WriteSprite("Button_Return", 560, 82, new Vector4(54f, 18f, 54f, 18f), t => DrawButton(t, Gold));
        WriteSprite("Button_Continue", 560, 82, new Vector4(54f, 18f, 54f, 18f), t => DrawButton(t, Cyan));
        WriteSprite("Hex_Slot", 128, 128, Vector4.zero, t => DrawHexIcon(t, null, Cyan));
        WriteSprite("Rank_Badge", 180, 180, Vector4.zero, DrawRankBadge);
        WriteSprite("Line_Cyan", 256, 8, new Vector4(16f, 0f, 16f, 0f), DrawLineSprite);

        WriteSprite("Icon_Emblem", 128, 150, Vector4.zero, DrawCrystalEmblem);
        WriteSprite("Icon_Hourglass", 96, 96, Vector4.zero, t => DrawHexIcon(t, DrawHourglass, Cyan));
        WriteSprite("Icon_Rhythm", 96, 96, Vector4.zero, t => DrawHexIcon(t, DrawRhythm, Cyan));
        WriteSprite("Icon_Combo", 96, 96, Vector4.zero, t => DrawHexIcon(t, DrawCombo, Cyan));
        WriteSprite("Icon_Miss", 96, 96, Vector4.zero, t => DrawHexIcon(t, DrawMiss, Red));
        WriteSprite("Icon_NextGate", 96, 96, Vector4.zero, DrawGate);
        WriteSprite("Icon_Level", 96, 96, Vector4.zero, DrawLevel);
        WriteSprite("Icon_Home", 96, 96, Vector4.zero, DrawHome);
        WriteSprite("Icon_Arrow", 96, 96, Vector4.zero, DrawArrow);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[StageClearUiAssetBuilder] Rebuilt Stage Clear UI sprite resources.");
    }

    [MenuItem(PreviewMenuPath)]
    public static void Preview()
    {
        Debug.Log("[StageClearUiAssetBuilder] Creating Stage Clear Result UI preview.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("PreviewCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.002f, 0.011f, 0.017f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        cameraObject.tag = "MainCamera";

        StageClearResultHud.Show(CreatePreviewData());

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem(PrefabMenuPath)]
    public static void CreateOrUpdatePrefab()
    {
        Rebuild();

        var eventSystemBefore = UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        StageClearResultHud hud = null;
        try
        {
            hud = StageClearResultHud.CreateEditorPrefabSource(CreatePreviewData());
            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, PrefabAssetPath);
            Debug.Log($"[StageClearUiAssetBuilder] Saved Stage Clear UI prefab: {PrefabAssetPath}");
        }
        finally
        {
            if (hud != null)
                UnityEngine.Object.DestroyImmediate(hud.gameObject);
            StageClearResultHud.DestroyEditorPreview();

            var eventSystemAfter = UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystemBefore == null && eventSystemAfter != null)
                UnityEngine.Object.DestroyImmediate(eventSystemAfter.gameObject);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    [MenuItem(RenderPreviewMenuPath)]
    public static void RenderPreviewPng()
    {
        Preview();
        Canvas.ForceUpdateCanvases();

        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("[StageClearUiAssetBuilder] Preview render failed: MainCamera not found.");
            return;
        }

        const int width = 1920;
        const int height = 1080;
        var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        var previewHud = UnityEngine.Object.FindFirstObjectByType<StageClearResultHud>();
        Canvas previewCanvas = previewHud != null ? previewHud.GetComponent<Canvas>() : null;
        RenderMode previousCanvasRenderMode = RenderMode.ScreenSpaceOverlay;
        Camera previousCanvasCamera = null;
        float previousCanvasPlaneDistance = 0f;
        bool previousCanvasOverrideSorting = false;
        int previousCanvasSortingOrder = 0;

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;

        try
        {
            if (previewCanvas != null)
            {
                previousCanvasRenderMode = previewCanvas.renderMode;
                previousCanvasCamera = previewCanvas.worldCamera;
                previousCanvasPlaneDistance = previewCanvas.planeDistance;
                previousCanvasOverrideSorting = previewCanvas.overrideSorting;
                previousCanvasSortingOrder = previewCanvas.sortingOrder;

                previewCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                previewCanvas.worldCamera = camera;
                previewCanvas.planeDistance = 1f;
                previewCanvas.overrideSorting = true;
                previewCanvas.sortingOrder = 32000;
                Canvas.ForceUpdateCanvases();
            }

            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            camera.Render();
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply();

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fullPath = Path.Combine(projectRoot, PreviewScreenshotPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(PreviewScreenshotPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[StageClearUiAssetBuilder] Rendered Stage Clear UI preview: {PreviewScreenshotPath}");
        }
        finally
        {
            if (previewCanvas != null)
            {
                previewCanvas.renderMode = previousCanvasRenderMode;
                previewCanvas.worldCamera = previousCanvasCamera;
                previewCanvas.planeDistance = previousCanvasPlaneDistance;
                previewCanvas.overrideSorting = previousCanvasOverrideSorting;
                previewCanvas.sortingOrder = previousCanvasSortingOrder;
            }

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }
    }

    [MenuItem(ClearPreviewMenuPath)]
    public static void ClearPreview()
    {
        StageClearResultHud.DestroyEditorPreview();
        var preview = GameObject.Find("__StageClearResultPreviewBootstrap");
        if (preview != null)
            UnityEngine.Object.DestroyImmediate(preview);

        foreach (var hud in UnityEngine.Object.FindObjectsByType<StageClearResultHud>(FindObjectsSortMode.None))
        {
            if (hud != null)
                UnityEngine.Object.DestroyImmediate(hud.gameObject);
        }

        foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (transform != null && transform.parent == null && transform.name == nameof(StageClearResultHud))
                UnityEngine.Object.DestroyImmediate(transform.gameObject);
        }
    }

    private static StageClearResultData CreatePreviewData()
    {
        return new StageClearResultData
        {
            MapId = "Game_Forest_01",
            Title = "STAGE CLEAR",
            Subtitle = "Purification Complete - Deepwood Gate Stabilized",
            ClearTimeMs = 154000,
            RhythmSyncPercent = 98,
            MaxCombo = 842,
            Misses = 4,
            NextArea = "Deepwood Gate",
            RecommendedLevel = 12,
            DangerRhythm = "Normal"
        };
    }

    private static bool TryRebuildFromReference()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        if (!File.Exists(Path.Combine(projectRoot, ReferenceButtonsPath))
            || !File.Exists(Path.Combine(projectRoot, ReferenceIconsPath))
            || !File.Exists(Path.Combine(projectRoot, ReferencePanelPath)))
        {
            return false;
        }

        Directory.CreateDirectory(Path.Combine(projectRoot, AssetRoot));

        WriteReferenceSprite("Panel_Frame", ReferencePanelPath, new RectInt(15, 17, 1458, 1022), new Vector4(96f, 96f, 96f, 96f), 5);
        WriteReferenceSprite("Button_Return", ReferenceButtonsPath, new RectInt(342, 76, 950, 183), new Vector4(100f, 45f, 100f, 45f), 8, 8);
        WriteReferenceSprite("Button_Continue", ReferenceButtonsPath, new RectInt(350, 587, 961, 165), new Vector4(100f, 45f, 100f, 45f), 8, 8);

        WriteReferenceSprite("Icon_Hourglass", ReferenceIconsPath, new RectInt(147, 38, 233, 252), Vector4.zero, 8, 6);
        WriteReferenceSprite("Icon_Rhythm", ReferenceIconsPath, new RectInt(438, 38, 240, 252), Vector4.zero, 8, 6);
        WriteReferenceSprite("Icon_Combo", ReferenceIconsPath, new RectInt(736, 39, 238, 251), Vector4.zero, 8, 6);
        WriteReferenceSprite("Icon_Miss", ReferenceIconsPath, new RectInt(1033, 38, 241, 252), Vector4.zero, 8, 6);
        WriteReferenceSprite("Icon_NextGate", ReferenceIconsPath, new RectInt(288, 624, 214, 221), Vector4.zero, 8, 6);
        WriteReferenceSprite("Icon_Level", ReferenceIconsPath, new RectInt(636, 618, 156, 218), Vector4.zero, 8, 6);
        WriteReferenceSprite("Icon_Home", ReferenceIconsPath, new RectInt(489, 873, 162, 153), Vector4.zero, 8, 6);
        WriteReferenceSprite("Icon_Arrow", ReferenceIconsPath, new RectInt(779, 887, 185, 122), Vector4.zero, 8, 6);
        WriteReferenceSprite("Icon_Emblem", ReferenceIconsPath, new RectInt(927, 355, 181, 207), Vector4.zero, 8, 8);

        if (!File.Exists(Path.Combine(projectRoot, $"{AssetRoot}/Hex_Slot.png")))
            WriteSprite("Hex_Slot", 128, 128, Vector4.zero, t => DrawHexIcon(t, null, Cyan));
        if (!File.Exists(Path.Combine(projectRoot, $"{AssetRoot}/Rank_Badge.png")))
            WriteSprite("Rank_Badge", 180, 180, Vector4.zero, DrawRankBadge);
        if (!File.Exists(Path.Combine(projectRoot, $"{AssetRoot}/Line_Cyan.png")))
            WriteSprite("Line_Cyan", 256, 8, new Vector4(16f, 0f, 16f, 0f), DrawLineSprite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[StageClearUiAssetBuilder] Rebuilt Stage Clear UI sprite resources from reference sheets.");
        return true;
    }

    private static void WriteReferenceSprite(string name, string sourceAssetPath, RectInt topLeftCrop, Vector4 border, int transparentThreshold, int pad = 0)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string fullSourcePath = Path.Combine(projectRoot, sourceAssetPath);

        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        if (!source.LoadImage(File.ReadAllBytes(fullSourcePath)))
        {
            UnityEngine.Object.DestroyImmediate(source);
            Debug.LogError($"[StageClearUiAssetBuilder] Failed to read reference sheet: {sourceAssetPath}");
            return;
        }

        int left = Mathf.Clamp(topLeftCrop.x - pad, 0, source.width);
        int top = Mathf.Clamp(topLeftCrop.y - pad, 0, source.height);
        int right = Mathf.Clamp(topLeftCrop.x + topLeftCrop.width + pad, 0, source.width);
        int bottom = Mathf.Clamp(topLeftCrop.y + topLeftCrop.height + pad, 0, source.height);
        int width = Mathf.Max(1, right - left);
        int height = Mathf.Max(1, bottom - top);

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float threshold = Mathf.Clamp01(transparentThreshold / 255f);
        for (int y = 0; y < height; y++)
        {
            int sourceY = source.height - 1 - (top + y);
            int targetY = height - 1 - y;
            for (int x = 0; x < width; x++)
            {
                Color pixel = source.GetPixel(left + x, sourceY);
                if (pixel.a <= 0.03f || Mathf.Max(pixel.r, pixel.g, pixel.b) <= threshold)
                    pixel.a = 0f;
                texture.SetPixel(x, targetY, pixel);
            }
        }

        texture.Apply();

        string assetPath = $"{AssetRoot}/{name}.png";
        string fullPath = Path.Combine(projectRoot, assetPath);
        File.WriteAllBytes(fullPath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(source);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        ConfigureSpriteImporter(assetPath, border);
    }

    private static void WriteSprite(string name, int width, int height, Vector4 border, Action<Texture2D> draw)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Fill(texture, Clear);
        draw(texture);
        texture.Apply();

        string assetPath = $"{AssetRoot}/{name}.png";
        string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
        File.WriteAllBytes(fullPath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        ConfigureSpriteImporter(assetPath, border);
    }

    private static void ConfigureSpriteImporter(string assetPath, Vector4 border)
    {
        if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }
    }

    private static void DrawPanelFrame(Texture2D t)
    {
        Fill(t, Panel);
        AddNoise(t, 0.025f);

        int w = t.width;
        int h = t.height;
        var outer = CutCornerPoints(w, h, 28);
        DrawPolyline(t, outer, Silver, 2, true);
        DrawPolyline(t, CutCornerPoints(w - 22, h - 22, 19, 11, 11), CyanSoft, 2, true);
        DrawPolyline(t, CutCornerPoints(w - 42, h - 42, 14, 21, 21), new Color(0.13f, 0.73f, 0.82f, 0.34f), 1, true);

        for (int i = 0; i < 10; i++)
        {
            int x = 78 + i * 62;
            DrawRect(t, x, h - 19, 26, 2, CyanSoft);
            DrawRect(t, x + 15, 18, 42, 1, CyanSoft);
        }

        DrawDiamond(t, 40, h / 2, 6, Cyan);
        DrawDiamond(t, w - 41, h / 2, 6, Cyan);
        DrawDiamond(t, w / 2, h - 18, 5, Cyan);
        DrawDiamond(t, w / 2, 18, 5, Cyan);
    }

    private static void DrawButton(Texture2D t, Color accent)
    {
        Fill(t, new Color(0.025f, 0.03f, 0.035f, 0.86f));
        int w = t.width;
        int h = t.height;
        DrawPolyline(t, CutCornerPoints(w, h, 44), new Color(accent.r, accent.g, accent.b, 0.92f), 3, true);
        DrawPolyline(t, CutCornerPoints(w - 20, h - 20, 30, 10, 10), new Color(accent.r, accent.g, accent.b, 0.36f), 2, true);
        DrawRect(t, 96, h / 2 - 1, w - 180, 2, new Color(accent.r, accent.g, accent.b, 0.25f));
        DrawDiamond(t, 70, h / 2, 5, accent);
        DrawDiamond(t, w - 70, h / 2, 5, accent);
    }

    private static void DrawHexIcon(Texture2D t, Action<Texture2D, Color> glyph, Color accent)
    {
        Fill(t, Clear);
        Vector2[] hex = HexPoints(t.width / 2f, t.height / 2f, Mathf.Min(t.width, t.height) * 0.39f);
        FillPolygon(t, hex, new Color(0.015f, 0.055f, 0.075f, 0.72f));
        DrawPolyline(t, hex, Silver, 2, true);
        DrawPolyline(t, HexPoints(t.width / 2f, t.height / 2f, Mathf.Min(t.width, t.height) * 0.32f), new Color(accent.r, accent.g, accent.b, 0.34f), 1, true);
        glyph?.Invoke(t, accent);
    }

    private static void DrawRankBadge(Texture2D t)
    {
        DrawHexIcon(t, null, Cyan);
        Vector2 c = new Vector2(t.width / 2f, t.height / 2f);
        DrawDiamond(t, (int)c.x, t.height - 16, 22, Cyan);
        DrawDiamond(t, (int)c.x, 16, 18, Cyan);
        DrawDiamond(t, 17, (int)c.y, 14, Cyan);
        DrawDiamond(t, t.width - 18, (int)c.y, 14, Cyan);
        DrawCircle(t, (int)c.x, (int)c.y, 58, new Color(0.06f, 0.18f, 0.25f, 0.76f));
        DrawCircleOutline(t, (int)c.x, (int)c.y, 62, Cyan, 3);
    }

    private static void DrawLineSprite(Texture2D t)
    {
        Fill(t, Clear);
        int y = t.height / 2;
        DrawRect(t, 10, y, t.width - 20, 1, Cyan);
        DrawRect(t, 24, y - 1, t.width - 48, 3, new Color(0.28f, 0.95f, 1f, 0.18f));
        DrawDiamond(t, t.width / 2, y, 5, Cyan);
    }

    private static void DrawCrystalEmblem(Texture2D t)
    {
        Fill(t, Clear);
        Vector2[] rock =
        {
            new Vector2(63, 145), new Vector2(96, 132), new Vector2(116, 88), new Vector2(104, 27),
            new Vector2(74, 7), new Vector2(34, 18), new Vector2(12, 72), new Vector2(24, 124)
        };
        FillPolygon(t, rock, new Color(0.09f, 0.12f, 0.14f, 0.94f));
        DrawPolyline(t, rock, Silver, 2, true);
        DrawLine(t, 62, 31, 62, 118, Cyan, 3);
        DrawLine(t, 38, 74, 62, 98, Cyan, 3);
        DrawLine(t, 85, 72, 62, 98, Cyan, 3);
        DrawLine(t, 47, 50, 62, 67, Cyan, 2);
        DrawLine(t, 78, 48, 62, 67, Cyan, 2);
        DrawCircle(t, 62, 95, 5, Cyan);
    }

    private static void DrawHourglass(Texture2D t, Color color)
    {
        DrawLine(t, 30, 68, 66, 68, color, 4);
        DrawLine(t, 30, 28, 66, 28, color, 4);
        DrawLine(t, 34, 66, 48, 50, color, 4);
        DrawLine(t, 62, 66, 48, 50, color, 4);
        DrawLine(t, 34, 30, 48, 46, color, 4);
        DrawLine(t, 62, 30, 48, 46, color, 4);
    }

    private static void DrawRhythm(Texture2D t, Color color)
    {
        DrawLine(t, 26, 44, 32, 44, color, 3);
        DrawLine(t, 32, 44, 38, 61, color, 3);
        DrawLine(t, 38, 61, 45, 26, color, 3);
        DrawLine(t, 45, 26, 52, 72, color, 3);
        DrawLine(t, 52, 72, 59, 39, color, 3);
        DrawLine(t, 59, 39, 68, 39, color, 3);
    }

    private static void DrawCombo(Texture2D t, Color color)
    {
        DrawLine(t, 50, 22, 56, 43, color, 3);
        DrawLine(t, 56, 43, 78, 40, color, 3);
        DrawLine(t, 78, 40, 60, 54, color, 3);
        DrawLine(t, 60, 54, 67, 76, color, 3);
        DrawLine(t, 67, 76, 48, 61, color, 3);
        DrawLine(t, 48, 61, 30, 76, color, 3);
        DrawLine(t, 30, 76, 37, 54, color, 3);
        DrawLine(t, 37, 54, 19, 40, color, 3);
        DrawLine(t, 19, 40, 42, 43, color, 3);
        DrawLine(t, 42, 43, 50, 22, color, 3);
    }

    private static void DrawMiss(Texture2D t, Color color)
    {
        DrawLine(t, 32, 70, 68, 28, color, 4);
        DrawLine(t, 68, 70, 32, 28, color, 4);
        DrawLine(t, 26, 26, 36, 36, color, 2);
        DrawLine(t, 70, 26, 60, 36, color, 2);
    }

    private static void DrawGate(Texture2D t)
    {
        Fill(t, Clear);
        DrawRect(t, 25, 18, 12, 56, Silver);
        DrawRect(t, 59, 18, 12, 56, Silver);
        DrawLine(t, 31, 74, 48, 88, Silver, 4);
        DrawLine(t, 48, 88, 65, 74, Silver, 4);
        DrawRect(t, 36, 18, 24, 44, new Color(0.04f, 0.18f, 0.22f, 0.8f));
        DrawLine(t, 48, 20, 48, 62, Cyan, 3);
        DrawCircleOutline(t, 48, 45, 21, Cyan, 2);
    }

    private static void DrawLevel(Texture2D t)
    {
        Fill(t, Clear);
        DrawLine(t, 48, 18, 48, 76, Silver, 4);
        DrawLine(t, 48, 76, 28, 52, Cyan, 4);
        DrawLine(t, 48, 76, 68, 52, Cyan, 4);
        DrawLine(t, 36, 34, 48, 18, Silver, 3);
        DrawLine(t, 60, 34, 48, 18, Silver, 3);
        DrawDiamond(t, 48, 48, 11, Cyan);
    }

    private static void DrawHome(Texture2D t)
    {
        Fill(t, Clear);
        DrawLine(t, 20, 48, 48, 72, Gold, 5);
        DrawLine(t, 48, 72, 76, 48, Gold, 5);
        DrawRect(t, 28, 22, 40, 34, Gold);
        DrawRect(t, 41, 22, 14, 22, new Color(0.03f, 0.03f, 0.03f, 0.8f));
        DrawRect(t, 31, 39, 10, 10, new Color(0.03f, 0.03f, 0.03f, 0.8f));
        DrawRect(t, 57, 39, 10, 10, new Color(0.03f, 0.03f, 0.03f, 0.8f));
    }

    private static void DrawArrow(Texture2D t)
    {
        Fill(t, Clear);
        DrawLine(t, 18, 48, 66, 48, Cyan, 9);
        DrawLine(t, 62, 72, 82, 48, Cyan, 9);
        DrawLine(t, 62, 24, 82, 48, Cyan, 9);
        DrawLine(t, 19, 38, 48, 38, new Color(0.28f, 0.95f, 1f, 0.22f), 5);
        DrawLine(t, 19, 58, 48, 58, new Color(0.28f, 0.95f, 1f, 0.22f), 5);
    }

    private static void Fill(Texture2D t, Color color)
    {
        Color[] pixels = new Color[t.width * t.height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
        t.SetPixels(pixels);
    }

    private static void AddNoise(Texture2D t, float amount)
    {
        for (int y = 0; y < t.height; y++)
        {
            for (int x = 0; x < t.width; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.045f, y * 0.045f) * amount;
                Color c = t.GetPixel(x, y);
                t.SetPixel(x, y, new Color(c.r + n, c.g + n, c.b + n, c.a));
            }
        }
    }

    private static Vector2[] CutCornerPoints(int w, int h, int cut)
        => CutCornerPoints(w, h, cut, 0, 0);

    private static Vector2[] CutCornerPoints(int w, int h, int cut, int ox, int oy)
    {
        return new[]
        {
            new Vector2(ox + cut, oy),
            new Vector2(ox + w - cut, oy),
            new Vector2(ox + w, oy + cut),
            new Vector2(ox + w, oy + h - cut),
            new Vector2(ox + w - cut, oy + h),
            new Vector2(ox + cut, oy + h),
            new Vector2(ox, oy + h - cut),
            new Vector2(ox, oy + cut)
        };
    }

    private static Vector2[] HexPoints(float cx, float cy, float r)
    {
        var points = new Vector2[6];
        for (int i = 0; i < points.Length; i++)
        {
            float a = Mathf.Deg2Rad * (90f + i * 60f);
            points[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
        }
        return points;
    }

    private static void FillPolygon(Texture2D t, Vector2[] points, Color color)
    {
        int minY = Mathf.Max(0, Mathf.FloorToInt(MinY(points)));
        int maxY = Mathf.Min(t.height - 1, Mathf.CeilToInt(MaxY(points)));
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = 0; x < t.width; x++)
            {
                if (ContainsPoint(points, x + 0.5f, y + 0.5f))
                    BlendPixel(t, x, y, color);
            }
        }
    }

    private static bool ContainsPoint(Vector2[] points, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            if ((points[i].y > y) != (points[j].y > y)
                && x < (points[j].x - points[i].x) * (y - points[i].y) / (points[j].y - points[i].y) + points[i].x)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static float MinY(Vector2[] points)
    {
        float min = points[0].y;
        for (int i = 1; i < points.Length; i++)
            min = Mathf.Min(min, points[i].y);
        return min;
    }

    private static float MaxY(Vector2[] points)
    {
        float max = points[0].y;
        for (int i = 1; i < points.Length; i++)
            max = Mathf.Max(max, points[i].y);
        return max;
    }

    private static void DrawPolyline(Texture2D t, Vector2[] points, Color color, int thickness, bool closed)
    {
        int count = closed ? points.Length : points.Length - 1;
        for (int i = 0; i < count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Length];
            DrawLine(t, Mathf.RoundToInt(a.x), Mathf.RoundToInt(a.y), Mathf.RoundToInt(b.x), Mathf.RoundToInt(b.y), color, thickness);
        }
    }

    private static void DrawLine(Texture2D t, int x0, int y0, int x1, int y1, Color color, int thickness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            DrawCircle(t, x0, y0, Mathf.Max(1, thickness / 2), color);
            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private static void DrawRect(Texture2D t, int x, int y, int w, int h, Color color)
    {
        for (int yy = y; yy < y + h; yy++)
        {
            for (int xx = x; xx < x + w; xx++)
                BlendPixel(t, xx, yy, color);
        }
    }

    private static void DrawCircle(Texture2D t, int cx, int cy, int r, Color color)
    {
        int rr = r * r;
        for (int y = cy - r; y <= cy + r; y++)
        {
            for (int x = cx - r; x <= cx + r; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                if (dx * dx + dy * dy <= rr)
                    BlendPixel(t, x, y, color);
            }
        }
    }

    private static void DrawCircleOutline(Texture2D t, int cx, int cy, int r, Color color, int thickness)
    {
        int outer = r * r;
        int innerR = Mathf.Max(0, r - thickness);
        int inner = innerR * innerR;
        for (int y = cy - r; y <= cy + r; y++)
        {
            for (int x = cx - r; x <= cx + r; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                int d = dx * dx + dy * dy;
                if (d <= outer && d >= inner)
                    BlendPixel(t, x, y, color);
            }
        }
    }

    private static void DrawDiamond(Texture2D t, int cx, int cy, int r, Color color)
    {
        Vector2[] points =
        {
            new Vector2(cx, cy + r),
            new Vector2(cx + r, cy),
            new Vector2(cx, cy - r),
            new Vector2(cx - r, cy)
        };
        FillPolygon(t, points, color);
        DrawPolyline(t, points, Silver, 1, true);
    }

    private static void BlendPixel(Texture2D t, int x, int y, Color src)
    {
        if (x < 0 || y < 0 || x >= t.width || y >= t.height)
            return;

        Color dst = t.GetPixel(x, y);
        float outA = src.a + dst.a * (1f - src.a);
        if (outA <= 0.0001f)
        {
            t.SetPixel(x, y, Clear);
            return;
        }

        Color outColor = new Color(
            (src.r * src.a + dst.r * dst.a * (1f - src.a)) / outA,
            (src.g * src.a + dst.g * dst.a * (1f - src.a)) / outA,
            (src.b * src.a + dst.b * dst.a * (1f - src.a)) / outA,
            outA);
        t.SetPixel(x, y, outColor);
    }
}
