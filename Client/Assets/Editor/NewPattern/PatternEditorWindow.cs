using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Client.Data;

public class PatternEditorWindow : EditorWindow
{
    [MenuItem("RhythmRPG/Editors/Content/Pattern Editor")]
    public static void OpenWindow()
    {
        PatternEditorWindow wnd = GetWindow<PatternEditorWindow>();
        wnd.titleContent = new GUIContent("Pattern Editor");
        wnd.Show();
    }

    private MonsterPatternSO _currentPattern;
    private PhaseDef _selectedPhase;
    private SelectorDef _selectedSelector;

    private Vector2 _scrollLeft;
    private Vector2 _scrollMiddle;
    private Vector2 _scrollRight;

    private float _leftPanelWidth   = 250f;
    private float _middlePanelWidth = 300f;

    private void OnGUI()
    {
        DrawToolbar();

        if (_currentPattern == null)
        {
            EditorGUILayout.HelpBox("Select a MonsterPatternSO to edit.", MessageType.Info);
            return;
        }

        if (_currentPattern.Data == null) _currentPattern.Data = new MonsterPatternDef();
        if (_currentPattern.Data.Phases == null) _currentPattern.Data.Phases = new List<PhaseDef>();

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.BeginHorizontal();
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(_leftPanelWidth));
            _scrollLeft = EditorGUILayout.BeginScrollView(_scrollLeft);
            DrawLeftPanel();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            DrawSplitter();

            EditorGUILayout.BeginVertical(GUILayout.Width(_middlePanelWidth));
            _scrollMiddle = EditorGUILayout.BeginScrollView(_scrollMiddle);
            DrawMiddlePanel();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            DrawSplitter();

