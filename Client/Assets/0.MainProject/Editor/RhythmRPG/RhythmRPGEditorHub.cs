#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class RhythmRPGEditorHub : EditorWindow
{
    private Vector2 _scroll;
    private readonly ToolEntry[] _contentTools =
    {
        new ToolEntry("Pattern Editor", "RhythmRPG/Editors/Content/Pattern Editor"),
        new ToolEntry("Skill Editor", "RhythmRPG/Editors/Content/Skill Editor"),
        new ToolEntry("Stage Editor", "RhythmRPG/Editors/Content/Stage Editor"),
    };

    private readonly ToolEntry[] _audioTools =
    {
        new ToolEntry("Rhythm Audio Data Editor", "RhythmRPG/Editors/Audio/Rhythm Audio Data Editor"),
    };

    private readonly ToolEntry[] _worldTools =
    {
        new ToolEntry("Map Painter", "RhythmRPG/Editors/World/Map Painter"),
        new ToolEntry("BoardView Map Baker", "RhythmRPG/Editors/World/BoardView Map Baker"),
        new ToolEntry("Export MapAsset to JSON", "RhythmRPG/Editors/World/Export MapAsset to JSON"),
    };

    private readonly ToolEntry[] _dataTools =
    {
        new ToolEntry("Export All Data", "RhythmRPG/Editors/Data/Export All Data"),
        new ToolEntry("Export Entity Data", "RhythmRPG/Editors/Data/Export Entity Data"),
    };

    private readonly ToolEntry[] _setupTools =
    {
        new ToolEntry("Entity Definitions", "RhythmRPG/Editors/Setup/Entity Definitions"),
        new ToolEntry("Equipment Sockets", "RhythmRPG/Editors/Setup/Equipment Sockets"),
        new ToolEntry("Generate Test Scene Objects", "RhythmRPG/Editors/Debug/Generate Test Scene Objects"),
    };

    private readonly ToolEntry[] _uiTools =
    {
        new ToolEntry("Generate Room UI", "RhythmRPG/Editors/UI/Generate Room UI"),
        new ToolEntry("Create Inventory UI", "RhythmRPG/Editors/UI/Create Inventory UI"),
    };

    [MenuItem("RhythmRPG/Editors/Hub")]
    public static void Open()
    {
        GetWindow<RhythmRPGEditorHub>("RhythmRPG Hub");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("RhythmRPG Editor Hub", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "자주 쓰는 Editor들을 한 곳에 모은 런처입니다. 아래 버튼으로 각 도구를 바로 열 수 있어요.",
            MessageType.Info);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawSection("Content", _contentTools);

        DrawSection("Audio", _audioTools);

        DrawSection("World", _worldTools);

        DrawSection("Data Export", _dataTools);

        DrawSection("Setup", _setupTools);

        DrawSection("UI", _uiTools);

        EditorGUILayout.EndScrollView();
    }

    private static void DrawSection(string title, ToolEntry[] tools)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        foreach (var tool in tools)
        {
            if (GUILayout.Button(tool.Label, GUILayout.Height(28)))
            {
                if (!EditorApplication.ExecuteMenuItem(tool.MenuPath))
                    Debug.LogWarning($"[RhythmRPGEditorHub] Menu item not found: {tool.MenuPath}");
            }
        }

        EditorGUILayout.EndVertical();
    }

    private struct ToolEntry
    {
        public string Label;
        public string MenuPath;

        public ToolEntry(string label, string menuPath)
        {
            Label = label;
            MenuPath = menuPath;
        }
    }
}
#endif
