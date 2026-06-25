#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Client.Data;
using GameShared.Data;

public class NewSkillEditorWindow : EditorWindow
{
    private const string DefaultSkillAssetFolder = "Assets/Resources/Data/NewSkills";
    private const string ServerSkillJsonRelativePath = "../Server/GameServer/Content/01.Game/Skill/Json";

    [SerializeField]
    private NewSkillSO _currentAsset;
    private Vector2 _mainScrollPos;
    private Vector2 _timelineScrollPos;

    // Timeline Settings
    private float _beatWidth = 50f;
    private const float HEADER_WIDTH = 200f;
    private const float TRACK_HEIGHT = 40f;
    
    // Interaction
    private SkillEvent _draggingEvent;
    private bool _isResizing;
    private float _dragStartMouseX;
    private int _dragStartBeat;
    private int _dragStartDuration;
    
    // Selection
    private SkillTrack _selectedTrack;
    private SkillEvent _selectedEvent;

    [MenuItem("RhythmRPG/Editors/Content/Skill Editor")]
    public static void Open()
    {
        GetWindow<NewSkillEditorWindow>("Skill Editor");
    }

    private void OnGUI()
    {
        if (_currentAsset == null)
        {
            EditorGUILayout.Space(10);
            _currentAsset = (NewSkillSO)EditorGUILayout.ObjectField("Select Skill Asset", _currentAsset, typeof(NewSkillSO), false);
            if (_currentAsset == null)
            {
                EditorGUILayout.HelpBox("Please select a NewSkillSO to begin.", MessageType.Info);
                if (GUILayout.Button("Create New Asset"))
                {
                    CreateNewSkillAsset();
                }
                return;
            }
        }

        // Top Toolbar
        DrawToolbar();

        // Main Layout
        EditorGUILayout.Space(5);
        _mainScrollPos = EditorGUILayout.BeginScrollView(_mainScrollPos);

        // 1. Skill Setting (Duration, ID)
        EditorGUILayout.LabelField("Skill Settings", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUI.BeginChangeCheck();
            _currentAsset.Data.SkillId = EditorGUILayout.TextField("Skill ID", _currentAsset.Data.SkillId);
            _currentAsset.Data.TotalDurationTicks = EditorGUILayout.IntField("Total Duration (Ticks)", _currentAsset.Data.TotalDurationTicks);
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_currentAsset);
        }

        EditorGUILayout.Space(10);

        // 2. Timeline Editor Area
        DrawTimelineEditor();

        EditorGUILayout.Space(10);

        // 3. Detail Property Inspector
        DrawDetailInspector();

        EditorGUILayout.EndScrollView();
        