            EditorGUILayout.BeginVertical();
            _scrollRight = EditorGUILayout.BeginScrollView(_scrollRight);
            DrawRightPanel();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(_currentPattern);
    }

    // ─────────────────────────────────────────────────────
    // Toolbar
    // ─────────────────────────────────────────────────────
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        _currentPattern = (MonsterPatternSO)EditorGUILayout.ObjectField(_currentPattern, typeof(MonsterPatternSO), false, GUILayout.Width(200));

        if (GUILayout.Button("New Pattern Asset", EditorStyles.toolbarButton))
            CreateNewPatternAsset();

        if (_currentPattern != null && GUILayout.Button("Export JSON", EditorStyles.toolbarButton))
            ExportPatternToJson(_currentPattern);

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Save", EditorStyles.toolbarButton))
        {
            EditorUtility.SetDirty(_currentPattern);
            AssetDatabase.SaveAssets();
        }
        EditorGUILayout.EndHorizontal();
    }

    // ─────────────────────────────────────────────────────
    // Left Panel: Phase & Selector tree
    // ─────────────────────────────────────────────────────
    private void DrawLeftPanel()
    {
        GUILayout.Label($"Pattern: {_currentPattern.Data.MonsterType}", EditorStyles.boldLabel);
        _currentPattern.Data.MonsterType  = EditorGUILayout.TextField("Pattern ID",     _currentPattern.Data.MonsterType);
        _currentPattern.Data.DefaultPhase = EditorGUILayout.TextField("Default Phase",  _currentPattern.Data.DefaultPhase);

        GUILayout.Space(10); DrawSplitter();

        GUILayout.Label("Phase Transitions (Global)", EditorStyles.boldLabel);
        if (_currentPattern.Data.Transitions == null)
            _currentPattern.Data.Transitions = new List<PhaseTransitionDef>();

        var transitions = _currentPattern.Data.Transitions;
        for (int i = 0; i < transitions.Count; i++)
        {
            var trans = transitions[i];
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("From:", GUILayout.Width(40));
            trans.FromPhaseId = EditorGUILayout.TextField(trans.FromPhaseId, GUILayout.Width(50));
            GUILayout.Label("To:", GUILayout.Width(25));
            trans.ToPhaseId = EditorGUILayout.TextField(trans.ToPhaseId, GUILayout.Width(50));
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                transitions.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            trans.Type  = (PhaseTransitionType)EditorGUILayout.EnumPopup(trans.Type, GUILayout.Width(80));
            trans.Value = EditorGUILayout.IntField(trans.Value);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
        if (GUILayout.Button("+ Add Transition"))
            transitions.Add(new PhaseTransitionDef { FromPhaseId = "P1", ToPhaseId = "P2", Type = PhaseTransitionType.HpPercentLE, Value = 50 });

        GUILayout.Space(10); DrawSplitter();
        GUILayout.Label("Phases & Patterns Logic", EditorStyles.boldLabel);

        var phases = _currentPattern.Data.Phases;
        for (int i = 0; i < phases.Count; i++)
        {
            var phase = phases[i];
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(phase.Id, EditorStyles.foldoutHeader))
            {
                _selectedPhase = phase;
                _selectedSelector = null;
                GUI.FocusControl(null);
            }
            bool deletePhase = GUILayout.Button("-", GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();

            if (deletePhase)
            {
                phases.RemoveAt(i);
                if (_selectedPhase == phase) _selectedPhase = null;
                GuiChanged(); return;
            }

            if (_selectedPhase == phase)
            {
                EditorGUI.indentLevel++;
                if (phase.Selectors == null) phase.Selectors = new List<SelectorDef>();

                for (int j = 0; j < phase.Selectors.Count; j++)
                {
                    var sel = phase.Selectors[j];
                    EditorGUILayout.BeginHorizontal();
                    GUIStyle style = (_selectedSelector == sel) ? EditorStyles.whiteLabel : EditorStyles.label;
                    string displayName = string.IsNullOrEmpty(sel.Id) ? "Unnamed Pattern" : sel.Id;
                    if (GUILayout.Button(displayName, style))
                    {
                        _selectedSelector = sel;
                        GUI.FocusControl(null);
                    }
                    bool deleteSelector = GUILayout.Button("-", GUILayout.Width(20));
                    EditorGUILayout.EndHorizontal();
                    if (deleteSelector)
                    {
                        phase.Selectors.RemoveAt(j);
                        if (_selectedSelector == sel) _selectedSelector = null;
                        GuiChanged(); return;
                    }
                }

                if (GUILayout.Button("+ Add Pattern"))
                {
                    phase.Selectors.Add(new SelectorDef { Id = "New Pattern" });
                    GuiChanged();
                }
                EditorGUI.indentLevel--;
            }
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("+ Add Phase"))
        {
            phases.Add(new PhaseDef { Id = $"P{phases.Count + 1}" });
            GuiChanged();
        }
    }

    private void GuiChanged()
    {
        if (_currentPattern != null) EditorUtility.SetDirty(_currentPattern);
        GUI.changed = true;
        GUIUtility.ExitGUI();
    }

    // ─────────────────────────────────────────────────────
    // Middle Panel: Action Inspector / Pattern Settings
    // ─────────────────────────────────────────────────────
    private ActionDef _selectedAction;

    private void DrawMiddlePanel()
    {
        if (_selectedAction != null)
        {
            DrawActionInspector(_selectedAction);
            EditorGUILayout.Space();
            if (GUILayout.Button("Back to Pattern Settings"))
            {
                _selectedAction = null;
                GUI.FocusControl(null);
            }
            return;
        }

        if (_selectedSelector != null)
        {
            GUILayout.Label($"Pattern: {_selectedSelector.Id}", EditorStyles.boldLabel);
            _selectedSelector.Id            = EditorGUILayout.TextField("Name (ID)",       _selectedSelector.Id);
            _selectedSelector.Weight        = EditorGUILayout.IntField("Weight",            _selectedSelector.Weight);
            _selectedSelector.CooldownBeats = EditorGUILayout.IntField("Cooldown (Beats)", _selectedSelector.CooldownBeats);

            EditorGUILayout.Space();
            GUILayout.Label("Conditions (When)", EditorStyles.boldLabel);

            if (_selectedSelector.When == null) _selectedSelector.When = new WhenGroup();
            var conditions = _selectedSelector.When.All;
            for (int i = 0; i < conditions.Count; i++)
            {
                var cond = conditions[i];
                EditorGUILayout.BeginHorizontal();
                cond.Type  = (ConditionType)EditorGUILayout.EnumPopup(cond.Type, GUILayout.Width(130));
                cond.Value = EditorGUILayout.IntField(cond.Value);
                bool deleteCond = GUILayout.Button("-", GUILayout.Width(20));
                EditorGUILayout.EndHorizontal();
                if (deleteCond) { conditions.RemoveAt(i); GuiChanged(); return; }
            }
            if (GUILayout.Button("+ Add Condition")) { conditions.Add(new ConditionDef()); GuiChanged(); }
        }
        else if (_selectedPhase != null)
        {
            GUILayout.Label($"Phase: {_selectedPhase.Id}", EditorStyles.boldLabel);
            _selectedPhase.Id = EditorGUILayout.TextField("Phase ID", _selectedPhase.Id);
        }
        else
        {
            GUILayout.Label("Select a valid item or Action to edit.", EditorStyles.centeredGreyMiniLabel);
        }
    }

    // ─────────────────────────────────────────────────────
    // Action Inspector
    // ─────────────────────────────────────────────────────
    private void DrawActionInspector(ActionDef action)
    {
        GUILayout.Label("Action Inspector", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            action.Type          = (ActionType)EditorGUILayout.EnumPopup("Action Type", action.Type);
            action.AtBeatOffset  = EditorGUILayout.IntField("Start Beat", action.AtBeatOffset);
            EditorGUILayout.Space(5);

            switch (action.Type)
            {
                // ── Skill ──────────────────────────────────────────────────
                case ActionType.CastSkill:
                    action.SkillRef = (NewSkillSO)EditorGUILayout.ObjectField("Skill Asset", action.SkillRef, typeof(NewSkillSO), false);
                    if (action.SkillRef != null)
                    {
                        EditorGUILayout.HelpBox(
                            $"Duration: {action.SkillRef.Data.TotalDurationTicks / 480} Beats " +
                            $"(Total {action.SkillRef.Data.TotalDurationTicks} Ticks)", MessageType.Info);
                        action.SkillId = action.SkillRef.Data.SkillId;
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Assign a NewSkillSO.", MessageType.Warning);
                    }
                    break;

                // ── Move (방향 기반 / Legacy Strategy) ────────────────────
                case ActionType.Move:
                    EditorGUILayout.LabelField("── 방향 기반 이동 (권장) ──", EditorStyles.centeredGreyMiniLabel);
                    action.MoveDirection = (MoveDirection)EditorGUILayout.EnumPopup("Direction", action.MoveDirection);
                    action.MoveDistance  = EditorGUILayout.IntField("Steps (칸수)", action.MoveDistance);

                    if (action.MoveDirection == MoveDirection.TowardTarget
                     || action.MoveDirection == MoveDirection.AwayFromTarget)
                    {
                        DrawTargetDef(action.Target);
                    }

                    if (action.MoveDirection != MoveDirection.None)
                    {
                        EditorGUILayout.HelpBox(
                            $"서버는 매 Beat {action.MoveDistance}칸을 1칸씩 나눠 이동합니다.\n" +
                            "절대 위치 예약이 아니므로 순간이동·대각선이동 없음.",
                            MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("── 전략 기반 이동 (Legacy) ──", EditorStyles.centeredGreyMiniLabel);
                        action.MoveStrategy = (MoveStrategy)EditorGUILayout.EnumPopup("Strategy", action.MoveStrategy);
                        if (action.MoveStrategy == MoveStrategy.Flee)
                            DrawTargetDef(action.Target);
                        EditorGUILayout.HelpBox("Direction을 None 이외로 설정하면 방향 기반으로 전환됩니다.", MessageType.Warning);
                    }
                    break;

                // ── MoveStepToward (Chase) ─────────────────────────────────
                case ActionType.MoveStepToward:
                    DrawTargetDef(action.Target);
                    action.MoveDistance = EditorGUILayout.IntField("Step Count", action.MoveDistance);
                    break;

                // ── Wait ───────────────────────────────────────────────────
                case ActionType.Wait:
                    EditorGUILayout.HelpBox("Wait holds the timeline flow.", MessageType.Info);
                    break;
            }
        }
    }

    private void DrawTargetDef(TargetDef target)
    {
        EditorGUILayout.LabelField("Target Selection", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        target.Type     = (TargetType)EditorGUILayout.EnumPopup("Target Type",  target.Type);
        target.MaxRange = EditorGUILayout.IntField("Search Range", target.MaxRange);
        if (target.Type == TargetType.RandomPlayer)
            EditorGUILayout.HelpBox("Selects a random player within range.", MessageType.None);
        EditorGUI.indentLevel--;
    }

    // ─────────────────────────────────────────────────────
    // Export
    // ─────────────────────────────────────────────────────
    private void ExportPatternToJson(MonsterPatternSO so)
    {
        if (so == null || so.Data == null) return;
        string fileName   = $"{so.Data.MonsterType ?? "Unknown"}.json";
        string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
        string serverPath  = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(projectRoot, "../Server/GameServer/Content/01.Game/Pattern/Json"));

        if (!System.IO.Directory.Exists(serverPath))
        {
            try { System.IO.Directory.CreateDirectory(serverPath); }
            catch { serverPath = "Assets/Export/Pattern/Json"; }
        }

        string path = EditorUtility.SaveFilePanel("Export Pattern JSON", serverPath, fileName, "json");
        if (string.IsNullOrEmpty(path)) return;

        var settings = BatchDataExporter.GetJsonSettings();
        string json  = Newtonsoft.Json.JsonConvert.SerializeObject(so.Data, settings);
        System.IO.File.WriteAllText(path, json);
        UnityEngine.Debug.Log($"Exported to {path}");
    }

    // ─────────────────────────────────────────────────────
    // Right Panel: Visual Timeline
    // ─────────────────────────────────────────────────────
    private void DrawRightPanel()
    {
        if (_selectedSelector == null) return;
        GUILayout.Label("Action Timeline", EditorStyles.boldLabel);
        DrawTimeline(_selectedSelector);
    }

    private float _beatWidth = 40f;
    private const float TRACK_HEIGHT  = 40f;
    private const float HEADER_WIDTH  = 100f;

    private ActionDef _draggingEvt;
    private bool      _isResizing;
    private float     _dragStartMouseX;
    private int       _dragStartBeat;

    private void DrawTimeline(SelectorDef data)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Zoom:", GUILayout.Width(40));
        _beatWidth = EditorGUILayout.Slider(_beatWidth, 20f, 100f, GUILayout.Width(150));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        float totalHeight  = 2 * TRACK_HEIGHT + 50f;
        Rect containerRect = EditorGUILayout.GetControlRect(false, Mathf.Max(totalHeight, 150));
        EditorGUI.DrawRect(containerRect, new Color(0.18f, 0.18f, 0.18f));

        string[] tracks = { "Actions / Skills", "Movement" };
        Rect headerRect = new Rect(containerRect.x, containerRect.y, HEADER_WIDTH, containerRect.height);
        EditorGUI.DrawRect(headerRect, new Color(0.25f, 0.25f, 0.25f));
        for (int i = 0; i < tracks.Length; i++)
        {
            Rect labelRect = new Rect(headerRect.x + 5, headerRect.y + 20 + i * TRACK_HEIGHT + 10, HEADER_WIDTH - 10, 20);
            GUI.Label(labelRect, tracks[i], EditorStyles.boldLabel);
            EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y + 20 + (i + 1) * TRACK_HEIGHT, HEADER_WIDTH, 1), Color.black);
        }

        Rect viewRect    = new Rect(containerRect.x + HEADER_WIDTH, containerRect.y, containerRect.width - HEADER_WIDTH, containerRect.height);
        float contentWidth = Mathf.Max(viewRect.width, 64 * _beatWidth + 100);
        Rect contentRect = new Rect(0, 0, contentWidth, totalHeight);

        _scrollRight = GUI.BeginScrollView(viewRect, _scrollRight, contentRect);
        {
            DrawRuler(contentWidth, 64);
            DrawGridLines(contentWidth, totalHeight, 64);

            if (data.Timeline == null) data.Timeline = new List<ActionDef>();

            for (int i = 0; i < data.Timeline.Count; i++)
            {
                var evt      = data.Timeline[i];
                int trackIdx = (evt.Type == ActionType.Move || evt.Type == ActionType.MoveStepToward) ? 1 : 0;
                float yPos   = 20 + trackIdx * TRACK_HEIGHT;
                Rect trackRect = new Rect(0, yPos, contentWidth, TRACK_HEIGHT);
                DrawEventBox(evt, trackRect, i);
            }

            if (Event.current.type == EventType.ContextClick)
            {
                var mousePos = Event.current.mousePosition;
                if (contentRect.Contains(mousePos))
                {
                    int clickedBeat = Mathf.FloorToInt(mousePos.x / _beatWidth);
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent($"Add Skill at Beat {clickedBeat}"), false, () =>
                    {
                        var a = new ActionDef { Type = ActionType.CastSkill, AtBeatOffset = clickedBeat };
                        data.Timeline.Add(a); _selectedAction = a;
                    });
                    menu.AddItem(new GUIContent($"Add Move at Beat {clickedBeat}"), false, () =>
                    {
                        var a = new ActionDef { Type = ActionType.Move, AtBeatOffset = clickedBeat };
                        data.Timeline.Add(a); _selectedAction = a;
                    });
                    menu.AddItem(new GUIContent($"Add Wait at Beat {clickedBeat}"), false, () =>
                    {
                        var a = new ActionDef { Type = ActionType.Wait, AtBeatOffset = clickedBeat };
                        data.Timeline.Add(a); _selectedAction = a;
                    });
                    menu.ShowAsContext();
                    Event.current.Use();
                }
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                _selectedAction = null;
                GUI.FocusControl(null);
                Event.current.Use();
                Repaint();
            }
        }
        GUI.EndScrollView();
    }

    private void DrawRuler(float width, int totalBeats)
    {
        Rect rulerRect = new Rect(0, 0, width, 20);
        EditorGUI.DrawRect(rulerRect, new Color(0.15f, 0.15f, 0.15f));
        Handles.color = Color.gray;
        Handles.DrawLine(new Vector3(0, 20), new Vector3(width, 20));
        for (int b = 0; b < totalBeats; b++)
        {
            float x = b * _beatWidth;
            if (b % 4 == 0)
            {
                GUI.Label(new Rect(x + 2, 0, 30, 20), b.ToString(), EditorStyles.miniLabel);
                Handles.DrawLine(new Vector3(x, 10), new Vector3(x, 20));
            }
            else
            {
                Handles.DrawLine(new Vector3(x, 15), new Vector3(x, 20));
            }
        }
    }

    private void DrawGridLines(float width, float height, int totalBeats)
    {
        Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.1f);
        for (int b = 0; b < totalBeats; b++)
        {
            float x = b * _beatWidth;
            Handles.DrawLine(new Vector3(x, 20), new Vector3(x, height));
        }
    }

    private void DrawEventBox(ActionDef evt, Rect trackRect, int index)
    {
        float x = evt.AtBeatOffset * _beatWidth;
        int durationVisual = 1;
        if (evt.Type == ActionType.CastSkill && evt.SkillRef != null)
            durationVisual = Mathf.Max(1, evt.SkillRef.Data.TotalDurationTicks / 480);

        float w    = durationVisual * _beatWidth;
        Rect evtRect = new Rect(x, trackRect.y + 2, w - 2, trackRect.height - 4);

        Color c = (evt.Type == ActionType.Move || evt.Type == ActionType.MoveStepToward)
            ? new Color(1f, 0.5f, 0f)
            : new Color(0f, 0.8f, 0.4f);
        if (_selectedAction == evt) c = Color.cyan;
        EditorGUI.DrawRect(evtRect, c);

        // 레이블: Move는 방향 화살표 표시
        string label = evt.Type.ToString();
        if (evt.Type == ActionType.CastSkill && evt.SkillRef != null)
            label = evt.SkillRef.name;
        else if ((evt.Type == ActionType.Move || evt.Type == ActionType.MoveStepToward)
                 && evt.MoveDirection != MoveDirection.None)
            label = $"{DirectionArrow(evt.MoveDirection)}x{evt.MoveDistance}";

        GUI.Label(evtRect, label, EditorStyles.whiteMiniLabel);
        HandleEventInput(evt, evtRect);
    }

    private void HandleEventInput(ActionDef evt, Rect evtRect)
    {
        Event e = Event.current;
        if (evtRect.Contains(e.mousePosition))
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                _draggingEvt     = evt;
                _selectedAction  = evt;
                _dragStartMouseX = e.mousePosition.x;
                _dragStartBeat   = evt.AtBeatOffset;
                e.Use();
                GUI.FocusControl(null);
            }
            else if (e.type == EventType.ContextClick)
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Delete"), false, () =>
                {
                    _selectedSelector.Timeline.Remove(evt);
                    if (_selectedAction == evt) _selectedAction = null;
                });
                menu.ShowAsContext();
                e.Use();
            }
        }

        if (e.type == EventType.MouseDrag && _draggingEvt == evt)
        {
            float deltaX  = e.mousePosition.x - _dragStartMouseX;
            int beatDelta = Mathf.RoundToInt(deltaX / _beatWidth);
            evt.AtBeatOffset = Mathf.Max(0, _dragStartBeat + beatDelta);
            e.Use();
            GUI.changed = true;
        }
        else if (e.type == EventType.MouseUp && _draggingEvt == evt)
        {
            _draggingEvt = null;
            e.Use();
        }
    }

    // ─────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────
    private void DrawSplitter()
    {
        GUILayout.Box("", GUILayout.Width(1), GUILayout.ExpandHeight(true));
    }

    /// <summary>MoveDirection을 타임라인 블록용 화살표 문자로 변환</summary>
    private static string DirectionArrow(MoveDirection dir) => dir switch
    {
        MoveDirection.Up            => "↑",
        MoveDirection.Down          => "↓",
        MoveDirection.Left          => "←",
        MoveDirection.Right         => "→",
        MoveDirection.TowardTarget  => "→T",
        MoveDirection.AwayFromTarget=> "←T",
        _                           => "?",
    };

    private void CreateNewPatternAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject("Save Monster Pattern", "NewMonsterPattern", "asset", "Save Monster Pattern");
        if (string.IsNullOrEmpty(path)) return;
        var asset = CreateInstance<MonsterPatternSO>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        _currentPattern = asset;
    }
}
