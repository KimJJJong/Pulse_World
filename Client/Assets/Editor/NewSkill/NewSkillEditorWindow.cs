#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Client.Data;
using GameShared.Data;

public class NewSkillEditorWindow : EditorWindow
{
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

    [MenuItem("RhythmRPG/Editors/New Skill Editor")]
    public static void Open()
    {
        GetWindow<NewSkillEditorWindow>("New Skill Editor");
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
                    // Simple helper to create asset if needed (optional)
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

    private void ExportSkillToJson(NewSkillSO so)
    {
        if (so == null || so.Data == null) return;
        
        string fileName = $"{so.Data.SkillId ?? "Unknown"}.json";
        
        string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName; // .../Client
        string serverPath = System.IO.Path.Combine(projectRoot, "../Server/GameServer/Content/01.Game/Skill/Json");
        serverPath = System.IO.Path.GetFullPath(serverPath);

        if (!System.IO.Directory.Exists(serverPath))
        {
            try { System.IO.Directory.CreateDirectory(serverPath); }
            catch { serverPath = "Assets/Export/Skill"; }
        }

        string path = EditorUtility.SaveFilePanel("Export Skill JSON", serverPath, fileName, "json");
        
        if (string.IsNullOrEmpty(path)) return;

        var settings = BatchDataExporter.GetJsonSettings();

        string json = Newtonsoft.Json.JsonConvert.SerializeObject(so.Data, settings);
        System.IO.File.WriteAllText(path, json);
        UnityEngine.Debug.Log($"Exported to {path}");
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
                m.Distance = EditorGUILayout.IntField("Distance", m.Distance);
                m.DirectionX = EditorGUILayout.IntField("Dir X", m.DirectionX);
                m.DirectionY = EditorGUILayout.IntField("Dir Y", m.DirectionY);
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
        }
    }

    // State for Visual Grid
    private int _gridRange = 6;
    private int _casterSize = 1; // 1x1, 3x3, etc.

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
            _gridRange = EditorGUILayout.IntSlider("Grid Size", _gridRange, 4, 12);
            _casterSize = EditorGUILayout.IntSlider("Caster Size (Visual)", _casterSize, 1, 5);
            EditorGUILayout.EndHorizontal();
            
            DrawVisualGrid(c);
        }
    }

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

        // Draw Caster visual footprint (Yellow Box)
        float casterPixelSize = _casterSize * size;
        Rect casterRect = new Rect(center.x - casterPixelSize/2f, center.y - casterPixelSize/2f, casterPixelSize, casterPixelSize);
        EditorGUI.DrawRect(casterRect, new Color(1f, 1f, 0f, 0.2f)); // Translucent Yellow
        Handles.color = Color.yellow;
        Handles.DrawWireDisc(center, Vector3.forward, 2f); // Exact Center

        // Draw Cells
        for (int y = -range; y <= range; y++)
        {
            for (int x = -range; x <= range; x++)
            {
                // Coordinates: +X is Right, +Y is Up (Logic)
                // Drawing: +Y is Down
                float dx = center.x + x * size - size/2;
                float dy = center.y - y * size - size/2;
                Rect cell = new Rect(dx, dy, size, size);

                bool on = shape.Cells.Any(p => p.X == x && p.Y == y);
                
                // Coloring
                if (on) 
                    EditorGUI.DrawRect(cell, new Color(0.2f, 1f, 0.2f, 0.7f)); // Green Active
                
                // Borders
                Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                Handles.DrawWireCube(cell.center, cell.size);

                // Mouse Interaction
                if (Event.current.type == EventType.MouseDown && cell.Contains(Event.current.mousePosition))
                {
                    if (on) shape.Cells.RemoveAll(p => p.X == x && p.Y == y);
                    else shape.Cells.Add(new GridPoint(x, y));
                    Event.current.Use();
                    GUI.changed = true;
                }
            }
        }
        
        EditorGUILayout.HelpBox($"Drawing on relative coordinates. Center(0,0) is the Caster's pivot.\nYellow Area = Visual reference for a {_casterSize}x{_casterSize} caster.", MessageType.Info);
    }
}
#endif
