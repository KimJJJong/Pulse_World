//#if UNITY_EDITOR
//using UnityEditor;
//using UnityEditorInternal;
//using UnityEngine;
//using System.Linq;

//public sealed class PatternEditorWindow : EditorWindow
//{
//    [SerializeField] private MonsterPatternsAsset _patternsAsset;
//    [SerializeField] private SkillsAsset _skillsAsset;

//    private ReorderableList _monsters;
//    private ReorderableList _phases;
//    private ReorderableList _selectors;
//    private ReorderableList _timeline;

//    private int _mapW = 10;
//    private int _mapH = 10;

//    private Vector2 _scroll;


//    [MenuItem("RhythmRPG/Editors/Pattern Editor")]
//    public static void Open() => GetWindow<PatternEditorWindow>("Pattern Editor");

//    private void OnEnable()
//    {
//        if (_patternsAsset != null)
//            BuildMonstersList();
//    }

//    private void OnGUI()
//    {
//        _scroll = EditorGUILayout.BeginScrollView(_scroll);

//        EditorGUILayout.Space(6);

//        _patternsAsset = (MonsterPatternsAsset)EditorGUILayout.ObjectField("Patterns Asset", _patternsAsset, typeof(MonsterPatternsAsset), false);
//        _skillsAsset = (SkillsAsset)EditorGUILayout.ObjectField("Skills Asset", _skillsAsset, typeof(SkillsAsset), false);

//        using (new EditorGUILayout.HorizontalScope())
//        {
//            _mapW = EditorGUILayout.IntField("MapW", _mapW);
//            _mapH = EditorGUILayout.IntField("MapH", _mapH);
//        }

//        if (_patternsAsset == null)
//        {
//            EditorGUILayout.HelpBox("MonsterPatternsAsset를 지정해줘.", MessageType.Info);
//            EditorGUILayout.EndScrollView();
//            return;
//        }

//        if (_monsters == null) BuildMonstersList();

//        EditorGUI.BeginChangeCheck();

//        EditorGUILayout.Space(8);
//        _monsters.DoLayoutList();

//        var selectedMonster = GetSelectedMonster();
//        if (selectedMonster != null)
//        {
//            EditorGUILayout.Space(10);
//            DrawMonsterDetail(selectedMonster);
//        }

//        if (EditorGUI.EndChangeCheck())
//            EditorUtility.SetDirty(_patternsAsset);

//        EditorGUILayout.Space(10);
//        using (new EditorGUILayout.HorizontalScope())
//        {
//            if (GUILayout.Button("Validate + Export patterns.json"))
//            {
//                if (PatternValidation.TryAutoFixAndValidate(_patternsAsset.Data, _skillsAsset, out var report, out var hasErrors))
//                {
//                    EditorUtility.SetDirty(_patternsAsset);

//                    if (hasErrors)
//                    {
//                        EditorUtility.DisplayDialog("Validation Failed", report, "OK");
//                    }
//                    else
//                    {
//                        EditorUtility.DisplayDialog("Validation OK", report, "OK");
//                        EditorCommon.ExportJson(_patternsAsset.Data, "patterns.json");
//                    }
//                }
//            }

//            if (GUILayout.Button("Rebuild Lists"))
//                BuildMonstersList();
//        }

//        EditorGUILayout.EndScrollView();
//    }



//    // -------------------------
//    // List Builders
//    // -------------------------

//    private void BuildMonstersList()
//    {
//        var list = _patternsAsset.Data.Monsters;
//        _monsters = new ReorderableList(list, typeof(MonsterPatternDef), true, true, true, true);

//        _monsters.drawHeaderCallback = r => EditorGUI.LabelField(r, "Monsters");
//        _monsters.onAddCallback = _ =>
//        {
//            list.Add(new MonsterPatternDef { MonsterType = "NewMonster", DefaultPhase = "P1" });
//            _monsters.index = list.Count - 1;
//            BuildPhaseList();
//            EditorUtility.SetDirty(_patternsAsset);
//        };
//        _monsters.onRemoveCallback = _ =>
//        {
//            if (_monsters.index >= 0 && _monsters.index < list.Count)
//                list.RemoveAt(_monsters.index);
//            _monsters.index = Mathf.Clamp(_monsters.index, 0, list.Count - 1);
//            BuildPhaseList();
//            EditorUtility.SetDirty(_patternsAsset);
//        };
//        _monsters.onSelectCallback = _ => BuildPhaseList();

