#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AppearanceAutoTilePalette))]
public sealed class AppearanceAutoTilePaletteEditor : Editor
{
    private const float SourceCellSize = 48f;
    private const float SlotWidth = 112f;
    private const float SlotHeight = 122f;

    private static readonly int[] Blob47Masks = CreateBlob47Masks();
    private static readonly Color SlotEmptyColor = new Color(0.16f, 0.16f, 0.16f, 1f);
    private static readonly Color SlotFilledColor = new Color(0.20f, 0.27f, 0.24f, 1f);
    private static readonly Color SelectedColor = new Color(0.25f, 0.62f, 0.95f, 1f);

    private AppearanceTileKind _kind = AppearanceTileKind.GrassBorder;
    private int _selectedSourceCell;
    private int _paintMask = byte.MaxValue;
    private Vector2 _sourceScroll;
    private Vector2 _slotScroll;
    private bool _showRawData;

    public override void OnInspectorGUI()
    {
        var palette = (AppearanceAutoTilePalette)target;
        EnsureDefinitions(palette);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Appearance Auto Tile Setup", EditorStyles.boldLabel);

        AppearanceAutoTileDefinition definition;
        using (new EditorGUILayout.VerticalScope("box"))
        {
            DrawTargetTileControls(palette, out definition);
            EditorGUILayout.Space(6);
            DrawSourceSheetControls(palette, definition);
        }

        EditorGUILayout.Space(8);
        DrawSourceSheetGrid(definition);

        EditorGUILayout.Space(8);
        DrawSelectedSourceMaskPainter(palette, definition);

        EditorGUILayout.Space(8);
        DrawAutoTileSlots(palette, definition);

        EditorGUILayout.Space(8);
        _showRawData = EditorGUILayout.Foldout(_showRawData, "Raw Palette Data", true);
        if (_showRawData)
        {
            EditorGUILayout.HelpBox("일반적으로는 위 Setup UI만 사용하면 됩니다. 아래 Raw Data는 디버그/수동 보정용입니다.", MessageType.Info);
            DrawDefaultInspector();
        }
    }