        // Handle input events that might need repaint
        if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)
            Repaint();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            _currentAsset = (NewSkillSO)EditorGUILayout.ObjectField(_currentAsset, typeof(NewSkillSO), false, GUILayout.Width(200));

            if (GUILayout.Button("New Skill Asset", EditorStyles.toolbarButton))
            {
                CreateNewSkillAsset();
            }
            
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Export JSON", EditorStyles.toolbarButton))
            {
                ExportSkillToJson(_currentAsset);
            }

            GUILayout.FlexibleSpace();
            
            GUILayout.Label("Zoom:");
            _beatWidth = GUILayout.HorizontalSlider(_beatWidth, 20f, 100f, GUILayout.Width(100));
            
            if (GUILayout.Button("Save", EditorStyles.toolbarButton))
            {
                EditorUtility.SetDirty(_currentAsset);
                AssetDatabase.SaveAssets();
            }
        }
    }

    private void CreateNewSkillAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create New Skill Asset",
            "NewSkill",
            "asset",
            "Save New Skill Asset",
            DefaultSkillAssetFolder);

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        string skillId = System.IO.Path.GetFileNameWithoutExtension(path);
        var asset = CreateInstance<NewSkillSO>();
        asset.Data.SkillId = skillId;
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        _currentAsset = asset;
        EditorGUIUtility.PingObject(asset);
    }

    private void ExportSkillToJson(NewSkillSO so)
    {
        if (so == null || so.Data == null) return;

        string skillId = string.IsNullOrWhiteSpace(so.Data.SkillId) ? so.name : so.Data.SkillId;
        so.Data.SkillId = skillId;
        EditorUtility.SetDirty(so);

        string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName; // .../Client
        string serverPath = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(projectRoot, ServerSkillJsonRelativePath, $"{skillId}.json"));
        string directory = System.IO.Path.GetDirectoryName(serverPath);
        if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        var settings = BatchDataExporter.GetJsonSettings();
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(so.Data, settings);
        System.IO.File.WriteAllText(serverPath, json);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        UnityEngine.Debug.Log($"[SkillEditor] Exported to {serverPath}");
    }

    // ------------------------------------------------------------------------
    // Timeline Implementation
    // ------------------------------------------------------------------------
    private void DrawTimelineEditor()
    {
        EditorGUILayout.LabelField("Timeline", EditorStyles.boldLabel);
        
        var data = _currentAsset.Data;
        
        // Calculate total rect for timeline
        float totalHeight = data.Tracks.Count * TRACK_HEIGHT + 40f; // +Scrollbar space
        Rect containerRect = EditorGUILayout.GetControlRect(false, Mathf.Max(totalHeight, 200));
        
        // Background
        EditorGUI.DrawRect(containerRect, new Color(0.18f, 0.18f, 0.18f));

        // 1. Left Header Panel (Track Names)
        Rect headerRect = new Rect(containerRect.x, containerRect.y, HEADER_WIDTH, containerRect.height);
        EditorGUI.DrawRect(headerRect, new Color(0.25f, 0.25f, 0.25f));
        
        float currentY = headerRect.y;
        for (int i = 0; i < data.Tracks.Count; i++)
        {
            Rect trackHeaderRect = new Rect(headerRect.x, currentY, HEADER_WIDTH, TRACK_HEIGHT);
            DrawTrackHeader(data.Tracks[i], trackHeaderRect, i);
            currentY += TRACK_HEIGHT;
        }

        // Add Track Button
        Rect addTrackRect = new Rect(headerRect.x, currentY, HEADER_WIDTH, 20);
        if (GUI.Button(addTrackRect, "+ Add Track"))
        {
            data.Tracks.Add(new SkillTrack { TrackName = $"Track {data.Tracks.Count}" });
        }


        // 2. Right Content Panel (Timeline)
        Rect viewRect = new Rect(containerRect.x + HEADER_WIDTH, containerRect.y, containerRect.width - HEADER_WIDTH, containerRect.height);
        
        // Scroll View for Timeline
        int totalBeatsVisual = Mathf.CeilToInt(_currentAsset.Data.TotalDurationTicks / 480f);
        float contentWidth = Mathf.Max(viewRect.width, totalBeatsVisual * _beatWidth + 100);
        float contentHeight = Mathf.Max(viewRect.height, data.Tracks.Count * TRACK_HEIGHT + 50);
        
        _timelineScrollPos = GUI.BeginScrollView(viewRect, _timelineScrollPos, new Rect(0, 0, contentWidth, contentHeight));
        {
            // Draw Grid
            DrawGrid(contentWidth, contentHeight, totalBeatsVisual);

            // Draw Events
            float trackY = 0;
            for (int i = 0; i < data.Tracks.Count; i++)
            {
                Rect trackRect = new Rect(0, trackY, contentWidth, TRACK_HEIGHT);
                DrawTrackEvents(data.Tracks[i], trackRect);
                trackY += TRACK_HEIGHT;
            }
        }
        GUI.EndScrollView();
    }

    private void DrawTrackHeader(SkillTrack track, Rect rect, int index)
    {
        // Outline
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), Color.black);
        
        Rect labelRect = new Rect(rect.x + 5, rect.y + 5, rect.width - 60, 20);
        track.TrackName = EditorGUI.TextField(labelRect, track.TrackName);
        
        Rect delRect = new Rect(rect.xMax - 25, rect.y + 5, 20, 20);
        if (GUI.Button(delRect, "X"))
        {
            if (EditorUtility.DisplayDialog("Delete Track", "Remove this track?", "Yes", "No"))
            {
                _currentAsset.Data.Tracks.RemoveAt(index);
                GUIUtility.ExitGUI(); // Force repaint
            }
        }
    }

    private void DrawGrid(float width, float height, int totalBeats)
    {
        Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
        for (int b = 0; b <= totalBeats; b++)
        {
            float x = b * _beatWidth;
            Handles.DrawLine(new Vector3(x, 0), new Vector3(x, height));
            
            // Beat number
            GUI.Label(new Rect(x + 2, 0, 30, 20), b.ToString(), EditorStyles.miniLabel);
        }
        
        // Max duration line
        float limitX = totalBeats * _beatWidth;
        Handles.color = Color.red;
        Handles.DrawLine(new Vector3(limitX, 0), new Vector3(limitX, height));
    }

    private void DrawTrackEvents(SkillTrack track, Rect trackRect)
    {
        // Draw Track background stripe
        EditorGUI.DrawRect(new Rect(trackRect.x, trackRect.y + trackRect.height - 1, trackRect.width, 1), new Color(0,0,0, 0.3f));

        for (int i = 0; i < track.Events.Count; i++)
        {
            SkillEvent evt = track.Events[i];
            
            float x = (evt.TriggerTick / 480f) * _beatWidth;
            float w = Mathf.Max((evt.DurationTicks / 480f), 0.25f) * _beatWidth; // Min 1/4 beat width visually
            
            Rect evtRect = new Rect(x, trackRect.y + 2, w - 2, trackRect.height - 4);

            // Interaction
            HandleEventInput(evt, evtRect, track);

            // Draw Box
            Color c = GetActionColor(evt.Action);
            if (_selectedEvent == evt) c = Color.cyan;
            EditorGUI.DrawRect(evtRect, c);
            
            // Label
            string label = evt.Action != null ? evt.Action.GetSkillActionType().ToString() : "None";
            GUI.Label(evtRect, label, EditorStyles.whiteMiniLabel);
            
            // Resize Handle (Right edge)
            Rect handleRect = new Rect(evtRect.xMax - 5, evtRect.y, 5, evtRect.height);
            EditorGUI.DrawRect(handleRect, new Color(0,0,0, 0.5f));
            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeHorizontal);
        }

        // Context Menu for Track
        if (Event.current.type == EventType.ContextClick && trackRect.Contains(Event.current.mousePosition))
        {
            float mouseX = Event.current.mousePosition.x;
            
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Add Event"), false, () => 
            {
                int beat = Mathf.FloorToInt(mouseX / _beatWidth);
                track.Events.Add(new SkillEvent { TriggerTick = beat * 480, DurationTicks = 480, Action = new WarningAction { Shape = new DiamondShape{Radius=1}} });
            });
            menu.ShowAsContext();
            Event.current.Use();
        }
    }

    private void HandleEventInput(SkillEvent evt, Rect evtRect, SkillTrack track)
    {
        Event e = Event.current;
        
        // 1. Check Resize Handle
        Rect resizeHandle = new Rect(evtRect.xMax - 10, evtRect.y, 10, evtRect.height);
        
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (resizeHandle.Contains(e.mousePosition))
            {
                _isResizing = true;
                _draggingEvent = evt;
                _selectedEvent = evt;
                _selectedTrack = track;
                _dragStartMouseX = e.mousePosition.x;
                _dragStartDuration = evt.DurationTicks;
                e.Use();
            }
            else if (evtRect.Contains(e.mousePosition))
            {
                _isResizing = false;
                _draggingEvent = evt;
                _selectedEvent = evt;
                _selectedTrack = track;
                _dragStartMouseX = e.mousePosition.x;
                _dragStartBeat = evt.TriggerTick;
                e.Use();
            }
        }
        else if (e.type == EventType.MouseDrag && _draggingEvent == evt)
        {
            float deltaX = e.mousePosition.x - _dragStartMouseX;
            int beatDelta = Mathf.RoundToInt(deltaX / _beatWidth);

            if (_isResizing)
            {
                int newDur = _dragStartDuration + (beatDelta * 480);
                evt.DurationTicks = Mathf.Max(120, newDur); // Min 0.25 Beat
            }
            else
            {
                int newStart = _dragStartBeat + (beatDelta * 480);
                evt.TriggerTick = Mathf.Max(0, newStart);
            }
            
            e.Use();
            GUI.changed = true; // Trigger Repaint
        }
        else if (e.type == EventType.MouseUp && _draggingEvent == evt)
        {
            _draggingEvent = null;
            _isResizing = false;
            e.Use();
        }
        else if (e.type == EventType.ContextClick && evtRect.Contains(e.mousePosition))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Delete Event"), false, () => 
            {
                track.Events.Remove(evt);
                if (_selectedEvent == evt) _selectedEvent = null;
            });
            menu.ShowAsContext();
            e.Use();
        }
    }
    
    // ------------------------------------------------------------------------
    // Detail Editor (Reused from previous step, slightly cleaned)
    // ------------------------------------------------------------------------
    private void DrawDetailInspector()
    {
        if (_selectedEvent == null)
        {
            EditorGUILayout.LabelField("Select an event to edit properties.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        EditorGUILayout.LabelField("Event Inspector", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
            _selectedEvent.TriggerTick = EditorGUILayout.IntField("Trigger Tick", _selectedEvent.TriggerTick);
            _selectedEvent.DurationTicks = EditorGUILayout.IntField("Duration Ticks", _selectedEvent.DurationTicks);
            EditorGUILayout.EndHorizontal();

            // Type Switching
            SkillActionType currentType = _selectedEvent.Action != null ? _selectedEvent.Action.GetSkillActionType() : SkillActionType.None;
            SkillActionType newType = (SkillActionType)EditorGUILayout.EnumPopup("Action Type", currentType);

            if (newType != currentType)
            {
                _selectedEvent.Action = CreateDefaultAction(newType);
            }

            if (_selectedEvent.Action != null)
            {
                DrawActionProperties(_selectedEvent.Action);
            }
            
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_currentAsset);
            }
        }
    }

    private Color GetActionColor(BaseAction action)
    {
        if (action == null) return Color.gray;
        switch (action.GetSkillActionType())
        {
            case SkillActionType.Warning: return new Color(1f, 0.6f, 0f, 0.8f);
            case SkillActionType.Damage: return new Color(1f, 0.2f, 0.2f, 0.8f);
            case SkillActionType.Move: return new Color(0.2f, 0.6f, 1f, 0.8f);
            case SkillActionType.InputLock: return new Color(0.5f, 0.5f, 0.5f, 0.8f);
            case SkillActionType.Sound: return new Color(0.4f, 1f, 0.8f, 0.8f); // Teal/Cyan
            case SkillActionType.SummonDecoy: return new Color(0.8f, 0.6f, 1f, 0.8f);
            default: return Color.green;
        }
    }

    private BaseAction CreateDefaultAction(SkillActionType type)
    {
        switch (type)
        {
            case SkillActionType.Warning: return new WarningAction { Shape = new DiamondShape { Radius = 1 } };
            case SkillActionType.Damage: return new DamageAction { Shape = new DiamondShape { Radius = 1 }, Amount = 10 };
            case SkillActionType.Move: return new MoveAction { Distance = 1, MoveType = MoveType.Dash };
            case SkillActionType.InputLock: return new InputLockAction();
            case SkillActionType.Sound: return new SoundAction { FmodEventPath = "", Volume = 1.0f, UseOwnerPerspective = true };
            case SkillActionType.SummonDecoy: return new SummonDecoyAction();
            default: return new WaitActionStub();
        }
    }
    private class WaitActionStub : BaseAction { public override SkillActionType GetSkillActionType() => SkillActionType.Wait; }

    private void DrawActionProperties(BaseAction action)
    {
        switch (action)
        {
            case WarningAction w:
                DrawShapeEditor(ref w.Shape);
                break;
            case DamageAction d:
                DrawShapeEditor(ref d.Shape);
                d.Amount = EditorGUILayout.IntField("Damage", d.Amount);
                d.HitPlayers = EditorGUILayout.Toggle("Hit Players", d.HitPlayers);
                d.HitMonsters = EditorGUILayout.Toggle("Hit Monsters", d.HitMonsters);
                break;
            case MoveAction m:
                m.MoveType = (MoveType)EditorGUILayout.EnumPopup("Move Type", m.MoveType);
                m.Distance = EditorGUILayout.IntSlider("Distance (Tiles)", m.Distance, 1, 10);
                m.StopOnObstacle = EditorGUILayout.Toggle("Stop On Obstacle", m.StopOnObstacle);

                // 방향 시각화: 플레이어 기준 앞/뒤/왼/우 버튼
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Direction (Player-Relative)", EditorStyles.miniBoldLabel);

                // 현재 방향 표시
                string curDirLabel = GetDirLabel(m.DirectionX, m.DirectionY);
                EditorGUILayout.LabelField($"  Current: {curDirLabel}  (X={m.DirectionX}, Y={m.DirectionY})",
                    EditorStyles.helpBox);

                // 3x3 방향 그리드
                float btnSize = 36f;
                EditorGUILayout.Space(2);
                DrawDirGrid(m, btnSize);

                if (m.MoveType == MoveType.Dash)
                    EditorGUILayout.HelpBox(
                        "Dash: 이동 중 벽이 막하면 바로 앞 지점까지만 이동.",
                        MessageType.None);
                else if (m.MoveType == MoveType.Blink)
                    EditorGUILayout.HelpBox(
                        "Blink: 목표지점이 철스 가능하면 순간이동, 아니면 실패.",
                        MessageType.None);
                break;
            case InputLockAction i:
                EditorGUILayout.HelpBox("Locks Input.", MessageType.None);
                break;
            case SoundAction s:
                EditorGUILayout.HelpBox(
                    "FMOD Event Path를 입력하세요.\n" +
                    "맨약 비워두면 기본 공격 사운드(fallback)로 재생됩니다.\n" +
                    "예: event:/SFX/Attack/Sword",
                    MessageType.Info);
                s.FmodEventPath = EditorGUILayout.TextField("FMOD Event Path", s.FmodEventPath);
                s.Volume = EditorGUILayout.Slider("Volume", s.Volume, 0f, 1f);
                s.UseOwnerPerspective = EditorGUILayout.Toggle("Owner Perspective", s.UseOwnerPerspective);
                break;
            case SummonDecoyAction decoy:
                decoy.AppearanceId = EditorGUILayout.IntField("Appearance Id", decoy.AppearanceId);
                decoy.Hp = EditorGUILayout.IntField("HP", decoy.Hp);
                decoy.DurationTicks = EditorGUILayout.IntField("Duration Ticks", decoy.DurationTicks);
                decoy.OffsetX = EditorGUILayout.IntField("Offset X", decoy.OffsetX);
                decoy.OffsetY = EditorGUILayout.IntField("Offset Y", decoy.OffsetY);
                decoy.RotateWithCaster = EditorGUILayout.Toggle("Rotate With Caster", decoy.RotateWithCaster);
                break;
        }
    }

    private int _gridRange = 6;

    private void DrawShapeEditor(ref IShapeDef shape)
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Shape", EditorStyles.miniBoldLabel);
        
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Diamond", EditorStyles.miniButtonLeft)) shape = new DiamondShape();
            if (GUILayout.Button("Rect", EditorStyles.miniButtonMid)) shape = new RectShape();
            if (GUILayout.Button("Custom", EditorStyles.miniButtonRight)) shape = new CustomCellsShape();
        }

        // Common Properties for all Shapes
        shape.CasterSize = EditorGUILayout.IntSlider("Caster Size", shape.CasterSize, 1, 5);
        shape.RotateWithCaster = EditorGUILayout.Toggle("Rotate With Caster", shape.RotateWithCaster);

        if (shape is DiamondShape d) d.Radius = EditorGUILayout.IntField("Radius", d.Radius);
        else if (shape is RectShape r)
        {
            r.Width = EditorGUILayout.IntField("Width", r.Width);
            r.Height = EditorGUILayout.IntField("Height", r.Height);
        }
        else if (shape is CustomCellsShape c)
        {
            // Grid Settings used only for visualization in Editor
            EditorGUILayout.BeginHorizontal();
            _gridRange = EditorGUILayout.IntSlider("Grid UI Range", _gridRange, 4, 12);
            _previewRotation = (PreviewRotation)EditorGUILayout.EnumPopup("Preview Rotation", _previewRotation);
            EditorGUILayout.EndHorizontal();
            
            DrawVisualGrid(c);
        }
    }

    private enum PreviewRotation { Up = 0, Right = 90, Down = 180, Left = 270 }
    private PreviewRotation _previewRotation = PreviewRotation.Up;

    private void DrawVisualGrid(CustomCellsShape shape)
    {
        int range = _gridRange;
        float size = 20f;
        int dim = range * 2 + 1;
        
        // Reserve Rect
        Rect r = GUILayoutUtility.GetRect(dim * size, dim * size);
        
        // Background
        EditorGUI.DrawRect(r, new Color(0.1f, 0.1f, 0.1f));
        GUI.Box(r, GUIContent.none);
        
        Vector2 center = r.center;

        // 1. Draw Forward Indicator (Arrow)
        DrawForwardArrow(center, (int)PreviewRotation.Up, size * 2, Color.gray); // Base Forward
        if (_previewRotation != PreviewRotation.Up)
            DrawForwardArrow(center, (int)_previewRotation, size * 2, Color.cyan);

        // 2. Draw Grid Boxes
        for (int y = -range; y <= range; y++)
        {
            for (int x = -range; x <= range; x++)
            {
                float dx = center.x + x * size - size/2;
                float dy = center.y - y * size - size/2;
                Rect cellRect = new Rect(dx, dy, size, size);

                // --- Resolution logic (Local vs Rotated) ---
                bool isCaster = IsInsideCaster(x, y, shape.CasterSize);
                
                // For 'on' check, we use the raw data (Up version)
                bool on = shape.Cells.Any(p => p.X == x && p.Y == y);
                
                // For 'preview' check, we rotate the coordinates back to find if it matches data
                GridPoint previewPt = RotatePoint(x, y, -(int)_previewRotation);
                bool previewOn = shape.Cells.Any(p => p.X == previewPt.X && p.Y == previewPt.Y);

                // Coloring
                if (isCaster)
                    EditorGUI.DrawRect(cellRect, new Color(1f, 1f, 0f, 0.4f)); // Caster (Yellow)

                if (previewOn)
                    EditorGUI.DrawRect(cellRect, new Color(0.2f, 1f, 0.2f, 0.6f)); // Active (Green)

                // Borders
                Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                Handles.DrawWireCube(cellRect.center, cellRect.size);

                // Interaction (Always Edit the 'Up' version for consistency)
                if (_previewRotation == PreviewRotation.Up)
                {
                    if (Event.current.type == EventType.MouseDown && cellRect.Contains(Event.current.mousePosition))
                    {
                        if (on) shape.Cells.RemoveAll(p => p.X == x && p.Y == y);
                        else shape.Cells.Add(new GridPoint(x, y));
                        Event.current.Use();
                        GUI.changed = true;
                    }
                }
            }
        }
        
        if (_previewRotation != PreviewRotation.Up)
            EditorGUILayout.HelpBox("Viewing rotated pattern. Toggle to 'Up' to edit cells.", MessageType.Warning);
        else
            EditorGUILayout.HelpBox($"Editing Base Pattern (Forward=Up).\nCaster Size: {shape.CasterSize}x{shape.CasterSize}.", MessageType.Info);
    }

    private void DrawForwardArrow(Vector2 center, int rotationDeg, float length, Color color)
    {
        Handles.color = color;
        Vector3 dir = Quaternion.Euler(0, 0, -rotationDeg) * Vector3.up; 
        Vector3 start = center;
        Vector3 end = (Vector3)center + dir * length;
        
        Handles.DrawLine(start, end);
        // Arrow head (simple)
        Vector3 sideA = Quaternion.Euler(0, 0, 150) * dir * (length * 0.3f);
        Vector3 sideB = Quaternion.Euler(0, 0, -150) * dir * (length * 0.3f);
        Handles.DrawLine(end, end + sideA);
        Handles.DrawLine(end, end + sideB);
    }

    private bool IsInsideCaster(int x, int y, int size)
    {
        int half = size / 2;
        if (size % 2 == 1) // 1x1, 3x3
            return (x >= -half && x <= half && y >= -half && y <= half);
        else // 2x2, 4x4 (pivot is center-offset)
            return (x >= -half && x < half && y >= -half && y < half);
    }

    private GridPoint RotatePoint(int x, int y, int deg)
    {
        // [Rollback] Reverting to previous working CCW mapping
        int d = (deg % 360 + 360) % 360;
        if (d == 90)  return new GridPoint(-y,  x); // Right
        if (d == 180) return new GridPoint(-x, -y); // Down
        if (d == 270) return new GridPoint( y, -x); // Left
        return new GridPoint(x, y);                 // Up (0)
    }

    // -----------------------------------------------------------------------
    // MoveAction Dir Helpers
    // -----------------------------------------------------------------------

    private static string GetDirLabel(int dx, int dy)
    {
        if (dx ==  0 && dy ==  1) return "\u2191 Forward";
        if (dx ==  0 && dy == -1) return "\u2193 Back";
        if (dx == -1 && dy ==  0) return "\u2190 Left";
        if (dx ==  1 && dy ==  0) return "\u2192 Right";
        if (dx ==  1 && dy ==  1) return "\u2197 Fwd-Right";
        if (dx == -1 && dy ==  1) return "\u2196 Fwd-Left";
        if (dx ==  1 && dy == -1) return "\u2198 Back-Right";
        if (dx == -1 && dy == -1) return "\u2199 Back-Left";
        if (dx ==  0 && dy ==  0) return "(None)";
        return $"({dx},{dy})";
    }

    /// <summary>
    /// 3x3 막대 마우스 클릭으로 방향 선택.
    /// 그리드 좌표: Y축 위 = Forward(+Y), 아래 = Back(-Y)
    /// 중앙(0,0)은 비활성 (None 상태)
    /// </summary>
    private void DrawDirGrid(MoveAction m, float btnSize)
    {
        // 레이아웃 중앙 정렬
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical();

        // 행 순서: +Y(Forward)를 위로
        for (int row = 1; row >= -1; row--)
        {
            GUILayout.BeginHorizontal();
            for (int col = -1; col <= 1; col++)
            {
                int dx = col;   // X: -1=Left, 0=Center, +1=Right
                int dy = row;   // Y: -1=Back, 0=Center, +1=Forward

                bool isCenter = (dx == 0 && dy == 0);
                bool isSelected = (m.DirectionX == dx && m.DirectionY == dy);

                string label = GetGridBtnLabel(dx, dy);

                // 선택 색 vs 일반 색
                Color normalBg  = isCenter ? new Color(0.25f,0.25f,0.25f) : new Color(0.35f,0.35f,0.35f);
                Color selectedBg = new Color(0.2f, 0.6f, 1f);

                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = isSelected ? selectedBg : normalBg;

                EditorGUI.BeginDisabledGroup(isCenter);
                if (GUILayout.Button(label, GUILayout.Width(btnSize), GUILayout.Height(btnSize)))
                {
                    m.DirectionX = dx;
                    m.DirectionY = dy;
                    GUI.changed = true;
                }
                EditorGUI.EndDisabledGroup();

                GUI.backgroundColor = prev;
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private static string GetGridBtnLabel(int dx, int dy)
    {
        if (dx ==  0 && dy ==  1) return "\u2191\nFwd";
        if (dx ==  0 && dy == -1) return "\u2193\nBck";
        if (dx == -1 && dy ==  0) return "\u2190\nL";
        if (dx ==  1 && dy ==  0) return "\u2192\nR";
        if (dx ==  1 && dy ==  1) return "\u2197";
        if (dx == -1 && dy ==  1) return "\u2196";
        if (dx ==  1 && dy == -1) return "\u2198";
        if (dx == -1 && dy == -1) return "\u2199";
        return "\u25A0"; // center
    }
}
#endif