//        _monsters.drawElementCallback = (rect, index, active, focused) =>
//        {
//            var m = list[index];
//            rect.y += 2;
//            m.MonsterType = EditorGUI.TextField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), m.MonsterType);
//        };

//        BuildPhaseList();
//    }

//    private void BuildPhaseList()
//    {
//        var monster = GetSelectedMonster();
//        if (monster == null) { _phases = null; _selectors = null; _timeline = null; return; }

//        var list = monster.Phases;
//        _phases = new ReorderableList(list, typeof(PhaseDef), true, true, true, true);

//        _phases.drawHeaderCallback = r => EditorGUI.LabelField(r, "Phases");
//        _phases.onAddCallback = _ =>
//        {
//            list.Add(new PhaseDef { Id = $"P{list.Count + 1}" });
//            _phases.index = list.Count - 1;
//            BuildSelectorList();
//            EditorUtility.SetDirty(_patternsAsset);
//        };
//        _phases.onRemoveCallback = _ =>
//        {
//            if (_phases.index >= 0 && _phases.index < list.Count)
//                list.RemoveAt(_phases.index);
//            _phases.index = Mathf.Clamp(_phases.index, 0, list.Count - 1);
//            BuildSelectorList();
//            EditorUtility.SetDirty(_patternsAsset);
//        };
//        _phases.onSelectCallback = _ => BuildSelectorList();

//        _phases.drawElementCallback = (rect, index, active, focused) =>
//        {
//            var p = list[index];
//            rect.y += 2;
//            p.Id = EditorGUI.TextField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), p.Id);
//        };

//        BuildSelectorList();
//    }

//    private void BuildSelectorList()
//    {
//        var phase = GetSelectedPhase();
//        if (phase == null) { _selectors = null; _timeline = null; return; }

//        var list = phase.Selectors;
//        _selectors = new ReorderableList(list, typeof(SelectorDef), true, true, true, true);

//        _selectors.drawHeaderCallback = r => EditorGUI.LabelField(r, "Selectors");
//        _selectors.onAddCallback = _ =>
//        {
//            list.Add(new SelectorDef { Id = $"S{list.Count + 1}", Weight = 1, CooldownBeats = 0 });
//            _selectors.index = list.Count - 1;
//            BuildTimelineList();
//            EditorUtility.SetDirty(_patternsAsset);
//        };
//        _selectors.onRemoveCallback = _ =>
//        {
//            if (_selectors.index >= 0 && _selectors.index < list.Count)
//                list.RemoveAt(_selectors.index);
//            _selectors.index = Mathf.Clamp(_selectors.index, 0, list.Count - 1);
//            BuildTimelineList();
//            EditorUtility.SetDirty(_patternsAsset);
//        };
//        _selectors.onSelectCallback = _ => BuildTimelineList();

//        _selectors.elementHeight = EditorGUIUtility.singleLineHeight * 2 + 6;
//        _selectors.drawElementCallback = (rect, index, active, focused) =>
//        {
//            var s = list[index];
//            rect.y += 2;

//            var r0 = new Rect(rect.x, rect.y, rect.width * 0.45f, EditorGUIUtility.singleLineHeight);
//            var r1 = new Rect(rect.x + rect.width * 0.47f, rect.y, rect.width * 0.25f, EditorGUIUtility.singleLineHeight);
//            var r2 = new Rect(rect.x + rect.width * 0.74f, rect.y, rect.width * 0.26f, EditorGUIUtility.singleLineHeight);

//            s.Id = EditorGUI.TextField(r0, s.Id);
//            s.Weight = EditorGUI.IntField(r1, s.Weight);
//            s.CooldownBeats = EditorGUI.IntField(r2, s.CooldownBeats);
//        };

