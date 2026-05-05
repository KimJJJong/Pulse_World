using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace RhythmRPG.Editor.StageBuilder
{
    public class StageEditorWindow : EditorWindow
    {
        private const string DefaultStageAssetFolder = "Assets/Resources/Data/StageAssets";

        private StageDataSO _currentStage;
        private Vector2 _scrollPos;
        private readonly BoxBoundsHandle _boundsHandle = new BoxBoundsHandle();

        private static readonly Color BasicAccent = new Color(0.42f, 0.57f, 0.86f, 1f);
        private static readonly Color RhythmAccent = new Color(0.26f, 0.70f, 0.62f, 1f);
        private static readonly Color RegistryAccent = new Color(0.90f, 0.63f, 0.24f, 1f);
        private static readonly Color SpawnAccent = new Color(0.34f, 0.74f, 0.43f, 1f);
        private static readonly Color ObjectAccent = new Color(0.72f, 0.46f, 0.93f, 1f);
        private static readonly Color EventAccent = new Color(0.67f, 0.44f, 0.93f, 1f);
        private static readonly Color ConditionAccent = new Color(0.32f, 0.68f, 0.96f, 1f);
        private static readonly Color ActionAccent = new Color(0.96f, 0.58f, 0.29f, 1f);

        [MenuItem("RhythmRPG/Editors/Content/Stage Editor")]
        public static void ShowWindow()
        {
            GetWindow<StageEditorWindow>("Stage Editor");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            GUILayout.Label("RhythmRPG Stage Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("이벤트는 '이름 -> 조건 -> 액션' 순서로 읽히게 구성하면 관리가 훨씬 쉬워집니다.", MessageType.Info);
            EditorGUILayout.Space();

            _currentStage = (StageDataSO)EditorGUILayout.ObjectField("Stage Data", _currentStage, typeof(StageDataSO), false);

            if (_currentStage == null)
            {
                EditorGUILayout.HelpBox("Select a Stage Data ScriptableObject to edit.", MessageType.Info);
                if (GUILayout.Button("Create New Stage Data"))
                {
                    CreateNewStageData();
                }
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Export JSON", GUILayout.Height(30)))
            {
                StageExporter.Export(_currentStage);
                EditorUtility.DisplayDialog("Export Success", $"Exported {_currentStage.MapId}.json", "OK");
            }

            if (GUILayout.Button("Ping File", GUILayout.Width(100), GUILayout.Height(30)))
            {
                EditorGUIUtility.PingObject(_currentStage);
            }

            if (GUILayout.Button("Normalize Event IDs", GUILayout.Width(150), GUILayout.Height(30)))
            {
                SerializedObject normalizeSo = new SerializedObject(_currentStage);
                normalizeSo.Update();
                NormalizeEventIds(normalizeSo.FindProperty("Events"));
                normalizeSo.ApplyModifiedProperties();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            DrawLine();
            EditorGUILayout.Space();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            SerializedObject so = new SerializedObject(_currentStage);
            so.Update();

            DrawBasicInfo(so);
            DrawRhythmSettings(so);
            DrawRegistry(so);
            DrawSpawnLists(so);
            DrawEventSection(so);

            so.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();
        }

        private void DrawBasicInfo(SerializedObject so)
        {
            DrawSectionHeader("Basic Info", "Stage identity and editor-facing description.", BasicAccent);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(so.FindProperty("MapId"));
            EditorGUILayout.PropertyField(so.FindProperty("Description"));
            EditorGUILayout.PropertyField(so.FindProperty("MapPrefab"));
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawRhythmSettings(SerializedObject so)
        {
            DrawSectionHeader("Rhythm Settings", "Song timing and action-window tuning.", RhythmAccent);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(so.FindProperty("Rhythm"));
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawRegistry(SerializedObject so)
        {
            DrawSectionHeader("Entity Registry", "Keys used by events and spawn data.", RegistryAccent);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(so.FindProperty("Registry"), true);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawSpawnLists(SerializedObject so)
        {
            DrawSectionHeader("Initial Spawns", "Monsters placed at stage start.", SpawnAccent);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(so.FindProperty("InitialSpawns"), true);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            DrawSectionHeader("Initial Objects", "Gates, traps, interactables, and helpers.", ObjectAccent);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(so.FindProperty("InitialObjects"), true);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawEventSection(SerializedObject so)
        {
            SerializedProperty eventsProp = so.FindProperty("Events");

            DrawSectionHeader("Events", "Conditions and actions are grouped here.", EventAccent);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawEventQuickAddRow(eventsProp, so);

            EditorGUILayout.HelpBox(
                "권장 구조: 하나의 이벤트는 '무엇이 발생하면'을 조건으로, '무엇을 실행할지'를 액션으로 작성합니다. 이름과 메모를 붙이면 큰 스테이지에서도 훨씬 찾기 쉽습니다.",
                MessageType.None);

            if (eventsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("이벤트가 비어 있습니다. + Event로 첫 이벤트를 추가하세요.", MessageType.Info);
            }
            else if (HasDuplicateEventIds(eventsProp))
            {
                EditorGUILayout.HelpBox("중복된 Event ID가 있습니다. ID 정리를 눌러 순서대로 다시 맞추는 것을 권장합니다.", MessageType.Warning);
            }

            for (int i = 0; i < eventsProp.arraySize; i++)
            {
                DrawEventCard(eventsProp, i);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawEventCard(SerializedProperty eventsProp, int index)
        {
            SerializedProperty evtProp = eventsProp.GetArrayElementAtIndex(index);
            SerializedProperty titleProp = evtProp.FindPropertyRelative("Title");
            SerializedProperty notesProp = evtProp.FindPropertyRelative("Notes");
            SerializedProperty enabledProp = evtProp.FindPropertyRelative("Enabled");
            SerializedProperty idProp = evtProp.FindPropertyRelative("EventId");
            SerializedProperty oneShotProp = evtProp.FindPropertyRelative("IsOneShot");
            SerializedProperty conditionsProp = evtProp.FindPropertyRelative("Conditions");
            SerializedProperty actionsProp = evtProp.FindPropertyRelative("Actions");

            string displayTitle = GetEventDisplayTitle(evtProp);
            string conditionSummary = GetConditionSummary(conditionsProp);
            string actionSummary = GetActionSummary(actionsProp);
            Color eventCardAccent = enabledProp.boolValue ? EventAccent : new Color(0.55f, 0.55f, 0.55f, 1f);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawCardAccent(eventCardAccent);

            EditorGUILayout.BeginHorizontal();
            evtProp.isExpanded = EditorGUILayout.Foldout(evtProp.isExpanded, $"{idProp.intValue}. {displayTitle}", true);
            GUILayout.FlexibleSpace();
            using (new GUIBackgroundColorScope(enabledProp.boolValue ? SpawnAccent : new Color(0.62f, 0.62f, 0.62f, 1f)))
            {
                enabledProp.boolValue = GUILayout.Toggle(enabledProp.boolValue, enabledProp.boolValue ? "Enabled" : "Disabled", EditorStyles.miniButton, GUILayout.Width(78));
            }

            if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(24)) && index > 0)
            {
                eventsProp.MoveArrayElement(index, index - 1);
            }

            if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(24)) && index < eventsProp.arraySize - 1)
            {
                eventsProp.MoveArrayElement(index, index + 1);
            }

            if (GUILayout.Button("Duplicate", EditorStyles.miniButtonMid, GUILayout.Width(72)))
            {
                DuplicateEvent(eventsProp, index);
            }

            if (GUILayout.Button("Delete", EditorStyles.miniButtonRight, GUILayout.Width(60)))
            {
                DeleteEvent(eventsProp, index);
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"{conditionSummary}  ->  {actionSummary}", EditorStyles.miniLabel);

            if (evtProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                DrawSubHeader("Event Settings", BasicAccent, "Name, memo, and life-cycle");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(titleProp, new GUIContent("Title"));
                EditorGUILayout.PropertyField(notesProp, new GUIContent("Notes"));
                EditorGUILayout.PropertyField(idProp, new GUIContent("Event ID"));
                EditorGUILayout.PropertyField(oneShotProp, new GUIContent("One Shot"));
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(2);
                DrawConditionsSection(conditionsProp);

                EditorGUILayout.Space(4);
                DrawActionsSection(actionsProp);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawConditionsSection(SerializedProperty conditionsProp)
        {
            DrawSubHeader("Conditions", ConditionAccent, "When this event becomes valid");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawConditionQuickAddRow(conditionsProp);

            if (conditionsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("조건이 비어 있습니다. 하나 이상의 조건을 추가하세요.", MessageType.Info);
            }

            for (int i = 0; i < conditionsProp.arraySize; i++)
            {
                DrawConditionRow(conditionsProp, i);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActionsSection(SerializedProperty actionsProp)
        {
            DrawSubHeader("Actions", ActionAccent, "What happens when the conditions pass");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawActionQuickAddRow(actionsProp);

            if (actionsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("액션이 비어 있습니다. 하나 이상의 액션을 추가하세요.", MessageType.Info);
            }

            for (int i = 0; i < actionsProp.arraySize; i++)
            {
                DrawActionRow(actionsProp, i);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawConditionRow(SerializedProperty conditionsProp, int index)
        {
            SerializedProperty condProp = conditionsProp.GetArrayElementAtIndex(index);
            SerializedProperty typeProp = condProp.FindPropertyRelative("Type");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"{index + 1}. {GetConditionLabel(condProp)}", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(24)) && index > 0)
            {
                conditionsProp.MoveArrayElement(index, index - 1);
            }

            if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(24)) && index < conditionsProp.arraySize - 1)
            {
                conditionsProp.MoveArrayElement(index, index + 1);
            }

            if (GUILayout.Button("X", EditorStyles.miniButtonRight, GUILayout.Width(24)))
            {
                conditionsProp.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(typeProp, new GUIContent("Type"));

            ConditionType type = (ConditionType)typeProp.enumValueIndex;
            switch (type)
            {
                case ConditionType.MonsterAllDead:
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("TargetId"), new GUIContent("Target Group ID"));
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("Count"), new GUIContent("Required Kills"));
                    EditorGUILayout.HelpBox("Target Group ID가 모두 전멸하면 발동합니다.", MessageType.None);
                    break;

                case ConditionType.AreaEnter:
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("Area"), new GUIContent("Area (X/Z)"));
                    EditorGUILayout.HelpBox("월드 X/Z 영역에 진입하면 발동합니다.", MessageType.None);
                    break;

                case ConditionType.TimeElapsed:
                    SerializedProperty timeProp = condProp.FindPropertyRelative("Count");
                    EditorGUILayout.PropertyField(timeProp, new GUIContent("Elapsed Time (ms)"));
                    EditorGUILayout.LabelField("Preview", $"{timeProp.intValue / 1000f:0.0}s");
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActionRow(SerializedProperty actionsProp, int index)
        {
            SerializedProperty actionProp = actionsProp.GetArrayElementAtIndex(index);
            SerializedProperty typeProp = actionProp.FindPropertyRelative("Type");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"{index + 1}. {GetActionLabel(actionProp)}", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(24)) && index > 0)
            {
                actionsProp.MoveArrayElement(index, index - 1);
            }

            if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(24)) && index < actionsProp.arraySize - 1)
            {
                actionsProp.MoveArrayElement(index, index + 1);
            }

            if (GUILayout.Button("X", EditorStyles.miniButtonRight, GUILayout.Width(24)))
            {
                actionsProp.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(typeProp, new GUIContent("Type"));

            ActionType type = (ActionType)typeProp.enumValueIndex;
            switch (type)
            {
                case ActionType.SpawnMonster:
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("HeaderParam"), new GUIContent("Registry Key"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("ParamId"), new GUIContent("Monster ID"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("Position"), new GUIContent("Spawn Position"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("StringVal"), new GUIContent("AI Pattern"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("GroupId"), new GUIContent("Group ID"));
                    EditorGUILayout.HelpBox("Registry Key가 있으면 Export 시 Monster ID와 AI Pattern이 자동으로 채워집니다.", MessageType.None);
                    break;

                case ActionType.Broadcast:
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("StringVal"), new GUIContent("Message"));
                    break;

                case ActionType.OpenGate:
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("Position"), new GUIContent("Gate Position"));
                    break;

                case ActionType.ReturnToTown:
                    EditorGUILayout.HelpBox("추가 파라미터가 필요 없는 종료 액션입니다.", MessageType.None);
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawSectionHeader(string title, string description, Color accent)
        {
            Rect headerRect = EditorGUILayout.GetControlRect(false, 22f);
            EditorGUI.DrawRect(headerRect, new Color(accent.r, accent.g, accent.b, 0.18f));
            EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 4f, headerRect.height), accent);

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = accent },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 6, 2, 2)
            };
            GUI.Label(headerRect, title, style);

            if (!string.IsNullOrWhiteSpace(description))
            {
                GUIStyle descStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(1f, 1f, 1f, 0.72f) },
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(12, 6, 0, 4)
                };

                Rect descRect = EditorGUILayout.GetControlRect(false, 15f);
                GUI.Label(descRect, description, descStyle);
            }
        }

        private static void DrawSubHeader(string title, Color accent, string description = null)
        {
            Rect headerRect = EditorGUILayout.GetControlRect(false, 18f);
            EditorGUI.DrawRect(headerRect, new Color(accent.r, accent.g, accent.b, 0.12f));
            EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 3f, headerRect.height), accent);

            GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = accent },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 4, 1, 1)
            };
            GUI.Label(headerRect, title, style);

            if (!string.IsNullOrWhiteSpace(description))
            {
                GUIStyle descStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(1f, 1f, 1f, 0.68f) },
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(10, 4, 0, 3)
                };

                Rect descRect = EditorGUILayout.GetControlRect(false, 14f);
                GUI.Label(descRect, description, descStyle);
            }
        }

        private static void DrawCardAccent(Color accent)
        {
            Rect accentRect = EditorGUILayout.GetControlRect(false, 4f);
            EditorGUI.DrawRect(accentRect, accent);
        }

        private static bool ColoredButton(string label, Color accent, params GUILayoutOption[] options)
        {
            using (new GUIBackgroundColorScope(accent))
            {
                return GUILayout.Button(label, options);
            }
        }

        private struct GUIBackgroundColorScope : IDisposable
        {
            private readonly Color _previousBackground;

            public GUIBackgroundColorScope(Color backgroundColor)
            {
                _previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = backgroundColor;
            }

            public void Dispose()
            {
                GUI.backgroundColor = _previousBackground;
            }
        }

        private enum EventQuickTemplate
        {
            Empty,
            MonsterClearSpawn,
            AreaBroadcast,
            TimeReturnTown
        }

        private enum ConditionQuickTemplate
        {
            MonsterAllDead,
            AreaEnter,
            TimeElapsed
        }

        private enum ActionQuickTemplate
        {
            Broadcast,
            SpawnMonster,
            OpenGate,
            ReturnToTown
        }

        private void DrawEventQuickAddRow(SerializedProperty eventsProp, SerializedObject so)
        {
            EditorGUILayout.BeginHorizontal();
            if (ColoredButton("+ Empty Event", BasicAccent, GUILayout.Width(110)))
            {
                AddEvent(eventsProp, EventQuickTemplate.Empty);
                so.ApplyModifiedProperties();
            }

            if (ColoredButton("Monster Clear", SpawnAccent, GUILayout.Width(110)))
            {
                AddEvent(eventsProp, EventQuickTemplate.MonsterClearSpawn);
                so.ApplyModifiedProperties();
            }

            if (ColoredButton("Area Trigger", ConditionAccent, GUILayout.Width(100)))
            {
                AddEvent(eventsProp, EventQuickTemplate.AreaBroadcast);
                so.ApplyModifiedProperties();
            }

            if (ColoredButton("Timer End", ActionAccent, GUILayout.Width(90)))
            {
                AddEvent(eventsProp, EventQuickTemplate.TimeReturnTown);
                so.ApplyModifiedProperties();
            }

            GUILayout.FlexibleSpace();

            if (ColoredButton("ID 정리", RegistryAccent, GUILayout.Width(80)))
            {
                NormalizeEventIds(eventsProp);
                so.ApplyModifiedProperties();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private void DrawConditionQuickAddRow(SerializedProperty conditionsProp)
        {
            EditorGUILayout.BeginHorizontal();
            if (ColoredButton("+ Dead", SpawnAccent, GUILayout.Width(70)))
            {
                AddCondition(conditionsProp, ConditionQuickTemplate.MonsterAllDead);
            }

            if (ColoredButton("+ Area", ConditionAccent, GUILayout.Width(70)))
            {
                AddCondition(conditionsProp, ConditionQuickTemplate.AreaEnter);
            }

            if (ColoredButton("+ Time", ActionAccent, GUILayout.Width(70)))
            {
                AddCondition(conditionsProp, ConditionQuickTemplate.TimeElapsed);
            }

            GUILayout.FlexibleSpace();

            if (ColoredButton("+ Condition", BasicAccent, GUILayout.Width(100)))
            {
                AddCondition(conditionsProp, ConditionQuickTemplate.MonsterAllDead);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        private void DrawActionQuickAddRow(SerializedProperty actionsProp)
        {
            EditorGUILayout.BeginHorizontal();
            if (ColoredButton("+ Spawn", SpawnAccent, GUILayout.Width(80)))
            {
                AddAction(actionsProp, ActionQuickTemplate.SpawnMonster);
            }

            if (ColoredButton("+ Msg", BasicAccent, GUILayout.Width(70)))
            {
                AddAction(actionsProp, ActionQuickTemplate.Broadcast);
            }

            if (ColoredButton("+ Gate", ActionAccent, GUILayout.Width(70)))
            {
                AddAction(actionsProp, ActionQuickTemplate.OpenGate);
            }

            if (ColoredButton("+ Town", RegistryAccent, GUILayout.Width(70)))
            {
                AddAction(actionsProp, ActionQuickTemplate.ReturnToTown);
            }

            GUILayout.FlexibleSpace();

            if (ColoredButton("+ Action", EventAccent, GUILayout.Width(90)))
            {
                AddAction(actionsProp, ActionQuickTemplate.Broadcast);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        private void AddEvent(SerializedProperty eventsProp)
        {
            AddEvent(eventsProp, EventQuickTemplate.Empty);
        }

        private void AddEvent(SerializedProperty eventsProp, EventQuickTemplate template)
        {
            int insertIndex = eventsProp.arraySize;
            eventsProp.InsertArrayElementAtIndex(insertIndex);
            int nextId = GetNextEventId(eventsProp);

            SerializedProperty evtProp = eventsProp.GetArrayElementAtIndex(insertIndex);
            ResetEvent(evtProp, nextId);

            switch (template)
            {
                case EventQuickTemplate.MonsterClearSpawn:
                    evtProp.FindPropertyRelative("Title").stringValue = $"Wave {nextId} Clear";
                    AddCondition(evtProp.FindPropertyRelative("Conditions"), ConditionQuickTemplate.MonsterAllDead);
                    AddAction(evtProp.FindPropertyRelative("Actions"), ActionQuickTemplate.SpawnMonster);
                    break;

                case EventQuickTemplate.AreaBroadcast:
                    evtProp.FindPropertyRelative("Title").stringValue = $"Area {nextId}";
                    AddCondition(evtProp.FindPropertyRelative("Conditions"), ConditionQuickTemplate.AreaEnter);
                    AddAction(evtProp.FindPropertyRelative("Actions"), ActionQuickTemplate.Broadcast);
                    break;

                case EventQuickTemplate.TimeReturnTown:
                    evtProp.FindPropertyRelative("Title").stringValue = $"Timer {nextId}";
                    AddCondition(evtProp.FindPropertyRelative("Conditions"), ConditionQuickTemplate.TimeElapsed);
                    AddAction(evtProp.FindPropertyRelative("Actions"), ActionQuickTemplate.ReturnToTown);
                    break;
            }

            evtProp.isExpanded = true;
        }

        private void DuplicateEvent(SerializedProperty eventsProp, int index)
        {
            eventsProp.InsertArrayElementAtIndex(index + 1);
            int nextId = GetNextEventId(eventsProp);
            SerializedProperty duplicated = eventsProp.GetArrayElementAtIndex(index + 1);
            duplicated.FindPropertyRelative("EventId").intValue = nextId;
            duplicated.FindPropertyRelative("Title").stringValue = $"{GetEventDisplayTitle(eventsProp.GetArrayElementAtIndex(index))} Copy";
            duplicated.isExpanded = true;
        }

        private void DeleteEvent(SerializedProperty eventsProp, int index)
        {
            eventsProp.DeleteArrayElementAtIndex(index);
        }

        private static void ResetEvent(SerializedProperty evtProp, int eventId)
        {
            evtProp.FindPropertyRelative("Title").stringValue = $"Event {eventId}";
            evtProp.FindPropertyRelative("Notes").stringValue = string.Empty;
            evtProp.FindPropertyRelative("Enabled").boolValue = true;
            evtProp.FindPropertyRelative("EventId").intValue = eventId;
            evtProp.FindPropertyRelative("IsOneShot").boolValue = true;
            evtProp.FindPropertyRelative("Conditions").arraySize = 0;
            evtProp.FindPropertyRelative("Actions").arraySize = 0;
        }

        private static void AddCondition(SerializedProperty conditionsProp)
        {
            AddCondition(conditionsProp, ConditionQuickTemplate.MonsterAllDead);
        }

        private static void AddCondition(SerializedProperty conditionsProp, ConditionQuickTemplate template)
        {
            int insertIndex = conditionsProp.arraySize;
            conditionsProp.InsertArrayElementAtIndex(insertIndex);

            SerializedProperty condProp = conditionsProp.GetArrayElementAtIndex(insertIndex);
            condProp.FindPropertyRelative("Area").rectIntValue = new RectInt(0, 0, 1, 1);

            switch (template)
            {
                case ConditionQuickTemplate.AreaEnter:
                    condProp.FindPropertyRelative("Type").enumValueIndex = (int)ConditionType.AreaEnter;
                    condProp.FindPropertyRelative("TargetId").intValue = 0;
                    condProp.FindPropertyRelative("Count").intValue = 0;
                    break;

                case ConditionQuickTemplate.TimeElapsed:
                    condProp.FindPropertyRelative("Type").enumValueIndex = (int)ConditionType.TimeElapsed;
                    condProp.FindPropertyRelative("TargetId").intValue = 0;
                    condProp.FindPropertyRelative("Count").intValue = 1000;
                    break;

                default:
                    condProp.FindPropertyRelative("Type").enumValueIndex = (int)ConditionType.MonsterAllDead;
                    condProp.FindPropertyRelative("TargetId").intValue = 0;
                    condProp.FindPropertyRelative("Count").intValue = 1;
                    break;
            }
        }

        private static void AddAction(SerializedProperty actionsProp)
        {
            AddAction(actionsProp, ActionQuickTemplate.Broadcast);
        }

        private static void AddAction(SerializedProperty actionsProp, ActionQuickTemplate template)
        {
            int insertIndex = actionsProp.arraySize;
            actionsProp.InsertArrayElementAtIndex(insertIndex);

            SerializedProperty actionProp = actionsProp.GetArrayElementAtIndex(insertIndex);
            actionProp.FindPropertyRelative("HeaderParam").stringValue = string.Empty;
            actionProp.FindPropertyRelative("ParamId").intValue = 0;
            actionProp.FindPropertyRelative("Position").vector3Value = Vector3.zero;
            actionProp.FindPropertyRelative("StringVal").stringValue = string.Empty;
            actionProp.FindPropertyRelative("GroupId").intValue = 0;

            switch (template)
            {
                case ActionQuickTemplate.SpawnMonster:
                    actionProp.FindPropertyRelative("Type").enumValueIndex = (int)ActionType.SpawnMonster;
                    actionProp.FindPropertyRelative("GroupId").intValue = 1;
                    break;

                case ActionQuickTemplate.OpenGate:
                    actionProp.FindPropertyRelative("Type").enumValueIndex = (int)ActionType.OpenGate;
                    break;

                case ActionQuickTemplate.ReturnToTown:
                    actionProp.FindPropertyRelative("Type").enumValueIndex = (int)ActionType.ReturnToTown;
                    break;

                default:
                    actionProp.FindPropertyRelative("Type").enumValueIndex = (int)ActionType.Broadcast;
                    actionProp.FindPropertyRelative("StringVal").stringValue = "Event triggered";
                    break;
            }
        }

        private static int GetNextEventId(SerializedProperty eventsProp)
        {
            int maxId = 0;
            for (int i = 0; i < eventsProp.arraySize; i++)
            {
                SerializedProperty evtProp = eventsProp.GetArrayElementAtIndex(i);
                int eventId = evtProp.FindPropertyRelative("EventId").intValue;
                if (eventId > maxId)
                {
                    maxId = eventId;
                }
            }

            return maxId + 1;
        }

        private static void NormalizeEventIds(SerializedProperty eventsProp)
        {
            for (int i = 0; i < eventsProp.arraySize; i++)
            {
                SerializedProperty evtProp = eventsProp.GetArrayElementAtIndex(i);
                evtProp.FindPropertyRelative("EventId").intValue = i + 1;
                if (string.IsNullOrWhiteSpace(evtProp.FindPropertyRelative("Title").stringValue))
                {
                    evtProp.FindPropertyRelative("Title").stringValue = $"Event {i + 1}";
                }
            }
        }

        private static bool HasDuplicateEventIds(SerializedProperty eventsProp)
        {
            HashSet<int> ids = new HashSet<int>();
            for (int i = 0; i < eventsProp.arraySize; i++)
            {
                int id = eventsProp.GetArrayElementAtIndex(i).FindPropertyRelative("EventId").intValue;
                if (!ids.Add(id))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetEventDisplayTitle(SerializedProperty evtProp)
        {
            int eventId = evtProp.FindPropertyRelative("EventId").intValue;
            string title = evtProp.FindPropertyRelative("Title").stringValue;
            if (string.IsNullOrWhiteSpace(title))
            {
                return $"Event {eventId}";
            }

            return title.Trim();
        }

        private static string GetConditionSummary(SerializedProperty conditionsProp)
        {
            if (conditionsProp.arraySize == 0)
            {
                return "조건 없음";
            }

            string primary = GetConditionLabel(conditionsProp.GetArrayElementAtIndex(0));
            if (conditionsProp.arraySize == 1)
            {
                return primary;
            }

            return $"{primary} 외 {conditionsProp.arraySize - 1}개";
        }

        private static string GetActionSummary(SerializedProperty actionsProp)
        {
            if (actionsProp.arraySize == 0)
            {
                return "액션 없음";
            }

            string primary = GetActionLabel(actionsProp.GetArrayElementAtIndex(0));
            if (actionsProp.arraySize == 1)
            {
                return primary;
            }

            return $"{primary} 외 {actionsProp.arraySize - 1}개";
        }

        private static string GetConditionLabel(SerializedProperty condProp)
        {
            ConditionType type = (ConditionType)condProp.FindPropertyRelative("Type").enumValueIndex;
            switch (type)
            {
                case ConditionType.MonsterAllDead:
                    return $"그룹 {condProp.FindPropertyRelative("TargetId").intValue} 전멸 x{condProp.FindPropertyRelative("Count").intValue}";

                case ConditionType.AreaEnter:
                    RectInt area = condProp.FindPropertyRelative("Area").rectIntValue;
                    return $"영역 진입 ({area.x}, {area.y}, {area.width}, {area.height})";

                case ConditionType.TimeElapsed:
                    int ms = condProp.FindPropertyRelative("Count").intValue;
                    return $"경과 시간 {ms / 1000f:0.0}s";
            }

            return type.ToString();
        }

        private static string GetActionLabel(SerializedProperty actionProp)
        {
            ActionType type = (ActionType)actionProp.FindPropertyRelative("Type").enumValueIndex;
            switch (type)
            {
                case ActionType.SpawnMonster:
                    string registryKey = actionProp.FindPropertyRelative("HeaderParam").stringValue;
                    if (!string.IsNullOrWhiteSpace(registryKey))
                    {
                        return $"몬스터 {registryKey} 소환";
                    }
                    return $"몬스터 {actionProp.FindPropertyRelative("ParamId").intValue} 소환";

                case ActionType.Broadcast:
                    string message = actionProp.FindPropertyRelative("StringVal").stringValue;
                    return string.IsNullOrWhiteSpace(message) ? "메시지 브로드캐스트" : $"브로드캐스트: {message}";

                case ActionType.OpenGate:
                    Vector3 pos = actionProp.FindPropertyRelative("Position").vector3Value;
                    return $"게이트 열기 ({Mathf.RoundToInt(pos.x)}, {Mathf.RoundToInt(pos.z)})";

                case ActionType.ReturnToTown:
                    return "마을 복귀";
            }

            return type.ToString();
        }

        private void DrawAreaHandle(EventInfoSO evt, ConditionInfoSO cond)
        {
            Vector3 center = new Vector3(cond.Area.x + cond.Area.width * 0.5f, 0, cond.Area.y + cond.Area.height * 0.5f);
            Vector3 size = new Vector3(cond.Area.width, 1.0f, cond.Area.height);

            _boundsHandle.center = center;
            _boundsHandle.size = size;
            _boundsHandle.SetColor(new Color(0f, 1f, 0f, 0.5f));

            EditorGUI.BeginChangeCheck();
            _boundsHandle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_currentStage, "Resize Area");

                int minX = Mathf.RoundToInt(_boundsHandle.center.x - _boundsHandle.size.x * 0.5f);
                int minZ = Mathf.RoundToInt(_boundsHandle.center.z - _boundsHandle.size.z * 0.5f);
                int width = Mathf.Max(1, Mathf.RoundToInt(_boundsHandle.size.x));
                int height = Mathf.Max(1, Mathf.RoundToInt(_boundsHandle.size.z));

                cond.Area = new RectInt(minX, minZ, width, height);
                EditorUtility.SetDirty(_currentStage);
            }

            Handles.Label(center, $"{GetEventSceneLabel(evt)}\nArea");
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_currentStage == null)
            {
                return;
            }

            for (int i = 0; i < _currentStage.InitialSpawns.Count; i++)
            {
                var spawn = _currentStage.InitialSpawns[i];
                Vector3 pos = spawn.Position;

                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(pos, Quaternion.identity);
                Handles.Label(pos + Vector3.up * 1f, $"Spawn {i}\n({spawn.EntityKey})");

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_currentStage, "Move Spawn");
                    spawn.Position = new Vector3(Mathf.Round(newPos.x), Mathf.Round(newPos.y), Mathf.Round(newPos.z));
                    EditorUtility.SetDirty(_currentStage);
                }
            }

            for (int i = 0; i < _currentStage.InitialObjects.Count; i++)
            {
                var obj = _currentStage.InitialObjects[i];
                Vector3 pos = obj.Position;

                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(pos, Quaternion.identity);
                Handles.Label(pos + Vector3.up * 1f, $"Obj {i}\n({obj.EntityKey})");

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_currentStage, "Move Object");
                    obj.Position = new Vector3(Mathf.Round(newPos.x), Mathf.Round(newPos.y), Mathf.Round(newPos.z));
                    EditorUtility.SetDirty(_currentStage);
                }
            }

            if (_currentStage.Events == null)
            {
                return;
            }

            foreach (var evt in _currentStage.Events)
            {
                if (evt == null || !evt.Enabled)
                {
                    continue;
                }

                foreach (var cond in evt.Conditions)
                {
                    if (cond.Type == ConditionType.AreaEnter)
                    {
                        DrawAreaHandle(evt, cond);
                    }
                }

                foreach (var action in evt.Actions)
                {
                    DrawActionHandle(evt, action);
                }
            }
        }

        private void DrawActionHandle(EventInfoSO evt, ActionInfoSO action)
        {
            if (action.Type == ActionType.SpawnMonster || action.Type == ActionType.OpenGate)
            {
                Vector3 pos = action.Position;

                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(pos, Quaternion.identity);
                Handles.Label(pos, $"{GetEventSceneLabel(evt)}\n{action.Type}");

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_currentStage, "Move Action");
                    action.Position = new Vector3(Mathf.Round(newPos.x), Mathf.Round(newPos.y), Mathf.Round(newPos.z));
                    EditorUtility.SetDirty(_currentStage);
                }
            }
        }

        private static string GetEventSceneLabel(EventInfoSO evt)
        {
            if (evt == null)
            {
                return "Event";
            }

            string title = string.IsNullOrWhiteSpace(evt.Title) ? $"Event {evt.EventId}" : evt.Title.Trim();
            return evt.Enabled ? title : $"{title} (Disabled)";
        }

        private void CreateNewStageData()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create New Stage Data",
                "NewStage",
                "asset",
                "Save Stage Data",
                DefaultStageAssetFolder);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string stageId = System.IO.Path.GetFileNameWithoutExtension(path);
            var newData = CreateInstance<StageDataSO>();
            newData.MapId = stageId;
            AssetDatabase.CreateAsset(newData, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _currentStage = newData;
            EditorGUIUtility.PingObject(newData);
        }

        private static void DrawLine()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        }
    }
}