    private void DrawTargetTileControls(AppearanceAutoTilePalette palette, out AppearanceAutoTileDefinition definition)
    {
        EditorGUI.BeginChangeCheck();
        _kind = (AppearanceTileKind)EditorGUILayout.EnumPopup("Target Tile", _kind);
        if (EditorGUI.EndChangeCheck())
        {
            if (_kind == AppearanceTileKind.None)
                _kind = AppearanceTileKind.GrassBorder;
            _selectedSourceCell = 0;
        }

        definition = GetOrCreateDefinition(palette, _kind);

        EditorGUI.BeginChangeCheck();
        definition.DisplayName = EditorGUILayout.TextField("Display Name", definition.DisplayName);
        definition.PreviewColor = EditorGUILayout.ColorField("Preview Color", definition.PreviewColor);
        definition.DefaultMaterial = (Material)EditorGUILayout.ObjectField("Default Material", definition.DefaultMaterial, typeof(Material), false);
        definition.DefaultPrefab = (GameObject)EditorGUILayout.ObjectField("Default Prefab", definition.DefaultPrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
            MarkPaletteDirty(palette);

        int assigned = CountAssignedMaterials(definition);
        EditorGUILayout.LabelField($"Assigned AutoTile Slots: {assigned} / {Blob47Masks.Length}");

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(definition.SetupSourceTexture == null))
            {
                if (GUILayout.Button("Auto Fill 47 From Sheet"))
                    AutoFillFromSheet(palette, definition);

                if (GUILayout.Button("Set Default From Selected"))
                    AssignDefaultFromSelectedCell(palette, definition);
            }

            using (new EditorGUI.DisabledScope(assigned == 0 && definition.DefaultMaterial == null))
            {
                if (GUILayout.Button("Clear Target Tile"))
                    ClearTargetTile(palette, definition);
            }
        }
    }

    private void DrawSourceSheetControls(AppearanceAutoTilePalette palette, AppearanceAutoTileDefinition definition)
    {
        EditorGUI.BeginChangeCheck();
        definition.SetupSourceTexture = (Texture2D)EditorGUILayout.ObjectField(
            "Source Tile Sheet",
            definition.SetupSourceTexture,
            typeof(Texture2D),
            false);

        using (new EditorGUILayout.HorizontalScope())
        {
            definition.SetupColumns = Mathf.Clamp(EditorGUILayout.IntField("Columns", definition.SetupColumns), 1, 64);
            definition.SetupRows = Mathf.Clamp(EditorGUILayout.IntField("Rows", definition.SetupRows), 1, 64);
        }

        DrawSheetLayoutControls(definition);

        int maxCell = Mathf.Max(0, definition.SetupColumns * definition.SetupRows - 1);
        _selectedSourceCell = Mathf.Clamp(EditorGUILayout.IntField("Selected Cell", _selectedSourceCell), 0, maxCell);

        if (EditorGUI.EndChangeCheck())
        {
            ConfigureTextureImporter(definition.SetupSourceTexture);
            MarkPaletteDirty(palette);
        }

        EditorGUILayout.HelpBox(
            "Source Tile Sheet에서 칸을 클릭한 뒤 아래 AutoTile 슬롯을 클릭하면 해당 규칙 슬롯에 들어갑니다. 순서가 맞는 시트라면 Auto Fill 47 From Sheet로 한 번에 채울 수 있습니다.",
            MessageType.None);
    }

    private void DrawSheetLayoutControls(AppearanceAutoTileDefinition definition)
    {
        Texture2D texture = definition.SetupSourceTexture;
        if (texture != null)
        {
            float autoCellW = texture.width / (float)Mathf.Max(1, definition.SetupColumns);
            float autoCellH = texture.height / (float)Mathf.Max(1, definition.SetupRows);
            EditorGUILayout.LabelField(
                "Sheet Pixels",
                $"{texture.width} x {texture.height}  /  Auto Cell {autoCellW:0.###} x {autoCellH:0.###}");

            bool fractional = !Mathf.Approximately(autoCellW, Mathf.Round(autoCellW))
                || !Mathf.Approximately(autoCellH, Mathf.Round(autoCellH));
            if (fractional)
            {
                EditorGUILayout.HelpBox(
                    "이미지 크기가 Columns/Rows로 딱 나누어지지 않습니다. 타일이 잘려 보이면 Tile Width/Height를 실제 한 칸 픽셀 크기로 지정해줘.",
                    MessageType.Warning);
            }
        }

        EditorGUILayout.LabelField("Cell Crop Override", EditorStyles.miniBoldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            definition.SetupTileWidth = Mathf.Max(0, EditorGUILayout.IntField("Tile W", definition.SetupTileWidth));
            definition.SetupTileHeight = Mathf.Max(0, EditorGUILayout.IntField("Tile H", definition.SetupTileHeight));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            definition.SetupMarginX = Mathf.Max(0, EditorGUILayout.IntField("Margin X", definition.SetupMarginX));
            definition.SetupMarginY = Mathf.Max(0, EditorGUILayout.IntField("Margin Y", definition.SetupMarginY));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            definition.SetupSpacingX = Mathf.Max(0, EditorGUILayout.IntField("Spacing X", definition.SetupSpacingX));
            definition.SetupSpacingY = Mathf.Max(0, EditorGUILayout.IntField("Spacing Y", definition.SetupSpacingY));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Full Sheet Grid"))
            {
                definition.SetupTileWidth = 0;
                definition.SetupTileHeight = 0;
                definition.SetupMarginX = 0;
                definition.SetupMarginY = 0;
                definition.SetupSpacingX = 0;
                definition.SetupSpacingY = 0;
                GUI.changed = true;
            }

            using (new EditorGUI.DisabledScope(texture == null))
            {
                if (GUILayout.Button("Fit Integer Cell"))
                {
                    definition.SetupTileWidth = texture.width / Mathf.Max(1, definition.SetupColumns);
                    definition.SetupTileHeight = texture.height / Mathf.Max(1, definition.SetupRows);
                    definition.SetupMarginX = Mathf.Max(0, (texture.width - definition.SetupTileWidth * definition.SetupColumns) / 2);
                    definition.SetupMarginY = Mathf.Max(0, (texture.height - definition.SetupTileHeight * definition.SetupRows) / 2);
                    definition.SetupSpacingX = 0;
                    definition.SetupSpacingY = 0;
                    GUI.changed = true;
                }
            }
        }
    }

    private void DrawSelectedSourceMaskPainter(AppearanceAutoTilePalette palette, AppearanceAutoTileDefinition definition)
    {
        EditorGUILayout.LabelField("Selected Source Mask Painter", EditorStyles.boldLabel);
        if (definition.SetupSourceTexture == null)
        {
            EditorGUILayout.HelpBox("Source Tile Sheet를 지정하면 선택한 칸에 3x3 마스크를 칠해서 규칙을 만들 수 있습니다.", MessageType.Info);
            return;
        }

        _paintMask = NormalizeBlobMask(_paintMask);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect previewRect = GUILayoutUtility.GetRect(108f, 108f, GUILayout.Width(108f), GUILayout.Height(108f));
                DrawTextureCell(previewRect, definition, _selectedSourceCell, 1f);
                GUI.Box(previewRect, GUIContent.none);
                GUI.Label(new Rect(previewRect.x + 4f, previewRect.y + 4f, 72f, 18f), $"Cell {_selectedSourceCell}", EditorStyles.miniBoldLabel);

                GUILayout.Space(10f);
                DrawMaskToggleGrid();

                GUILayout.Space(10f);
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField($"Current Mask: {_paintMask}", EditorStyles.boldLabel);
                    Material current = GetVariantMaterial(definition, _paintMask);
                    if (current != null)
                        EditorGUILayout.LabelField("This mask already has a tile.", EditorStyles.miniLabel);
                    else
                        EditorGUILayout.LabelField("This mask is empty.", EditorStyles.miniLabel);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Assign Cell To Mask", GUILayout.Height(28f)))
                            AssignSlotFromSelectedCell(palette, definition, _paintMask);

                        if (GUILayout.Button("Set As Default", GUILayout.Height(28f)))
                            AssignDefaultFromSelectedCell(palette, definition);
                    }

                    using (new EditorGUI.DisabledScope(current == null))
                    {
                        if (GUILayout.Button("Clear This Mask"))
                            ClearMaskSlot(palette, definition, _paintMask);
                    }

                    EditorGUILayout.HelpBox(
                        "원본 칸을 고르고 3x3에서 연결되어야 하는 방향을 켠 뒤 Assign하면 됩니다. 대각선을 켜면 필요한 가로/세로 방향도 같이 켜집니다.",
                        MessageType.None);
                }
            }
        }
    }

    private void DrawMaskToggleGrid()
    {
        Rect gridRect = GUILayoutUtility.GetRect(132f, 132f, GUILayout.Width(132f), GUILayout.Height(132f));
        string[] labels = { "NW", "N", "NE", "W", "C", "E", "SW", "S", "SE" };
        int[] bits =
        {
            MapAsset.AppearanceNorthWest,
            MapAsset.AppearanceNorth,
            MapAsset.AppearanceNorthEast,
            MapAsset.AppearanceWest,
            0,
            MapAsset.AppearanceEast,
            MapAsset.AppearanceSouthWest,
            MapAsset.AppearanceSouth,
            MapAsset.AppearanceSouthEast
        };

        for (int i = 0; i < 9; i++)
        {
            int col = i % 3;
            int row = i / 3;
            Rect cell = new Rect(gridRect.x + col * 44f, gridRect.y + row * 44f, 42f, 42f);
            bool isCenter = i == 4;
            bool active = isCenter || (_paintMask & bits[i]) != 0;

            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = active ? SelectedColor : Color.gray;
            using (new EditorGUI.DisabledScope(isCenter))
            {
                if (GUI.Button(cell, labels[i]))
                    TogglePaintMaskBit(bits[i]);
            }
            GUI.backgroundColor = previous;
        }
    }

    private void DrawSourceSheetGrid(AppearanceAutoTileDefinition definition)
    {
        EditorGUILayout.LabelField("Source Tile Sheet", EditorStyles.boldLabel);
        if (definition.SetupSourceTexture == null)
        {
            EditorGUILayout.HelpBox("먼저 Source Tile Sheet를 지정해줘.", MessageType.Warning);
            return;
        }

        int columns = Mathf.Max(1, definition.SetupColumns);
        int rows = Mathf.Max(1, definition.SetupRows);
        float gridW = columns * SourceCellSize;
        float gridH = rows * SourceCellSize;
        float viewH = Mathf.Min(360f, gridH + 18f);

        _sourceScroll = EditorGUILayout.BeginScrollView(_sourceScroll, GUILayout.Height(viewH));
        Rect gridRect = GUILayoutUtility.GetRect(gridW, gridH, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));

        for (int i = 0; i < columns * rows; i++)
        {
            int col = i % columns;
            int row = i / columns;
            Rect cellRect = new Rect(
                gridRect.x + col * SourceCellSize,
                gridRect.y + row * SourceCellSize,
                SourceCellSize,
                SourceCellSize);

            DrawTextureCell(cellRect, definition, i, 1f);
            GUI.Box(cellRect, GUIContent.none);

            if (i == _selectedSourceCell)
                DrawSelectionFrame(cellRect, SelectedColor, 3f);

            if (GUI.Button(cellRect, GUIContent.none, GUIStyle.none))
            {
                _selectedSourceCell = i;
                Repaint();
            }

            GUI.Label(new Rect(cellRect.x + 3f, cellRect.y + 2f, 36f, 16f), i.ToString(), EditorStyles.miniLabel);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawAutoTileSlots(AppearanceAutoTilePalette palette, AppearanceAutoTileDefinition definition)
    {
        EditorGUILayout.LabelField("AutoTile Rule Slots", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("각 슬롯은 주변 연결 규칙입니다. 슬롯을 클릭하면 현재 선택한 Source Cell이 들어갑니다.", MessageType.None);

        int columns = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 38f) / SlotWidth));
        int rows = Mathf.CeilToInt(Blob47Masks.Length / (float)columns);
        float gridW = columns * SlotWidth;
        float gridH = rows * SlotHeight;
        float viewH = Mathf.Min(520f, gridH + 18f);

        _slotScroll = EditorGUILayout.BeginScrollView(_slotScroll, GUILayout.Height(viewH));
        Rect gridRect = GUILayoutUtility.GetRect(gridW, gridH, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));

        for (int i = 0; i < Blob47Masks.Length; i++)
        {
            int mask = Blob47Masks[i];
            int col = i % columns;
            int row = i / columns;
            Rect slotRect = new Rect(
                gridRect.x + col * SlotWidth + 4f,
                gridRect.y + row * SlotHeight + 4f,
                SlotWidth - 8f,
                SlotHeight - 8f);

            DrawSlot(palette, definition, slotRect, mask, i);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSlot(AppearanceAutoTilePalette palette, AppearanceAutoTileDefinition definition, Rect slotRect, int mask, int index)
    {
        Material material = GetVariantMaterial(definition, mask);
        bool hasMaterial = material != null;
        EditorGUI.DrawRect(slotRect, hasMaterial ? SlotFilledColor : SlotEmptyColor);

        Rect previewRect = new Rect(slotRect.x + 8f, slotRect.y + 8f, slotRect.width - 16f, 62f);
        if (hasMaterial)
            DrawMaterialPreview(previewRect, material, 1f);
        else
            DrawMaskPreview(previewRect, mask, definition.PreviewColor);

        Rect maskGlyphRect = new Rect(slotRect.x + 8f, slotRect.y + 74f, 28f, 28f);
        DrawMaskPreview(maskGlyphRect, mask, definition.PreviewColor);

        GUI.Label(
            new Rect(slotRect.x + 42f, slotRect.y + 74f, slotRect.width - 46f, 16f),
            $"Mask {mask}",
            EditorStyles.miniBoldLabel);
        GUI.Label(
            new Rect(slotRect.x + 42f, slotRect.y + 90f, slotRect.width - 46f, 16f),
            $"#{index}",
            EditorStyles.miniLabel);

        if (GUI.Button(slotRect, GUIContent.none, GUIStyle.none))
            AssignSlotFromSelectedCell(palette, definition, mask);
    }

    private void AssignSlotFromSelectedCell(AppearanceAutoTilePalette palette, AppearanceAutoTileDefinition definition, int mask)
    {
        if (definition.SetupSourceTexture == null)
            return;

        Undo.RecordObject(palette, "Assign Appearance AutoTile Slot");
        ConfigureTextureImporter(definition.SetupSourceTexture);
        var material = CreateOrUpdateMaterial(palette, definition, mask, _selectedSourceCell, false);
        SetVariantMaterial(definition, mask, material);
        MarkPaletteDirty(palette);
    }

    private void AssignDefaultFromSelectedCell(AppearanceAutoTilePalette palette, AppearanceAutoTileDefinition definition)
    {
        if (definition.SetupSourceTexture == null)
            return;

        Undo.RecordObject(palette, "Assign Appearance AutoTile Default");
        ConfigureTextureImporter(definition.SetupSourceTexture);
        definition.DefaultMaterial = CreateOrUpdateMaterial(palette, definition, -1, _selectedSourceCell, true);
        MarkPaletteDirty(palette);
    }

    private void AutoFillFromSheet(AppearanceAutoTilePalette palette, AppearanceAutoTileDefinition definition)
    {
        if (definition.SetupSourceTexture == null)
            return;

        Undo.RecordObject(palette, "Auto Fill Appearance AutoTile");
        ConfigureTextureImporter(definition.SetupSourceTexture);

        int sourceCount = definition.SetupColumns * definition.SetupRows;
        int count = Mathf.Min(sourceCount, Blob47Masks.Length);
        var variants = new List<AppearanceAutoTileVariant>();
        Material defaultMaterial = null;

        for (int i = 0; i < count; i++)
        {
            int mask = Blob47Masks[i];
            var material = CreateOrUpdateMaterial(palette, definition, mask, i, false);
            variants.Add(new AppearanceAutoTileVariant
            {
                Mask = mask,
                Material = material
            });

            if (mask == byte.MaxValue)
                defaultMaterial = material;
        }

        definition.Variants = variants.ToArray();
        if (defaultMaterial != null)
            definition.DefaultMaterial = defaultMaterial;

        MarkPaletteDirty(palette);
    }

    private void ClearTargetTile(AppearanceAutoTilePalette palette, AppearanceAutoTileDefinition definition)
    {
        Undo.RecordObject(palette, "Clear Appearance AutoTile Slots");
        definition.DefaultMaterial = null;
        definition.DefaultPrefab = null;
        definition.Variants = Array.Empty<AppearanceAutoTileVariant>();
        MarkPaletteDirty(palette);
    }

    private void ClearMaskSlot(AppearanceAutoTilePalette palette, AppearanceAutoTileDefinition definition, int mask)
    {
        Undo.RecordObject(palette, "Clear Appearance AutoTile Mask");
        var variants = new List<AppearanceAutoTileVariant>(definition.Variants ?? Array.Empty<AppearanceAutoTileVariant>());
        variants.RemoveAll(variant => variant == null || variant.Mask == mask);
        definition.Variants = variants.ToArray();
        MarkPaletteDirty(palette);
    }

    private void TogglePaintMaskBit(int bit)
    {
        if ((_paintMask & bit) != 0)
        {
            _paintMask &= ~bit;
            _paintMask = ClearDependentCorners(_paintMask, bit);
        }
        else
        {
            _paintMask |= bit;
        }

        _paintMask = NormalizeBlobMask(_paintMask);
        Repaint();
    }

    private Material CreateOrUpdateMaterial(
        AppearanceAutoTilePalette palette,
        AppearanceAutoTileDefinition definition,
        int mask,
        int sourceCell,
        bool isDefault)
    {
        string folder = EnsureGeneratedMaterialFolder(palette, definition);
        string materialName = isDefault
            ? $"{definition.Kind}_Default_cell_{sourceCell:000}"
            : $"{definition.Kind}_mask_{mask:000}_cell_{sourceCell:000}";
        string materialPath = $"{folder}/{SanitizeFileName(materialName)}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = FindTextureShader();
            if (shader == null)
            {
                Debug.LogError("[AppearanceAutoTilePaletteEditor] No compatible texture shader found.");
                return null;
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }

        material.name = Path.GetFileNameWithoutExtension(materialPath);
        ConfigureMaterialTexture(material, definition, sourceCell);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureMaterialTexture(Material material, AppearanceAutoTileDefinition definition, int sourceCell)
    {
        Texture2D texture = definition.SetupSourceTexture;
        Rect texCoords = GetCellTexCoords(definition, sourceCell);

        material.mainTexture = texture;
        material.mainTextureScale = new Vector2(texCoords.width, texCoords.height);
        material.mainTextureOffset = new Vector2(texCoords.x, texCoords.y);

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", material.mainTextureScale);
            material.SetTextureOffset("_BaseMap", material.mainTextureOffset);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", material.mainTextureScale);
            material.SetTextureOffset("_MainTex", material.mainTextureOffset);
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
    }

    private static void DrawTextureCell(Rect rect, AppearanceAutoTileDefinition definition, int sourceCell, float alpha)
    {
        Texture texture = definition.SetupSourceTexture;
        Rect texCoords = GetCellTexCoords(definition, sourceCell);
        Color previous = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTextureWithTexCoords(rect, texture, texCoords, true);
        GUI.color = previous;
    }

    private static void DrawMaterialPreview(Rect rect, Material material, float alpha)
    {
        Texture texture = GetMaterialTexture(material);
        if (texture == null)
        {
            EditorGUI.DrawRect(rect, Color.black);
            return;
        }

        Color previous = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTextureWithTexCoords(rect, texture, GetMaterialTexCoords(material), true);
        GUI.color = previous;
    }

    private static void DrawMaskPreview(Rect rect, int mask, Color color)
    {
        EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, 0.22f));
        Rect center = new Rect(
            rect.x + rect.width * 0.32f,
            rect.y + rect.height * 0.32f,
            rect.width * 0.36f,
            rect.height * 0.36f);
        Color strong = new Color(color.r, color.g, color.b, 0.92f);

        if ((mask & MapAsset.AppearanceNorth) != 0)
            EditorGUI.DrawRect(new Rect(center.x, rect.y, center.width, center.yMax - rect.y), strong);
        if ((mask & MapAsset.AppearanceEast) != 0)
            EditorGUI.DrawRect(new Rect(center.x, center.y, rect.xMax - center.x, center.height), strong);
        if ((mask & MapAsset.AppearanceSouth) != 0)
            EditorGUI.DrawRect(new Rect(center.x, center.y, center.width, rect.yMax - center.y), strong);
        if ((mask & MapAsset.AppearanceWest) != 0)
            EditorGUI.DrawRect(new Rect(rect.x, center.y, center.xMax - rect.x, center.height), strong);

        float cornerW = rect.width * 0.25f;
        float cornerH = rect.height * 0.25f;
        if ((mask & MapAsset.AppearanceNorthEast) != 0)
            EditorGUI.DrawRect(new Rect(rect.xMax - cornerW, rect.y, cornerW, cornerH), strong);
        if ((mask & MapAsset.AppearanceSouthEast) != 0)
            EditorGUI.DrawRect(new Rect(rect.xMax - cornerW, rect.yMax - cornerH, cornerW, cornerH), strong);
        if ((mask & MapAsset.AppearanceSouthWest) != 0)
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - cornerH, cornerW, cornerH), strong);
        if ((mask & MapAsset.AppearanceNorthWest) != 0)
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, cornerW, cornerH), strong);

        EditorGUI.DrawRect(center, strong);
    }

    private static Rect GetCellTexCoords(AppearanceAutoTileDefinition definition, int sourceCell)
    {
        Texture2D texture = definition.SetupSourceTexture;
        int columns = Mathf.Max(1, definition.SetupColumns);
        int rows = Mathf.Max(1, definition.SetupRows);
        if (texture == null || texture.width <= 0 || texture.height <= 0)
            return new Rect(0f, 0f, 1f, 1f);

        sourceCell = Mathf.Clamp(sourceCell, 0, columns * rows - 1);

        int col = sourceCell % columns;
        int rowFromTop = sourceCell / columns;

        float marginX = Mathf.Max(0, definition.SetupMarginX);
        float marginY = Mathf.Max(0, definition.SetupMarginY);
        float spacingX = Mathf.Max(0, definition.SetupSpacingX);
        float spacingY = Mathf.Max(0, definition.SetupSpacingY);
        float autoW = Mathf.Max(1f, (texture.width - marginX * 2f - spacingX * (columns - 1)) / columns);
        float autoH = Mathf.Max(1f, (texture.height - marginY * 2f - spacingY * (rows - 1)) / rows);
        float cellW = definition.SetupTileWidth > 0 ? definition.SetupTileWidth : autoW;
        float cellH = definition.SetupTileHeight > 0 ? definition.SetupTileHeight : autoH;

        float x = marginX + col * (cellW + spacingX);
        float yFromTop = marginY + rowFromTop * (cellH + spacingY);
        float y = texture.height - yFromTop - cellH;

        x = Mathf.Clamp(x, 0f, texture.width - 1f);
        y = Mathf.Clamp(y, 0f, texture.height - 1f);
        cellW = Mathf.Clamp(cellW, 1f, texture.width - x);
        cellH = Mathf.Clamp(cellH, 1f, texture.height - y);

        return new Rect(
            x / texture.width,
            y / texture.height,
            cellW / texture.width,
            cellH / texture.height);
    }

    private static Texture GetMaterialTexture(Material material)
    {
        Texture texture = material.mainTexture;
        if (texture != null)
            return texture;

        if (material.HasProperty("_BaseMap"))
        {
            texture = material.GetTexture("_BaseMap");
            if (texture != null)
                return texture;
        }

        if (material.HasProperty("_MainTex"))
            return material.GetTexture("_MainTex");

        return null;
    }

    private static Rect GetMaterialTexCoords(Material material)
    {
        Vector2 scale = material.mainTextureScale;
        Vector2 offset = material.mainTextureOffset;

        if ((scale == Vector2.one && offset == Vector2.zero) && material.HasProperty("_BaseMap"))
        {
            scale = material.GetTextureScale("_BaseMap");
            offset = material.GetTextureOffset("_BaseMap");
        }

        if ((scale == Vector2.one && offset == Vector2.zero) && material.HasProperty("_MainTex"))
        {
            scale = material.GetTextureScale("_MainTex");
            offset = material.GetTextureOffset("_MainTex");
        }

        if (Mathf.Approximately(scale.x, 0f))
            scale.x = 1f;
        if (Mathf.Approximately(scale.y, 0f))
            scale.y = 1f;

        return new Rect(offset.x, offset.y, scale.x, scale.y);
    }

    private static Material GetVariantMaterial(AppearanceAutoTileDefinition definition, int mask)
    {
        if (definition.Variants == null)
            return null;

        for (int i = 0; i < definition.Variants.Length; i++)
        {
            var variant = definition.Variants[i];
            if (variant != null && variant.Mask == mask)
                return variant.Material;
        }

        return null;
    }

    private static void SetVariantMaterial(AppearanceAutoTileDefinition definition, int mask, Material material)
    {
        var variants = new List<AppearanceAutoTileVariant>(definition.Variants ?? Array.Empty<AppearanceAutoTileVariant>());
        variants.RemoveAll(variant => variant == null);

        for (int i = 0; i < variants.Count; i++)
        {
            if (variants[i].Mask == mask)
            {
                variants[i].Material = material;
                definition.Variants = variants.ToArray();
                return;
            }
        }

        variants.Add(new AppearanceAutoTileVariant
        {
            Mask = mask,
            Material = material
        });
        variants.Sort((a, b) => a.Mask.CompareTo(b.Mask));
        definition.Variants = variants.ToArray();
    }

    private static int CountAssignedMaterials(AppearanceAutoTileDefinition definition)
    {
        int count = 0;
        if (definition.Variants == null)
            return count;

        for (int i = 0; i < definition.Variants.Length; i++)
        {
            if (definition.Variants[i] != null && definition.Variants[i].Material != null)
                count++;
        }

        return count;
    }

    private static AppearanceAutoTileDefinition GetOrCreateDefinition(AppearanceAutoTilePalette palette, AppearanceTileKind kind)
    {
        if (palette.TryGetDefinition(kind, out var definition))
            return definition;

        var list = new List<AppearanceAutoTileDefinition>(palette.Tiles ?? Array.Empty<AppearanceAutoTileDefinition>());
        definition = new AppearanceAutoTileDefinition
        {
            Kind = kind,
            DisplayName = kind.ToString(),
            PreviewColor = AppearanceAutoTilePalette.GetBuiltInPreviewColor(kind),
            Variants = Array.Empty<AppearanceAutoTileVariant>()
        };
        list.Add(definition);
        palette.Tiles = list.ToArray();
        EditorUtility.SetDirty(palette);
        return definition;
    }

    private static void EnsureDefinitions(AppearanceAutoTilePalette palette)
    {
        if (palette.Tiles != null && palette.Tiles.Length > 0)
            return;

        palette.Tiles = AppearanceAutoTilePalette.CreateDefaultDefinitions();
        EditorUtility.SetDirty(palette);
    }

    private static void MarkPaletteDirty(AppearanceAutoTilePalette palette)
    {
        EditorUtility.SetDirty(palette);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureTextureImporter(Texture2D texture)
    {
        if (texture == null)
            return;

        string path = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(path) || AssetImporter.GetAtPath(path) is not TextureImporter importer)
            return;

        bool changed = false;
        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.npotScale = TextureImporterNPOTScale.None;
            changed = true;
        }

        if (importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
    }

    private static string EnsureGeneratedMaterialFolder(
        AppearanceAutoTilePalette palette,
        AppearanceAutoTileDefinition definition)
    {
        string palettePath = AssetDatabase.GetAssetPath(palette);
        string root = string.IsNullOrEmpty(palettePath)
            ? "Assets/Resources/Data/Map/AppearancePalettes"
            : Path.GetDirectoryName(palettePath)?.Replace("\\", "/");
        if (string.IsNullOrEmpty(root))
            root = "Assets/Resources/Data/Map/AppearancePalettes";

        string generatedRoot = $"{root}/GeneratedMaterials";
        EnsureAssetFolder(generatedRoot);

        string paletteFolder = $"{generatedRoot}/{SanitizeFileName(palette.name)}";
        EnsureAssetFolder(paletteFolder);

        string kindFolder = $"{paletteFolder}/{SanitizeFileName(definition.Kind.ToString())}";
        EnsureAssetFolder(kindFolder);
        return kindFolder;
    }

    private static void EnsureAssetFolder(string path)
    {
        path = path.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folderName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent))
            return;

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureAssetFolder(parent);

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folderName);
    }

    private static Shader FindTextureShader()
    {
        return Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Texture")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Standard");
    }

    private static void DrawSelectionFrame(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "AppearanceTile";

        char[] invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            for (int j = 0; j < invalidChars.Length; j++)
            {
                if (chars[i] == invalidChars[j])
                {
                    chars[i] = '_';
                    break;
                }
            }
        }

        return new string(chars).Replace(' ', '_');
    }

    private static int ClearDependentCorners(int mask, int disabledBit)
    {
        if (disabledBit == MapAsset.AppearanceNorth)
            mask &= ~(MapAsset.AppearanceNorthEast | MapAsset.AppearanceNorthWest);
        if (disabledBit == MapAsset.AppearanceEast)
            mask &= ~(MapAsset.AppearanceNorthEast | MapAsset.AppearanceSouthEast);
        if (disabledBit == MapAsset.AppearanceSouth)
            mask &= ~(MapAsset.AppearanceSouthEast | MapAsset.AppearanceSouthWest);
        if (disabledBit == MapAsset.AppearanceWest)
            mask &= ~(MapAsset.AppearanceSouthWest | MapAsset.AppearanceNorthWest);

        return mask;
    }

    private static int NormalizeBlobMask(int mask)
    {
        mask = Mathf.Clamp(mask, 0, 255);

        if ((mask & MapAsset.AppearanceNorthEast) != 0)
            mask |= MapAsset.AppearanceNorth | MapAsset.AppearanceEast;
        if ((mask & MapAsset.AppearanceSouthEast) != 0)
            mask |= MapAsset.AppearanceSouth | MapAsset.AppearanceEast;
        if ((mask & MapAsset.AppearanceSouthWest) != 0)
            mask |= MapAsset.AppearanceSouth | MapAsset.AppearanceWest;
        if ((mask & MapAsset.AppearanceNorthWest) != 0)
            mask |= MapAsset.AppearanceNorth | MapAsset.AppearanceWest;

        return mask;
    }

    private static int[] CreateBlob47Masks()
    {
        var masks = new List<int>();
        for (int mask = 0; mask <= 255; mask++)
        {
            if (IsValidBlobMask(mask))
                masks.Add(mask);
        }

        return masks.ToArray();
    }

    private static bool IsValidBlobMask(int mask)
    {
        bool north = (mask & MapAsset.AppearanceNorth) != 0;
        bool east = (mask & MapAsset.AppearanceEast) != 0;
        bool south = (mask & MapAsset.AppearanceSouth) != 0;
        bool west = (mask & MapAsset.AppearanceWest) != 0;

        if ((mask & MapAsset.AppearanceNorthEast) != 0 && (!north || !east))
            return false;
        if ((mask & MapAsset.AppearanceSouthEast) != 0 && (!south || !east))
            return false;
        if ((mask & MapAsset.AppearanceSouthWest) != 0 && (!south || !west))
            return false;
        if ((mask & MapAsset.AppearanceNorthWest) != 0 && (!north || !west))
            return false;

        return true;
    }
}
#endif