//        BuildTimelineList();
//    }

//    private void BuildTimelineList()
//    {
//        var sel = GetSelectedSelector();
//        if (sel == null) { _timeline = null; return; }

//        var list = sel.Timeline;
//        _timeline = new ReorderableList(list, typeof(ActionDef), true, true, true, true);

//        _timeline.drawHeaderCallback = r => EditorGUI.LabelField(r, "Timeline (Actions)");
//        _timeline.onAddCallback = _ =>
//        {
//            list.Add(new ActionDef { AtBeatOffset = 0, Type = ActionType.Wait });
//            _timeline.index = list.Count - 1;
//            EditorUtility.SetDirty(_patternsAsset);
//        };
//        _timeline.onRemoveCallback = _ =>
//        {
//            if (_timeline.index >= 0 && _timeline.index < list.Count)
//                list.RemoveAt(_timeline.index);
//            _timeline.index = Mathf.Clamp(_timeline.index, 0, list.Count - 1);
//            EditorUtility.SetDirty(_patternsAsset);
//        };

//        _timeline.elementHeight = EditorGUIUtility.singleLineHeight * 2 + 10;
//        _timeline.drawElementCallback = (rect, index, active, focused) =>
//        {
//            var a = list[index];
//            rect.y += 2;

//            var r0 = new Rect(rect.x, rect.y, rect.width * 0.2f, EditorGUIUtility.singleLineHeight);
//            var r1 = new Rect(rect.x + rect.width * 0.22f, rect.y, rect.width * 0.25f, EditorGUIUtility.singleLineHeight);
//            var r2 = new Rect(rect.x + rect.width * 0.49f, rect.y, rect.width * 0.25f, EditorGUIUtility.singleLineHeight);
//            var r3 = new Rect(rect.x + rect.width * 0.76f, rect.y, rect.width * 0.24f, EditorGUIUtility.singleLineHeight);

//            a.AtBeatOffset = EditorGUI.IntField(r0, a.AtBeatOffset);
//            a.Type = (ActionType)EditorGUI.EnumPopup(r1, a.Type);
//            a.TelegraphBeats = EditorGUI.IntField(r2, a.TelegraphBeats);
//            a.TelegraphStyleId = (byte)EditorGUI.IntField(r3, a.TelegraphStyleId);
//        };
//    }

//    // -------------------------
//    // Detail UI
//    // -------------------------

//    private void DrawMonsterDetail(MonsterPatternDef m)
//    {
//        m.MonsterType = EditorGUILayout.TextField("MonsterType", m.MonsterType);
//        m.DefaultPhase = EditorGUILayout.TextField("DefaultPhase", m.DefaultPhase);

//        EditorGUILayout.Space(6);
//        if (_phases != null) _phases.DoLayoutList();

//        var phase = GetSelectedPhase();
//        if (phase == null) return;

//        EditorGUILayout.Space(6);
//        if (_selectors != null) _selectors.DoLayoutList();

//        var sel = GetSelectedSelector();
//        if (sel == null) return;

//        // When 조건(간단 버전: All list)
//        EditorGUILayout.Space(8);
//        EditorGUILayout.LabelField("When (All Conditions)", EditorStyles.boldLabel);
//        DrawWhenGroup(sel.When);

//        EditorGUILayout.Space(8);
//        if (_timeline != null) _timeline.DoLayoutList();

//        var act = GetSelectedAction();
//        if (act != null)
//        {
//            EditorGUILayout.Space(10);
//            DrawActionDetail(act);
//        }

//        // Transitions
//        EditorGUILayout.Space(10);
//        EditorGUILayout.LabelField("Phase Transitions", EditorStyles.boldLabel);
//        DrawTransitions(m);
//    }

//    private void DrawWhenGroup(WhenGroup when)
//    {
//        if (when.All == null) when.All = new();

//        for (int i = 0; i < when.All.Count; i++)
//        {
//            using (new EditorGUILayout.HorizontalScope())
//            {
//                when.All[i].Type = (ConditionType)EditorGUILayout.EnumPopup(when.All[i].Type);
//                when.All[i].Value = EditorGUILayout.IntField(when.All[i].Value);

//                if (GUILayout.Button("-", GUILayout.Width(24)))
//                {
//                    when.All.RemoveAt(i);
//                    break;
//                }
//            }
//        }

//        if (GUILayout.Button("+ Add Condition"))
//            when.All.Add(new ConditionDef { Type = ConditionType.DistanceToClosestPlayerLE, Value = 3 });
//    }

//    private void DrawTransitions(MonsterPatternDef m)
//    {
//        if (m.Transitions == null) m.Transitions = new();

//        for (int i = 0; i < m.Transitions.Count; i++)
//        {
//            var t = m.Transitions[i];
//            using (new EditorGUILayout.VerticalScope("box"))
//            {
//                t.FromPhaseId = EditorGUILayout.TextField("From", t.FromPhaseId);
//                t.ToPhaseId = EditorGUILayout.TextField("To", t.ToPhaseId);
//                t.Type = (PhaseTransitionType)EditorGUILayout.EnumPopup("Type", t.Type);
//                t.Value = EditorGUILayout.IntField("Value", t.Value);

//                if (GUILayout.Button("Remove Transition"))
//                {
//                    m.Transitions.RemoveAt(i);
//                    break;
//                }
//            }
//        }

//        if (GUILayout.Button("+ Add Transition"))
//            m.Transitions.Add(new PhaseTransitionDef { FromPhaseId = "P1", ToPhaseId = "P2", Type = PhaseTransitionType.HpPercentLE, Value = 30 });
//    }

//    private void DrawActionDetail(ActionDef a)
//    {
//        EditorGUILayout.LabelField("Action Detail", EditorStyles.boldLabel);

//        a.AtBeatOffset = EditorGUILayout.IntField("AtBeatOffset", a.AtBeatOffset);
//        a.Type = (ActionType)EditorGUILayout.EnumPopup("Type", a.Type);

//        // Target
//        EditorGUILayout.Space(6);
//        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
//        a.Target.Type = (TargetType)EditorGUILayout.EnumPopup("TargetType", a.Target.Type);
//        a.Target.MaxRange = EditorGUILayout.IntField("MaxRange", a.Target.MaxRange);
//        a.Target.RequireAlive = EditorGUILayout.Toggle("RequireAlive", a.Target.RequireAlive);

//        // Attack이면 SkillId 드롭다운
//        if (a.Type == ActionType.Attack)
//        {
//            EditorGUILayout.Space(6);
//            EditorGUILayout.LabelField("Attack", EditorStyles.boldLabel);

//            var ids = SkillEditorWindow.GetSkillIds(_skillsAsset);
//            if (ids.Length == 0)
//            {
//                a.SkillId = EditorGUILayout.TextField("SkillId", a.SkillId);
//                EditorGUILayout.HelpBox("SkillsAsset에 Skill이 없어서 드롭다운을 못 씀.", MessageType.Warning);
//            }
//            else
//            {
//                int idx = System.Array.IndexOf(ids, a.SkillId);
//                if (idx < 0) idx = 0;
//                idx = EditorGUILayout.Popup("SkillId", idx, ids);
//                a.SkillId = ids[idx];
//            }

//            a.TelegraphBeats = EditorGUILayout.IntField("TelegraphBeats", a.TelegraphBeats);
//            a.TelegraphStyleId = (byte)EditorGUILayout.IntField("TelegraphStyleId", a.TelegraphStyleId);
//        }

//        // Area(OriginType + Cells 편집)
//        EditorGUILayout.Space(6);
//        EditorGUILayout.LabelField("Area (Hint / Telegraph)", EditorStyles.boldLabel);
//        a.Area.OriginType = (TelegraphOriginType)EditorGUILayout.EnumPopup("OriginType", a.Area.OriginType);

//        if (a.Area.OriginType == TelegraphOriginType.Point)
//        {
//            a.Area.OriginX = EditorGUILayout.IntField("OriginX", a.Area.OriginX);
//            a.Area.OriginY = EditorGUILayout.IntField("OriginY", a.Area.OriginY);
//        }

//        a.Area.Shape = (TelegraphShape)EditorGUILayout.EnumPopup("Shape", a.Area.Shape);

//        if (a.Area.Shape == TelegraphShape.Cells)
//        {
//            DrawCellsGrid(a.Area);
//        }
//        else
//        {
//            a.Area.ParamA = EditorGUILayout.IntField("ParamA", a.Area.ParamA);
//            a.Area.ParamB = EditorGUILayout.IntField("ParamB", a.Area.ParamB);
//            EditorGUILayout.HelpBox("주의: 서버 판정은 SkillDef가 권위. 여기 Param은 연출/힌트 용도로만 쓰는 걸 추천.", MessageType.Info);
//        }
//    }

//    private void DrawCellsGrid(AreaDef area)
//    {
//        if (area.Cells == null) area.Cells = new();

//        EditorGUILayout.HelpBox("Cells 편집: 클릭해서 셀 토글. Export 시 Cells 그대로 저장됨.", MessageType.Info);

//        int cellSize = 20;
//        int cols = Mathf.Clamp(_mapW, 1, 64);
//        int rows = Mathf.Clamp(_mapH, 1, 64);

//        // 빠른 조회용
//        var set = area.Cells.Select(c => (c.X, c.Y)).ToHashSet();

//        var rect = GUILayoutUtility.GetRect(cols * cellSize, rows * cellSize);
//        GUI.Box(rect, GUIContent.none);

//        for (int y = 0; y < rows; y++)
//            for (int x = 0; x < cols; x++)
//            {
//                var r = new Rect(rect.x + x * cellSize, rect.y + y * cellSize, cellSize, cellSize);
//                bool on = set.Contains((x, y));

//                // 셀 표시(색은 EditorGUIUtility로 처리, 스타일 고정 안함)
//                EditorGUI.DrawRect(r, on ? new Color(0.25f, 0.9f, 0.25f, 0.8f) : new Color(0.2f, 0.2f, 0.2f, 0.2f));
//                GUI.Label(r, "", "box");

//                // 클릭 토글
//                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
//                {
//                    Event.current.Use();
//                    ToggleCell(area, x, y);
//                    Repaint();
//                }
//            }

//        if (GUILayout.Button("Clear Cells"))
//        {
//            area.Cells.Clear();
//        }
//    }

//    private static void ToggleCell(AreaDef area, int x, int y)
//    {
//        for (int i = 0; i < area.Cells.Count; i++)
//        {
//            if (area.Cells[i].X == x && area.Cells[i].Y == y)
//            {
//                area.Cells.RemoveAt(i);
//                return;
//            }
//        }
//        area.Cells.Add(new GridPos(x, y));
//    }

//    // -------------------------
//    // Selection helpers
//    // -------------------------

//    private MonsterPatternDef GetSelectedMonster()
//    {
//        var list = _patternsAsset.Data.Monsters;
//        if (_monsters == null || _monsters.index < 0 || _monsters.index >= list.Count) return null;
//        return list[_monsters.index];
//    }

//    private PhaseDef GetSelectedPhase()
//    {
//        var m = GetSelectedMonster();
//        if (m == null || _phases == null || _phases.index < 0 || _phases.index >= m.Phases.Count) return null;
//        return m.Phases[_phases.index];
//    }

//    private SelectorDef GetSelectedSelector()
//    {
//        var p = GetSelectedPhase();
//        if (p == null || _selectors == null || _selectors.index < 0 || _selectors.index >= p.Selectors.Count) return null;
//        return p.Selectors[_selectors.index];
//    }

//    private ActionDef GetSelectedAction()
//    {
//        var s = GetSelectedSelector();
//        if (s == null || _timeline == null || _timeline.index < 0 || _timeline.index >= s.Timeline.Count) return null;
//        return s.Timeline[_timeline.index];
//    }
//}
//#endif
