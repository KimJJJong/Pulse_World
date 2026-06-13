using System;
using System.Collections.Generic;
using GameServer.InGame.Director.Data;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmRPG.Editor.StageBuilder
{
    public class StageEditorWindow : EditorWindow
    {
        private const string DefaultStageAssetFolder = "Assets/Resources/Data/StageAssets";
        private const string PreviewRootPrefix = "__StagePreview_";
        private const string DefaultEventSectionName = "Default";

        private StageDataSO _currentStage;
        private Vector2 _scrollPos;
        private readonly BoxBoundsHandle _boundsHandle = new BoxBoundsHandle();
        private readonly Dictionary<string, bool> _eventSectionFoldouts = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private bool _autoSyncPreview = true;
        private bool _autoExportJson = true;
        private bool _showSceneLabels = true;
        private bool _showEventLinks = true;
        private bool _groupEventsBySection = true;
        private string _newEventSectionName = DefaultEventSectionName;

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
                ExportCurrentStageJson(showDialog: true);
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
                if (normalizeSo.ApplyModifiedProperties())
                    ExportCurrentStageJson(showDialog: false);
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
            DrawSceneTools();
            DrawSpawnLists(so);
            DrawEventSection(so);

            if (so.ApplyModifiedProperties() && _autoExportJson)
                ExportCurrentStageJson(showDialog: false);

            EditorGUILayout.EndScrollView();
        }

        private void ExportCurrentStageJson(bool showDialog)
        {
            if (_currentStage == null)
                return;

            StageExporter.Export(_currentStage);
            if (showDialog)
                EditorUtility.DisplayDialog("Export Success", $"Exported {_currentStage.MapId}.json", "OK");
        }

        private void MarkCurrentStageDirtyAndExport()
        {
            if (_currentStage == null)
                return;

            EditorUtility.SetDirty(_currentStage);
            if (_autoExportJson)
                ExportCurrentStageJson(showDialog: false);
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
            DrawSectionHeader("Rhythm Settings", "Audio timing is sourced from Rhythm Audio Data.", RhythmAccent);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            SerializedProperty mapIdProp = so.FindProperty("MapId");
            SerializedProperty rhythmProp = so.FindProperty("Rhythm");
            SerializedProperty songKeyProp = rhythmProp.FindPropertyRelative("SongKey");
            SerializedProperty bpmProp = rhythmProp.FindPropertyRelative("Bpm");
            SerializedProperty baseBeatDivisionProp = rhythmProp.FindPropertyRelative("BaseBeatDivision");
            SerializedProperty actionWindowMsProp = rhythmProp.FindPropertyRelative("ActionWindowMs");
            SerializedProperty startDelayMsProp = rhythmProp.FindPropertyRelative("StartDelayMs");

            EditorGUILayout.PropertyField(songKeyProp, new GUIContent("Rhythm Audio StageId"));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use MapId", GUILayout.Width(92)))
            {
                songKeyProp.stringValue = mapIdProp.stringValue;
            }

            if (GUILayout.Button("Open Audio Editor", GUILayout.Width(132)))
            {
                EditorApplication.ExecuteMenuItem("RhythmRPG/Editors/Audio/Rhythm Audio Data Editor");
            }
            EditorGUILayout.EndHorizontal();

            string rhythmKey = StageExporter.ResolveRhythmKey(mapIdProp.stringValue, songKeyProp.stringValue);
            bool hasRhythmAudio = StageExporter.TryLoadRhythmAudioData(rhythmKey, out var rhythmAudio);

            EditorGUILayout.Space(2f);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Resolved StageId", rhythmKey);
                if (hasRhythmAudio)
                {
                    EditorGUILayout.IntField("BPM (Audio)", rhythmAudio.Bpm);
                    EditorGUILayout.TextField("Time Signature", $"{rhythmAudio.TimeSignatureNum}/{rhythmAudio.TimeSignatureDenom}");
                    EditorGUILayout.IntField("Ticks Per Beat", rhythmAudio.TicksPerBeat);
                    EditorGUILayout.IntField("Blocks", rhythmAudio.BlockCount);
                }
                else
                {
                    EditorGUILayout.IntField("Fallback BPM", bpmProp.intValue);
                    EditorGUILayout.IntField("Fallback Beat Division", baseBeatDivisionProp.intValue);
                }
            }

            if (hasRhythmAudio)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox("BPM and score timing will be exported from the linked Rhythm Audio Data.", MessageType.Info);
                if (GUILayout.Button("Reveal JSON", GUILayout.Width(92), GUILayout.Height(38)))
                {
                    EditorUtility.RevealInFinder(rhythmAudio.Path);
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Rhythm Audio Data was not found for '{rhythmKey}'. Create/export it in RhythmRPG Audio Editor. " +
                    "Until then, export uses the legacy hidden BPM fallback.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(actionWindowMsProp);
            EditorGUILayout.PropertyField(startDelayMsProp);

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

        private void DrawSceneTools()
        {
            DrawSectionHeader("Scene Layout Tools", "Preview and edit the real placement in Scene View.", ObjectAccent);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _autoSyncPreview = EditorGUILayout.ToggleLeft("Auto-sync preview objects when Stage handles move", _autoSyncPreview);
            _autoExportJson = EditorGUILayout.ToggleLeft("Auto-export JSON when Stage data changes", _autoExportJson);
            _showSceneLabels = EditorGUILayout.ToggleLeft("Show scene labels", _showSceneLabels);
            _showEventLinks = EditorGUILayout.ToggleLeft("Show event condition/action links", _showEventLinks);

            EditorGUILayout.BeginHorizontal();
            if (ColoredButton("Sync Preview", ObjectAccent, GUILayout.Height(28)))
            {
                SyncScenePreview();
            }

            if (ColoredButton("Pull From Scene", SpawnAccent, GUILayout.Height(28)))
            {
                PullPreviewTransformsToStage();
            }

            if (ColoredButton("Clear Preview", new Color(0.70f, 0.34f, 0.34f, 1f), GUILayout.Height(28)))
            {
                ClearScenePreview();
            }

            if (GUILayout.Button("Select Root", GUILayout.Width(92), GUILayout.Height(28)))
            {
                Selection.activeGameObject = FindPreviewRoot();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Sync Preview는 Registry의 EntityDefinition Prefab을 씬에 배치합니다. 씬에서 프리뷰 오브젝트를 이동한 뒤 Pull From Scene을 누르면 StageData 위치/회전/스케일로 저장됩니다.",
                MessageType.None);

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

            DrawEventSectionControls(eventsProp);
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

            if (_groupEventsBySection)
            {
                DrawGroupedEventCards(eventsProp);
            }
            else
            {
                for (int i = 0; i < eventsProp.arraySize; i++)
                {
                    if (DrawEventCard(eventsProp, i))
                        break;
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawEventSectionControls(SerializedProperty eventsProp)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            _groupEventsBySection = EditorGUILayout.ToggleLeft("Group by Section", _groupEventsBySection, GUILayout.Width(140));
            GUILayout.Label("New Events Section", GUILayout.Width(118));
            _newEventSectionName = EditorGUILayout.TextField(NormalizeEventSection(_newEventSectionName));

            if (GUILayout.Button("Normalize Empty", GUILayout.Width(118)))
            {
                NormalizeEmptyEventSections(eventsProp);
            }
            EditorGUILayout.EndHorizontal();

            List<EventSectionView> sections = BuildEventSections(eventsProp);
            if (sections.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Sections", EditorStyles.miniBoldLabel, GUILayout.Width(64));
                foreach (var section in sections)
                {
                    if (GUILayout.Button($"{section.Name} ({section.EventIndexes.Count})", EditorStyles.miniButton, GUILayout.MaxWidth(150)))
                    {
                        _newEventSectionName = section.Name;
                        _eventSectionFoldouts[section.Name] = true;
                    }
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.HelpBox("Section은 Editor 전용 분류입니다. Export JSON과 런타임 이벤트 순서는 바뀌지 않습니다.", MessageType.None);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawGroupedEventCards(SerializedProperty eventsProp)
        {
            List<EventSectionView> sections = BuildEventSections(eventsProp);
            foreach (var section in sections)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawCardAccent(EventAccent);

                bool expanded = GetEventSectionFoldout(section.Name);
                EditorGUILayout.BeginHorizontal();
                expanded = EditorGUILayout.Foldout(expanded, $"{section.Name}  ({section.EventIndexes.Count} events, {section.EnabledCount} enabled)", true);
                _eventSectionFoldouts[section.Name] = expanded;
                GUILayout.FlexibleSpace();

                using (new GUIBackgroundColorScope(string.Equals(_newEventSectionName, section.Name, StringComparison.OrdinalIgnoreCase) ? SpawnAccent : BasicAccent))
                {
                    if (GUILayout.Button("Target", EditorStyles.miniButton, GUILayout.Width(58)))
                    {
                        _newEventSectionName = section.Name;
                        _eventSectionFoldouts[section.Name] = true;
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (expanded)
                {
                    EditorGUI.indentLevel++;
                    foreach (int eventIndex in section.EventIndexes)
                    {
                        if (eventIndex >= eventsProp.arraySize)
                            continue;

                        if (DrawEventCard(eventsProp, eventIndex))
                        {
                            EditorGUI.indentLevel--;
                            EditorGUILayout.EndVertical();
                            return;
                        }
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }
        }

        private bool GetEventSectionFoldout(string sectionName)
        {
            sectionName = NormalizeEventSection(sectionName);
            if (!_eventSectionFoldouts.TryGetValue(sectionName, out bool expanded))
            {
                expanded = true;
                _eventSectionFoldouts[sectionName] = true;
            }

            return expanded;
        }

        private static List<EventSectionView> BuildEventSections(SerializedProperty eventsProp)
        {
            var sections = new List<EventSectionView>();
            var sectionByName = new Dictionary<string, EventSectionView>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < eventsProp.arraySize; i++)
            {
                SerializedProperty evtProp = eventsProp.GetArrayElementAtIndex(i);
                string sectionName = GetEventSection(evtProp);
                if (!sectionByName.TryGetValue(sectionName, out var section))
                {
                    section = new EventSectionView(sectionName);
                    sectionByName[sectionName] = section;
                    sections.Add(section);
                }

                section.EventIndexes.Add(i);
                if (evtProp.FindPropertyRelative("Enabled").boolValue)
                    section.EnabledCount++;
            }

            return sections;
        }

        private static string GetEventSection(SerializedProperty evtProp)
        {
            SerializedProperty sectionProp = evtProp.FindPropertyRelative("Section");
            return NormalizeEventSection(sectionProp != null ? sectionProp.stringValue : string.Empty);
        }

        private static void SetEventSection(SerializedProperty evtProp, string sectionName)
        {
            SerializedProperty sectionProp = evtProp.FindPropertyRelative("Section");
            if (sectionProp != null)
                sectionProp.stringValue = NormalizeEventSection(sectionName);
        }

        private static string NormalizeEventSection(string sectionName)
            => string.IsNullOrWhiteSpace(sectionName) ? DefaultEventSectionName : sectionName.Trim();

        private static void NormalizeEmptyEventSections(SerializedProperty eventsProp)
        {
            for (int i = 0; i < eventsProp.arraySize; i++)
            {
                SerializedProperty evtProp = eventsProp.GetArrayElementAtIndex(i);
                SerializedProperty sectionProp = evtProp.FindPropertyRelative("Section");
                if (sectionProp != null && string.IsNullOrWhiteSpace(sectionProp.stringValue))
                    sectionProp.stringValue = DefaultEventSectionName;
            }
        }

        private bool DrawEventCard(SerializedProperty eventsProp, int index)
        {
            SerializedProperty evtProp = eventsProp.GetArrayElementAtIndex(index);
            SerializedProperty sectionProp = evtProp.FindPropertyRelative("Section");
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
            bool structureChanged = false;
            using (new GUIBackgroundColorScope(enabledProp.boolValue ? SpawnAccent : new Color(0.62f, 0.62f, 0.62f, 1f)))
            {
                enabledProp.boolValue = GUILayout.Toggle(enabledProp.boolValue, enabledProp.boolValue ? "Enabled" : "Disabled", EditorStyles.miniButton, GUILayout.Width(78));
            }

            if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(24)) && index > 0)
            {
                eventsProp.MoveArrayElement(index, index - 1);
                structureChanged = true;
            }

            if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(24)) && index < eventsProp.arraySize - 1)
            {
                eventsProp.MoveArrayElement(index, index + 1);
                structureChanged = true;
            }

            if (GUILayout.Button("Duplicate", EditorStyles.miniButtonMid, GUILayout.Width(72)))
            {
                DuplicateEvent(eventsProp, index);
                structureChanged = true;
            }

            if (GUILayout.Button("Delete", EditorStyles.miniButtonRight, GUILayout.Width(60)))
            {
                DeleteEvent(eventsProp, index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return true;
            }
            EditorGUILayout.EndHorizontal();

            if (structureChanged)
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
                return true;
            }

            EditorGUILayout.LabelField($"{conditionSummary}  ->  {actionSummary}", EditorStyles.miniLabel);

            if (evtProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                DrawSubHeader("Event Settings", BasicAccent, "Name, memo, and life-cycle");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(sectionProp, new GUIContent("Section"));
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
            return false;
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
                    DrawAreaConditionFields(condProp);
                    EditorGUILayout.HelpBox("월드 X/Z 영역에 진입하면 발동합니다.", MessageType.None);
                    break;

                case ConditionType.AreaExit:
                    DrawAreaConditionFields(condProp);
                    EditorGUILayout.HelpBox("월드 X/Z 영역을 벗어나면 발동합니다. Tutorial Panel Hide 이벤트에 사용합니다.", MessageType.None);
                    break;

                case ConditionType.AreaPlayerCount:
                    DrawAreaConditionFields(condProp);
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("CountRequirement"), new GUIContent("Required Count Source"));
                    if ((StageCountRequirementMode)condProp.FindPropertyRelative("CountRequirement").enumValueIndex == StageCountRequirementMode.FixedCount)
                    {
                        EditorGUILayout.PropertyField(condProp.FindPropertyRelative("Count"), new GUIContent("Required Players"));
                    }
                    else
                    {
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.IntField("Required Players", 0);
                        EditorGUILayout.HelpBox("런타임의 게임 참여 인원 수를 필요 인원으로 사용합니다.", MessageType.None);
                    }
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("ProgressLabel"), new GUIContent("Progress Label"));
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("ProgressDurationMs"), new GUIContent("Text Duration (ms)"));
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("ShowProgressUi"), new GUIContent("Show Center Count UI"));
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("ShowAreaOutline"), new GUIContent("Show Area Outline"));
                    EditorGUILayout.HelpBox("영역 안의 살아있는 Player 수가 조건을 만족하면 발동합니다. Participant Count를 선택하면 현재 게임 참여 인원이 n이 됩니다.", MessageType.None);
                    break;

                case ConditionType.AreaHoldBeats:
                    DrawAreaConditionFields(condProp);
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("Count"), new GUIContent("Required Hold Beats"));
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("CountRequirement"), new GUIContent("Required Player Source"));
                    if ((StageCountRequirementMode)condProp.FindPropertyRelative("CountRequirement").enumValueIndex == StageCountRequirementMode.FixedCount)
                    {
                        EditorGUILayout.PropertyField(condProp.FindPropertyRelative("TargetId"), new GUIContent("Required Players"));
                    }
                    else
                    {
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.IntField("Required Players", 0);
                        EditorGUILayout.HelpBox("런타임의 게임 참여 인원 수를 필요 인원으로 사용합니다.", MessageType.None);
                    }
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("TargetKey"), new GUIContent("Linked Scene Effect Key"));
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("SecondaryTargetId"), new GUIContent("Linked Scene Effect Group ID"));
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("ProgressLabel"), new GUIContent("Progress Label"));
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("ProgressDurationMs"), new GUIContent("Text Duration (ms)"));
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("ShowProgressUi"), new GUIContent("Show Center Progress UI"));
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("ShowAreaOutline"), new GUIContent("Show Area Outline"));
                    EditorGUILayout.HelpBox("영역 안에 있을 때만 Beat 진행도가 쌓입니다. 영역을 벗어나면 진행도는 유지된 채 정지하고, 완료 전에는 연결 연출을 끕니다.", MessageType.None);
                    break;

                case ConditionType.TimeElapsed:
                    SerializedProperty timeProp = condProp.FindPropertyRelative("Count");
                    EditorGUILayout.PropertyField(timeProp, new GUIContent("Elapsed Time (ms)"));
                    EditorGUILayout.LabelField("Preview", $"{timeProp.intValue / 1000f:0.0}s");
                    break;

                case ConditionType.ObjectInteracted:
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("TargetId"), new GUIContent("Object Group/ID"));
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("TargetKey"), new GUIContent("Editor Key"));
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("Count"), new GUIContent("Required Interactions"));
                    EditorGUILayout.HelpBox("지정한 오브젝트 GroupId 또는 ID와 상호작용하면 발동합니다. 0이면 아무 오브젝트 상호작용에도 반응합니다.", MessageType.None);
                    break;

                case ConditionType.ObjectPairInteracted:
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("TargetId"), new GUIContent("First Object Group/ID"));
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("SecondaryTargetId"), new GUIContent("Second Object Group/ID"));
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("TargetKey"), new GUIContent("Editor Key"));
                    EditorGUILayout.HelpBox("두 오브젝트를 각각 한 번 이상 상호작용하면 발동합니다. 크리스탈 2개를 모두 활성화하는 구조에 사용합니다.", MessageType.None);
                    break;

                case ConditionType.ObjectStateEquals:
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("TargetId"), new GUIContent("Object Group/ID"));
                    EditorGUILayout.PropertyField(condProp.FindPropertyRelative("Count"), new GUIContent("Expected State"));
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawAreaConditionFields(SerializedProperty condProp)
        {
            SerializedProperty areaProp = condProp.FindPropertyRelative("Area");
            SerializedProperty shapeProp = condProp.FindPropertyRelative("AreaShape");

            EditorGUILayout.PropertyField(areaProp, new GUIContent("Area Bounds (X/Z)"));
            EditorGUILayout.PropertyField(shapeProp, new GUIContent("Area Shape"));

            StageAreaShapeType shape = (StageAreaShapeType)shapeProp.enumValueIndex;
            if (shape != StageAreaShapeType.CustomCells)
                return;

            SerializedProperty cellsProp = condProp.FindPropertyRelative("AreaCells");
            EditorGUILayout.PropertyField(cellsProp, new GUIContent("Custom Cells"), true);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Seed From Bounds", EditorStyles.miniButtonLeft))
            {
                SeedAreaCellsFromBounds(condProp);
            }

            if (GUILayout.Button("Fit Bounds", EditorStyles.miniButtonMid))
            {
                FitBoundsToAreaCells(condProp);
            }

            if (GUILayout.Button("Normalize", EditorStyles.miniButtonRight))
            {
                NormalizeAreaCells(condProp);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("Custom Cells는 지정한 타일들의 외곽선만 표시하고, 런타임 조건도 해당 셀 목록 기준으로 판정합니다.", MessageType.None);
        }

        private static void SeedAreaCellsFromBounds(SerializedProperty condProp)
        {
            SerializedProperty areaProp = condProp.FindPropertyRelative("Area");
            SerializedProperty cellsProp = condProp.FindPropertyRelative("AreaCells");
            RectInt area = areaProp.rectIntValue;

            int width = Mathf.Max(1, area.width);
            int height = Mathf.Max(1, area.height);
            cellsProp.arraySize = width * height;

            int index = 0;
            for (int y = area.y; y < area.y + height; y++)
            {
                for (int x = area.x; x < area.x + width; x++)
                {
                    cellsProp.GetArrayElementAtIndex(index).vector2IntValue = new Vector2Int(x, y);
                    index++;
                }
            }
        }

        private static void FitBoundsToAreaCells(SerializedProperty condProp)
        {
            SerializedProperty cellsProp = condProp.FindPropertyRelative("AreaCells");
            if (cellsProp.arraySize <= 0)
                return;

            Vector2Int first = cellsProp.GetArrayElementAtIndex(0).vector2IntValue;
            int minX = first.x;
            int minY = first.y;
            int maxX = first.x;
            int maxY = first.y;

            for (int i = 1; i < cellsProp.arraySize; i++)
            {
                Vector2Int cell = cellsProp.GetArrayElementAtIndex(i).vector2IntValue;
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }

            condProp.FindPropertyRelative("Area").rectIntValue = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static void NormalizeAreaCells(SerializedProperty condProp)
        {
            SerializedProperty cellsProp = condProp.FindPropertyRelative("AreaCells");
            if (cellsProp.arraySize <= 1)
                return;

            var cells = new List<Vector2Int>();
            var seen = new HashSet<Vector2Int>();
            for (int i = 0; i < cellsProp.arraySize; i++)
            {
                Vector2Int cell = cellsProp.GetArrayElementAtIndex(i).vector2IntValue;
                if (seen.Add(cell))
                    cells.Add(cell);
            }

            cells.Sort((a, b) =>
            {
                int yCompare = a.y.CompareTo(b.y);
                return yCompare != 0 ? yCompare : a.x.CompareTo(b.x);
            });

            cellsProp.arraySize = cells.Count;
            for (int i = 0; i < cells.Count; i++)
                cellsProp.GetArrayElementAtIndex(i).vector2IntValue = cells[i];

            FitBoundsToAreaCells(condProp);
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

                case ActionType.FinGame:
                    EditorGUILayout.HelpBox("Stage Clear 결과 UI를 표시합니다. Rewards는 아직 비워 둡니다.", MessageType.None);
                    break;

                case ActionType.SpawnObject:
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("HeaderParam"), new GUIContent("Registry Key"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("ParamId"), new GUIContent("Object Entity ID"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("Position"), new GUIContent("Spawn Position"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("ObjectSize"), new GUIContent("Object Size"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("GroupId"), new GUIContent("Group ID"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("StringVal"), new GUIContent("Pattern/State"));
                    EditorGUILayout.HelpBox("Registry Key가 있으면 Export 시 Object Entity ID가 자동으로 채워집니다.", MessageType.None);
                    break;

                case ActionType.ShowGuide:
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("GuideTitle"), new GUIContent("Title"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("GuideBody"), new GUIContent("Body"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("GuideImageResource"), new GUIContent("Image Resource"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("DurationMs"), new GUIContent("Duration (ms)"));
                    EditorGUILayout.HelpBox("Image Resource는 Resources 기준 경로입니다. 예: UI/Tutorial/MoveGuide", MessageType.None);
                    break;

                case ActionType.ShowTutorialPanel:
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("StringVal"), new GUIContent("Panel ID"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("GuideImageResource"), new GUIContent("Panel Image Resource"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("ParamId"), new GUIContent("Panel Width"));
                    DrawTutorialPanelAnchorSelector(actionProp.FindPropertyRelative("GroupId"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("Position"), new GUIContent("Screen Offset (X/Y)"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("DurationMs"), new GUIContent("Fade In (ms)"));
                    EditorGUILayout.HelpBox("Panel Image Resource는 Resources 기준 경로입니다. 예: UI/UI_Tutorial/Tutorial_Movement", MessageType.None);
                    break;

                case ActionType.HideTutorialPanel:
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("StringVal"), new GUIContent("Panel ID"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("DurationMs"), new GUIContent("Fade Out (ms)"));
                    EditorGUILayout.HelpBox("Panel ID가 비어 있으면 현재 표시 중인 Tutorial Panel을 숨깁니다.", MessageType.None);
                    break;

                case ActionType.SetObjectState:
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("ParamId"), new GUIContent("Object Group/ID"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("GroupId"), new GUIContent("State Value"));
                    break;

                case ActionType.RemoveEntityGroup:
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("ParamId"), new GUIContent("Target Entity Group ID"));
                    SerializedProperty removeGroupDelayProp = actionProp.FindPropertyRelative("Position");
                    Vector3 removeGroupDelay = removeGroupDelayProp.vector3Value;
                    removeGroupDelay.x = EditorGUILayout.IntField(new GUIContent("Delay (ms)", "RemoveEntityGroup에서만 Position X를 지연 시간으로 사용합니다."), Mathf.Max(0, Mathf.RoundToInt(removeGroupDelay.x)));
                    removeGroupDelayProp.vector3Value = removeGroupDelay;
                    EditorGUILayout.HelpBox("해당 Group ID를 가진 몬스터/오브젝트 엔티티를 despawn합니다. Player는 제거하지 않습니다.", MessageType.None);
                    break;

                case ActionType.SetSceneObjectActive:
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("StringVal"), new GUIContent("Scene Target Key"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("ParamId"), new GUIContent("Scene Target Group ID"));
                    SerializedProperty sceneObjectActiveProp = actionProp.FindPropertyRelative("GroupId");
                    bool sceneObjectActive = sceneObjectActiveProp.intValue != 0;
                    sceneObjectActive = EditorGUILayout.Toggle(new GUIContent("Active"), sceneObjectActive);
                    sceneObjectActiveProp.intValue = sceneObjectActive ? 1 : 0;
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("DurationMs"), new GUIContent("Fade Duration (ms)"));
                    SerializedProperty sceneObjectDelayProp = actionProp.FindPropertyRelative("Position");
                    Vector3 sceneObjectDelay = sceneObjectDelayProp.vector3Value;
                    sceneObjectDelay.x = EditorGUILayout.IntField(new GUIContent("Delay (ms)", "SetSceneObjectActive에서만 Position X를 지연 시간으로 사용합니다."), Mathf.RoundToInt(sceneObjectDelay.x));
                    sceneObjectDelayProp.vector3Value = sceneObjectDelay;
                    EditorGUILayout.HelpBox("맵에 직접 배치된 안개/길막/장식 오브젝트 root에 StageSceneObjectTarget을 붙이고 Key 또는 Group ID를 맞추면 자연스럽게 표시/숨김 처리됩니다. Delay를 넣으면 지정 시간 후 시작합니다.", MessageType.Info);
                    break;

                case ActionType.SetSummonPortalActive:
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("StringVal"), new GUIContent("Portal Key"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("ParamId"), new GUIContent("Spawn Group ID"));
                    SerializedProperty summonActiveProp = actionProp.FindPropertyRelative("GroupId");
                    bool summonActive = summonActiveProp.intValue != 0;
                    summonActive = EditorGUILayout.Toggle(new GUIContent("Active"), summonActive);
                    summonActiveProp.intValue = summonActive ? 1 : 0;
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("Position"), new GUIContent("Spawn Cell"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("GuideTitle"), new GUIContent("Monster IDs CSV"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("GuideBody"), new GUIContent("Monster Pattern"));
                    SerializedProperty summonSizeProp = actionProp.FindPropertyRelative("ObjectSize");
                    Vector2Int summonSize = summonSizeProp.vector2IntValue;
                    summonSize.x = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Max Alive"), Mathf.Max(1, summonSize.x)));
                    summonSize.y = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Initial Delay Beats"), Mathf.Max(0, summonSize.y)));
                    summonSizeProp.vector2IntValue = summonSize;
                    actionProp.FindPropertyRelative("DurationMs").intValue = Mathf.Max(
                        1,
                        EditorGUILayout.IntField(new GUIContent("Interval Beats"), Mathf.Max(1, actionProp.FindPropertyRelative("DurationMs").intValue)));
                    EditorGUILayout.HelpBox("Host 전용 Beat 소환 액션입니다. Ring/Gate 표시 자체는 SetSceneObjectActive로 별도 처리하고, 이 액션은 해당 위치에 주기적으로 몬스터를 생성합니다.", MessageType.Info);
                    break;

                case ActionType.SetGateDoorOpen:
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("StringVal"), new GUIContent("Gate Target Key"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("ParamId"), new GUIContent("Gate Group ID"));
                    SerializedProperty gateOpenProp = actionProp.FindPropertyRelative("GroupId");
                    bool gateOpen = gateOpenProp.intValue != 0;
                    gateOpen = EditorGUILayout.Toggle(new GUIContent("Open"), gateOpen);
                    gateOpenProp.intValue = gateOpen ? 1 : 0;
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("DurationMs"), new GUIContent("Duration (ms)"));
                    SerializedProperty gateAngleProp = actionProp.FindPropertyRelative("Position");
                    Vector3 gateAngle = gateAngleProp.vector3Value;
                    gateAngle.x = EditorGUILayout.IntField(new GUIContent("Angle Override", "0이면 StageGateStoneDoorTarget의 Open Angle을 사용합니다."), Mathf.RoundToInt(gateAngle.x));
                    gateAngleProp.vector3Value = gateAngle;
                    EditorGUILayout.HelpBox("Gate_Stone root에 StageGateStoneDoorTarget을 붙이고 DoorTransform을 자식 문으로 지정합니다. PivotTransform은 돌탑 root를 사용합니다.", MessageType.Info);
                    break;

                case ActionType.PlayVfx:
                    SerializedProperty vfxKeyProp = actionProp.FindPropertyRelative("VfxKey");
                    DrawVfxKeySelector(vfxKeyProp);

                    StageVfxCatalog.TryGetDefinition(vfxKeyProp.stringValue, out var vfxDefinition);
                    bool objectTargetVfx = vfxDefinition != null && vfxDefinition.TargetMode == StageVfxTargetMode.ObjectPulseColor;
                    if (objectTargetVfx)
                    {
                        EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("ParamId"), new GUIContent("Target Object Group/ID"));
                        EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("GroupId"), new GUIContent("Second Object Group/ID"));
                    }

                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("Position"), new GUIContent(objectTargetVfx ? "Fallback Position" : "Position"));
                    EditorGUILayout.PropertyField(actionProp.FindPropertyRelative("DurationMs"), new GUIContent("Duration (ms)"));
                    if (vfxDefinition != null && !string.IsNullOrWhiteSpace(vfxDefinition.Description))
                        EditorGUILayout.HelpBox(vfxDefinition.Description, MessageType.None);
                    else
                        EditorGUILayout.HelpBox("Catalog에 없는 key는 기존 위치 기반 VFX 마커 fallback으로 처리됩니다.", MessageType.Info);
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawVfxKeySelector(SerializedProperty vfxKeyProp)
        {
            string currentKey = vfxKeyProp.stringValue ?? string.Empty;
            bool hasKnownDefinition = StageVfxCatalog.TryGetDefinition(currentKey, out var currentDefinition);
            if (hasKnownDefinition && !string.Equals(currentKey, currentDefinition.Key, StringComparison.Ordinal))
            {
                currentKey = currentDefinition.Key;
                vfxKeyProp.stringValue = currentKey;
            }

            var definitions = StageVfxCatalog.All;
            int extraOptionCount = hasKnownDefinition ? 0 : 1;
            var labels = new GUIContent[definitions.Count + extraOptionCount];
            var values = new string[labels.Length];
            int selectedIndex = 0;
            int writeIndex = 0;

            if (!hasKnownDefinition)
            {
                labels[0] = new GUIContent(string.IsNullOrWhiteSpace(currentKey) ? "None" : $"Custom: {currentKey}");
                values[0] = currentKey;
                writeIndex = 1;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                int optionIndex = writeIndex + i;
                labels[optionIndex] = new GUIContent($"{definition.DisplayName} ({definition.Key})");
                values[optionIndex] = definition.Key;

                if (hasKnownDefinition && string.Equals(definition.Key, currentDefinition.Key, StringComparison.Ordinal))
                    selectedIndex = optionIndex;
            }

            int nextIndex = EditorGUILayout.Popup(new GUIContent("VFX Key"), selectedIndex, labels);
            if (nextIndex >= 0 && nextIndex < values.Length && !string.Equals(vfxKeyProp.stringValue, values[nextIndex], StringComparison.Ordinal))
                vfxKeyProp.stringValue = values[nextIndex];

            if (!hasKnownDefinition && !string.IsNullOrWhiteSpace(currentKey))
                EditorGUILayout.PropertyField(vfxKeyProp, new GUIContent("Custom VFX Key"));
        }

        private static void DrawTutorialPanelAnchorSelector(SerializedProperty anchorProp)
        {
            string[] labels =
            {
                "Bottom Right",
                "Bottom Center",
                "Top Right",
                "Top Center",
                "Center",
                "Bottom Left",
                "Top Left",
                "Left Center"
            };

            int current = Mathf.Clamp(anchorProp.intValue, 0, labels.Length - 1);
            int next = EditorGUILayout.Popup(new GUIContent("Anchor"), current, labels);
            if (next != anchorProp.intValue)
                anchorProp.intValue = next;
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

        private sealed class EventSectionView
        {
            public readonly string Name;
            public readonly List<int> EventIndexes = new List<int>();
            public int EnabledCount;

            public EventSectionView(string name)
            {
                Name = name;
            }
        }

        private enum EventQuickTemplate
        {
            Empty,
            MonsterClearSpawn,
            AreaBroadcast,
            TimeReturnTown,
            TutorialGuide,
            TutorialPanelPair,
            CrystalPair,
            StageClearFinGame
        }

        private enum ConditionQuickTemplate
        {
            MonsterAllDead,
            AreaEnter,
            AreaExit,
            AreaPlayerCount,
            AreaHoldBeats,
            TimeElapsed,
            ObjectInteracted,
            ObjectPairInteracted,
            ObjectStateEquals
        }

        private enum ActionQuickTemplate
        {
            Broadcast,
            SpawnMonster,
            OpenGate,
            ReturnToTown,
            SpawnObject,
            ShowGuide,
            ShowTutorialPanel,
            HideTutorialPanel,
            SetObjectState,
            PlayVfx,
            RemoveEntityGroup,
            SetSceneObjectActive,
            SetSummonPortalActive,
            SetGateDoorOpen,
            FinGame
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

            if (ColoredButton("Guide", BasicAccent, GUILayout.Width(70)))
            {
                AddEvent(eventsProp, EventQuickTemplate.TutorialGuide);
                so.ApplyModifiedProperties();
            }

            if (ColoredButton("Tutorial Panel", BasicAccent, GUILayout.Width(112)))
            {
                AddEvent(eventsProp, EventQuickTemplate.TutorialPanelPair);
                so.ApplyModifiedProperties();
            }

            if (ColoredButton("Crystal Pair", ObjectAccent, GUILayout.Width(100)))
            {
                AddEvent(eventsProp, EventQuickTemplate.CrystalPair);
                so.ApplyModifiedProperties();
            }

            if (ColoredButton("Fin Game", ActionAccent, GUILayout.Width(90)))
            {
                AddEvent(eventsProp, EventQuickTemplate.StageClearFinGame);
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

            if (ColoredButton("+ Exit", ConditionAccent, GUILayout.Width(66)))
            {
                AddCondition(conditionsProp, ConditionQuickTemplate.AreaExit);
            }

            if (ColoredButton("+ Count", ConditionAccent, GUILayout.Width(76)))
            {
                AddCondition(conditionsProp, ConditionQuickTemplate.AreaPlayerCount);
            }

            if (ColoredButton("+ Hold", ConditionAccent, GUILayout.Width(70)))
            {
                AddCondition(conditionsProp, ConditionQuickTemplate.AreaHoldBeats);
            }

            if (ColoredButton("+ Time", ActionAccent, GUILayout.Width(70)))
            {
                AddCondition(conditionsProp, ConditionQuickTemplate.TimeElapsed);
            }

            if (ColoredButton("+ Obj", ObjectAccent, GUILayout.Width(68)))
            {
                AddCondition(conditionsProp, ConditionQuickTemplate.ObjectInteracted);
            }

            if (ColoredButton("+ Pair", EventAccent, GUILayout.Width(70)))
            {
                AddCondition(conditionsProp, ConditionQuickTemplate.ObjectPairInteracted);
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

            if (ColoredButton("+ Fin", ActionAccent, GUILayout.Width(66)))
            {
                AddAction(actionsProp, ActionQuickTemplate.FinGame);
            }

            if (ColoredButton("+ Obj", ObjectAccent, GUILayout.Width(70)))
            {
                AddAction(actionsProp, ActionQuickTemplate.SpawnObject);
            }

            if (ColoredButton("+ Guide", BasicAccent, GUILayout.Width(76)))
            {
                AddAction(actionsProp, ActionQuickTemplate.ShowGuide);
            }

            if (ColoredButton("+ Panel", BasicAccent, GUILayout.Width(76)))
            {
                AddAction(actionsProp, ActionQuickTemplate.ShowTutorialPanel);
            }

            if (ColoredButton("+ Hide", BasicAccent, GUILayout.Width(70)))
            {
                AddAction(actionsProp, ActionQuickTemplate.HideTutorialPanel);
            }

            if (ColoredButton("+ VFX", ConditionAccent, GUILayout.Width(70)))
            {
                AddAction(actionsProp, ActionQuickTemplate.PlayVfx);
            }

            if (ColoredButton("+ KillGrp", ActionAccent, GUILayout.Width(78)))
            {
                AddAction(actionsProp, ActionQuickTemplate.RemoveEntityGroup);
            }

            if (ColoredButton("+ Deco", ObjectAccent, GUILayout.Width(72)))
            {
                AddAction(actionsProp, ActionQuickTemplate.SetSceneObjectActive);
            }

            if (ColoredButton("+ Summon", SpawnAccent, GUILayout.Width(86)))
            {
                AddAction(actionsProp, ActionQuickTemplate.SetSummonPortalActive);
            }

            if (ColoredButton("+ Gate", ActionAccent, GUILayout.Width(72)))
            {
                AddAction(actionsProp, ActionQuickTemplate.SetGateDoorOpen);
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
            SetEventSection(evtProp, _newEventSectionName);

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

                case EventQuickTemplate.TutorialGuide:
                    evtProp.FindPropertyRelative("Title").stringValue = $"Guide {nextId}";
                    AddCondition(evtProp.FindPropertyRelative("Conditions"), ConditionQuickTemplate.AreaEnter);
                    AddAction(evtProp.FindPropertyRelative("Actions"), ActionQuickTemplate.ShowGuide);
                    break;

                case EventQuickTemplate.TutorialPanelPair:
                    evtProp.FindPropertyRelative("Title").stringValue = $"Tutorial Panel {nextId} Enter";
                    evtProp.FindPropertyRelative("IsOneShot").boolValue = false;
                    SerializedProperty enterConditions = evtProp.FindPropertyRelative("Conditions");
                    AddCondition(enterConditions, ConditionQuickTemplate.AreaEnter);
                    SetLastConditionArea(enterConditions, new RectInt(0, 0, 6, 6));
                    AddAction(evtProp.FindPropertyRelative("Actions"), ActionQuickTemplate.ShowTutorialPanel);

                    int exitIndex = eventsProp.arraySize;
                    eventsProp.InsertArrayElementAtIndex(exitIndex);
                    SerializedProperty exitProp = eventsProp.GetArrayElementAtIndex(exitIndex);
                    ResetEvent(exitProp, GetNextEventId(eventsProp));
                    SetEventSection(exitProp, GetEventSection(evtProp));
                    exitProp.FindPropertyRelative("Title").stringValue = $"Tutorial Panel {nextId} Exit";
                    exitProp.FindPropertyRelative("IsOneShot").boolValue = false;
                    SerializedProperty exitConditions = exitProp.FindPropertyRelative("Conditions");
                    AddCondition(exitConditions, ConditionQuickTemplate.AreaExit);
                    SetLastConditionArea(exitConditions, new RectInt(0, 0, 6, 6));
                    AddAction(exitProp.FindPropertyRelative("Actions"), ActionQuickTemplate.HideTutorialPanel);
                    exitProp.isExpanded = true;
                    break;

                case EventQuickTemplate.CrystalPair:
                    evtProp.FindPropertyRelative("Title").stringValue = $"Crystal Pair {nextId}";
                    AddCondition(evtProp.FindPropertyRelative("Conditions"), ConditionQuickTemplate.ObjectPairInteracted);
                    AddAction(evtProp.FindPropertyRelative("Actions"), ActionQuickTemplate.PlayVfx);
                    break;

                case EventQuickTemplate.StageClearFinGame:
                    evtProp.FindPropertyRelative("Title").stringValue = "Stage Clear";
                    AddCondition(evtProp.FindPropertyRelative("Conditions"), ConditionQuickTemplate.MonsterAllDead);
                    AddAction(evtProp.FindPropertyRelative("Actions"), ActionQuickTemplate.FinGame);
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
            SetEventSection(evtProp, DefaultEventSectionName);
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

        private static void SetLastConditionArea(SerializedProperty conditionsProp, RectInt area)
        {
            if (conditionsProp == null || conditionsProp.arraySize <= 0)
                return;

            conditionsProp.GetArrayElementAtIndex(conditionsProp.arraySize - 1)
                .FindPropertyRelative("Area").rectIntValue = area;
        }

        private static void AddCondition(SerializedProperty conditionsProp, ConditionQuickTemplate template)
        {
            int insertIndex = conditionsProp.arraySize;
            conditionsProp.InsertArrayElementAtIndex(insertIndex);

            SerializedProperty condProp = conditionsProp.GetArrayElementAtIndex(insertIndex);
            condProp.FindPropertyRelative("Area").rectIntValue = new RectInt(0, 0, 1, 1);
            condProp.FindPropertyRelative("AreaShape").enumValueIndex = (int)StageAreaShapeType.Rectangle;
            condProp.FindPropertyRelative("AreaCells").arraySize = 0;
            condProp.FindPropertyRelative("TargetKey").stringValue = string.Empty;
            condProp.FindPropertyRelative("SecondaryTargetId").intValue = 0;
            condProp.FindPropertyRelative("CountRequirement").enumValueIndex = (int)StageCountRequirementMode.FixedCount;
            condProp.FindPropertyRelative("ShowProgressUi").boolValue = true;
            condProp.FindPropertyRelative("ShowAreaOutline").boolValue = true;
            condProp.FindPropertyRelative("ProgressLabel").stringValue = "Area";
            condProp.FindPropertyRelative("ProgressDurationMs").intValue = 1200;

            switch (template)
            {
                case ConditionQuickTemplate.AreaEnter:
                    condProp.FindPropertyRelative("Type").enumValueIndex = (int)ConditionType.AreaEnter;
                    condProp.FindPropertyRelative("TargetId").intValue = 0;
                    condProp.FindPropertyRelative("Count").intValue = 0;
                    break;

                case ConditionQuickTemplate.AreaExit:
                    condProp.FindPropertyRelative("Type").enumValueIndex = (int)ConditionType.AreaExit;
                    condProp.FindPropertyRelative("TargetId").intValue = 0;
                    condProp.FindPropertyRelative("Count").intValue = 0;
                    break;

                case ConditionQuickTemplate.AreaPlayerCount:
                    condProp.FindPropertyRelative("Type").enumValueIndex = (int)ConditionType.AreaPlayerCount;
                    condProp.FindPropertyRelative("TargetId").intValue = 0;
                    condProp.FindPropertyRelative("Count").intValue = 1;
                    condProp.FindPropertyRelative("CountRequirement").enumValueIndex = (int)StageCountRequirementMode.ParticipantCount;
                    condProp.FindPropertyRelative("ProgressLabel").stringValue = "Gather";
                    break;

                case ConditionQuickTemplate.AreaHoldBeats:
                    condProp.FindPropertyRelative("Type").enumValueIndex = (int)ConditionType.AreaHoldBeats;
                    condProp.FindPropertyRelative("TargetId").intValue = 1;
                    condProp.FindPropertyRelative("Count").intValue = 8;
                    condProp.FindPropertyRelative("CountRequirement").enumValueIndex = (int)StageCountRequirementMode.FixedCount;
                    condProp.FindPropertyRelative("ProgressLabel").stringValue = "Hold";
                    break;

                case ConditionQuickTemplate.TimeElapsed:
                    condProp.FindPropertyRelative("Type").enumValueIndex = (int)ConditionType.TimeElapsed;
                    condProp.FindPropertyRelative("TargetId").intValue = 0;
                    condProp.FindPropertyRelative("Count").intValue = 1000;
                    break;

                case ConditionQuickTemplate.ObjectInteracted:
                    condProp.FindPropertyRelative("Type").enumValueIndex = (int)ConditionType.ObjectInteracted;
                    condProp.FindPropertyRelative("TargetId").intValue = 1;
                    condProp.FindPropertyRelative("Count").intValue = 1;
                    break;

                case ConditionQuickTemplate.ObjectPairInteracted:
                    condProp.FindPropertyRelative("Type").enumValueIndex = (int)ConditionType.ObjectPairInteracted;
                    condProp.FindPropertyRelative("TargetId").intValue = 1;
                    condProp.FindPropertyRelative("SecondaryTargetId").intValue = 2;
                    condProp.FindPropertyRelative("Count").intValue = 0;
                    break;

                case ConditionQuickTemplate.ObjectStateEquals:
                    condProp.FindPropertyRelative("Type").enumValueIndex = (int)ConditionType.ObjectStateEquals;
                    condProp.FindPropertyRelative("TargetId").intValue = 1;
                    condProp.FindPropertyRelative("Count").intValue = 1;
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
            actionProp.FindPropertyRelative("GuideTitle").stringValue = string.Empty;
            actionProp.FindPropertyRelative("GuideBody").stringValue = string.Empty;
            actionProp.FindPropertyRelative("GuideImageResource").stringValue = string.Empty;
            actionProp.FindPropertyRelative("DurationMs").intValue = 3500;
            actionProp.FindPropertyRelative("VfxKey").stringValue = string.Empty;

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

                case ActionQuickTemplate.FinGame:
                    actionProp.FindPropertyRelative("Type").enumValueIndex = (int)ActionType.FinGame;
                    break;

                case ActionQuickTemplate.SpawnObject:
                    actionProp.FindPropertyRelative("Type").enumValueIndex = (int)ActionType.SpawnObject;
                    actionProp.FindPropertyRelative("GroupId").intValue = 1;
                    break;

                case ActionQuickTemplate.ShowGuide:
                    actionProp.FindPropertyRelative("Type").enumValueIndex = (int)ActionType.ShowGuide;
                    actionProp.FindPropertyRelative("GuideTitle").stringValue = "Guide";
                    actionProp.FindPropertyRelative("GuideBody").stringValue = "Move with WASD and use skills on beat.";
                    actionProp.FindPropertyRelative("DurationMs").intValue = 4500;
                    break;

                case ActionQuickTemplate.ShowTutorialPanel:
                    actionProp.FindPropertyRelative("Type").enumValueIndex = (int)ActionType.ShowTutorialPanel;
                    actionProp.FindPropertyRelative("StringVal").stringValue = "TutorialPanel";
                    actionProp.FindPropertyRelative("GuideImageResource").stringValue = "UI/UI_Tutorial/Tutorial_Movement";
                    actionProp.FindPropertyRelative("ParamId").intValue = 900;
                    actionProp.FindPropertyRelative("GroupId").intValue = 7;
                    actionProp.FindPropertyRelative("Position").vector3Value = new Vector3(24f, 0f, 0f);
                    actionProp.FindPropertyRelative("DurationMs").intValue = 220;
                    break;

                case ActionQuickTemplate.HideTutorialPanel:
                    actionProp.FindPropertyRelative("Type").enumValueIndex = (int)ActionType.HideTutorialPanel;
                    actionProp.FindPropertyRelative("StringVal").stringValue = "TutorialPanel";
                    actionProp.FindPropertyRelative("DurationMs").intValue = 180;
                    break;

                case ActionQuickTemplate.SetObjectState:
                    actionProp.FindPropertyRelative("Type").enumValueIndex = (int)ActionType.SetObjectState;
                    actionProp.FindPropertyRelative("ParamId").intValue = 1;
                    actionProp.FindPropertyRelative("GroupId").intValue = 1;
                    break;

                case ActionQuickTemplate.RemoveEntityGroup:
                    actionProp.FindPropertyRelative("Type").enumValueIndex = (int)ActionType.RemoveEntityGroup;
                    actionProp.FindPropertyRelative("ParamId").intValue = 1;
                    break;

                case ActionQuickTemplate.SetSceneObjectActive:
                    actionProp.FindPropertyRelative("Type").enumValueIndex = (int)ActionType.SetSceneObjectActive;
                    actionProp.FindPropertyRelative("StringVal").stringValue = "FogBlocker";
                    actionProp.FindPropertyRelative("ParamId").intValue = 1;
                    actionProp.FindPropertyRelative("GroupId").intValue = 0;
                    actionProp.FindPropertyRelative("DurationMs").intValue = 650;
                    break;

                case ActionQuickTemplate.SetSummonPortalActive:
                    actionProp.FindPropertyRelative("Type").enumValueIndex = (int)ActionType.SetSummonPortalActive;
                    actionProp.FindPropertyRelative("StringVal").stringValue = "SummonRing";
                    actionProp.FindPropertyRelative("ParamId").intValue = 2101;
                    actionProp.FindPropertyRelative("GroupId").intValue = 1;
                    actionProp.FindPropertyRelative("Position").vector3Value = Vector3.zero;
                    actionProp.FindPropertyRelative("ObjectSize").vector2IntValue = new Vector2Int(2, 1);
                    actionProp.FindPropertyRelative("GuideTitle").stringValue = "1027";
                    actionProp.FindPropertyRelative("GuideBody").stringValue = "Enemy_Specter";
                    actionProp.FindPropertyRelative("DurationMs").intValue = 8;
                    break;

                case ActionQuickTemplate.SetGateDoorOpen:
                    actionProp.FindPropertyRelative("Type").enumValueIndex = (int)ActionType.SetGateDoorOpen;
                    actionProp.FindPropertyRelative("StringVal").stringValue = "";
                    actionProp.FindPropertyRelative("ParamId").intValue = 1;
                    actionProp.FindPropertyRelative("GroupId").intValue = 1;
                    actionProp.FindPropertyRelative("DurationMs").intValue = 900;
                    actionProp.FindPropertyRelative("Position").vector3Value = Vector3.zero;
                    break;

                case ActionQuickTemplate.PlayVfx:
                    actionProp.FindPropertyRelative("Type").enumValueIndex = (int)ActionType.PlayVfx;
                    actionProp.FindPropertyRelative("VfxKey").stringValue = StageVfxKeys.MarkerCyan;
                    actionProp.FindPropertyRelative("DurationMs").intValue = 1200;
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

                case ConditionType.AreaExit:
                    RectInt exitArea = condProp.FindPropertyRelative("Area").rectIntValue;
                    return $"영역 이탈 ({exitArea.x}, {exitArea.y}, {exitArea.width}, {exitArea.height})";

                case ConditionType.AreaPlayerCount:
                    RectInt countArea = condProp.FindPropertyRelative("Area").rectIntValue;
                    bool useParticipants = (StageCountRequirementMode)condProp.FindPropertyRelative("CountRequirement").enumValueIndex == StageCountRequirementMode.ParticipantCount;
                    string required = useParticipants ? "참여 인원" : condProp.FindPropertyRelative("Count").intValue.ToString();
                    return $"영역 인원 {required}명 ({countArea.x}, {countArea.y}, {countArea.width}, {countArea.height})";

                case ConditionType.AreaHoldBeats:
                    RectInt holdArea = condProp.FindPropertyRelative("Area").rectIntValue;
                    return $"영역 체류 {condProp.FindPropertyRelative("Count").intValue} Beat ({holdArea.x}, {holdArea.y}, {holdArea.width}, {holdArea.height})";

                case ConditionType.TimeElapsed:
                    int ms = condProp.FindPropertyRelative("Count").intValue;
                    return $"경과 시간 {ms / 1000f:0.0}s";

                case ConditionType.ObjectInteracted:
                    return $"오브젝트 {condProp.FindPropertyRelative("TargetId").intValue} 상호작용";

                case ConditionType.ObjectPairInteracted:
                    return $"오브젝트 {condProp.FindPropertyRelative("TargetId").intValue}+{condProp.FindPropertyRelative("SecondaryTargetId").intValue} 상호작용";

                case ConditionType.ObjectStateEquals:
                    return $"오브젝트 {condProp.FindPropertyRelative("TargetId").intValue} 상태={condProp.FindPropertyRelative("Count").intValue}";
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

                case ActionType.FinGame:
                    return "Fin Game 결과 UI";

                case ActionType.SpawnObject:
                    string objectKey = actionProp.FindPropertyRelative("HeaderParam").stringValue;
                    return string.IsNullOrWhiteSpace(objectKey)
                        ? $"오브젝트 {actionProp.FindPropertyRelative("ParamId").intValue} 소환"
                        : $"오브젝트 {objectKey} 소환";

                case ActionType.ShowGuide:
                    string title = actionProp.FindPropertyRelative("GuideTitle").stringValue;
                    return string.IsNullOrWhiteSpace(title) ? "가이드 UI 표시" : $"가이드: {title}";

                case ActionType.ShowTutorialPanel:
                    string panelId = actionProp.FindPropertyRelative("StringVal").stringValue;
                    string panelImage = actionProp.FindPropertyRelative("GuideImageResource").stringValue;
                    if (!string.IsNullOrWhiteSpace(panelId))
                        return $"튜토리얼 패널 표시: {panelId}";
                    return string.IsNullOrWhiteSpace(panelImage) ? "튜토리얼 패널 표시" : $"튜토리얼 패널: {panelImage}";

                case ActionType.HideTutorialPanel:
                    string hidePanelId = actionProp.FindPropertyRelative("StringVal").stringValue;
                    return string.IsNullOrWhiteSpace(hidePanelId) ? "튜토리얼 패널 숨김" : $"튜토리얼 패널 숨김: {hidePanelId}";

                case ActionType.SetObjectState:
                    return $"오브젝트 {actionProp.FindPropertyRelative("ParamId").intValue} 상태={actionProp.FindPropertyRelative("GroupId").intValue}";

                case ActionType.RemoveEntityGroup:
                    return $"Group {actionProp.FindPropertyRelative("ParamId").intValue} 제거";

                case ActionType.SetSceneObjectActive:
                    string sceneTargetKey = actionProp.FindPropertyRelative("StringVal").stringValue;
                    int sceneTargetGroup = actionProp.FindPropertyRelative("ParamId").intValue;
                    bool sceneTargetActive = actionProp.FindPropertyRelative("GroupId").intValue != 0;
                    string sceneTarget = !string.IsNullOrWhiteSpace(sceneTargetKey)
                        ? sceneTargetKey
                        : $"Group {sceneTargetGroup}";
                    return $"Scene Object {sceneTarget} {(sceneTargetActive ? "표시" : "숨김")}";

                case ActionType.SetSummonPortalActive:
                    string portalKey = actionProp.FindPropertyRelative("StringVal").stringValue;
                    int summonGroup = actionProp.FindPropertyRelative("ParamId").intValue;
                    bool portalActive = actionProp.FindPropertyRelative("GroupId").intValue != 0;
                    string portalLabel = string.IsNullOrWhiteSpace(portalKey) ? $"Group {summonGroup}" : portalKey;
                    return $"Summon Portal {portalLabel} {(portalActive ? "시작" : "중지")}";

                case ActionType.SetGateDoorOpen:
                    string gateTargetKey = actionProp.FindPropertyRelative("StringVal").stringValue;
                    int gateTargetGroup = actionProp.FindPropertyRelative("ParamId").intValue;
                    bool gateOpen = actionProp.FindPropertyRelative("GroupId").intValue != 0;
                    string gateTarget = !string.IsNullOrWhiteSpace(gateTargetKey)
                        ? gateTargetKey
                        : $"Group {gateTargetGroup}";
                    return $"Gate {gateTarget} {(gateOpen ? "열기" : "닫기")}";

                case ActionType.PlayVfx:
                    string vfxKey = actionProp.FindPropertyRelative("VfxKey").stringValue;
                    int targetId = actionProp.FindPropertyRelative("ParamId").intValue;
                    int secondTargetId = actionProp.FindPropertyRelative("GroupId").intValue;
                    string targetLabel = targetId > 0
                        ? secondTargetId > 0 ? $" -> {targetId}, {secondTargetId}" : $" -> {targetId}"
                        : string.Empty;
                    string vfxLabel = StageVfxCatalog.TryGetDefinition(vfxKey, out var vfxDefinition)
                        ? vfxDefinition.DisplayName
                        : vfxKey;
                    return string.IsNullOrWhiteSpace(vfxLabel) ? "VFX 재생" : $"VFX: {vfxLabel}{targetLabel}";
            }

            return type.ToString();
        }

        private void DrawAreaHandle(EventInfoSO evt, ConditionInfoSO cond)
        {
            Color areaColor = evt != null && evt.Visual != null ? evt.Visual.SceneColor : ConditionAccent;
            Vector3 center = GetAreaCenter(cond);

            if (cond.AreaShape == StageAreaShapeType.CustomCells && cond.AreaCells != null && cond.AreaCells.Count > 0)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 movedCenter = Handles.PositionHandle(center, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 delta = RoundVector(movedCenter - center);
                    int dx = Mathf.RoundToInt(delta.x);
                    int dz = Mathf.RoundToInt(delta.z);
                    if (dx != 0 || dz != 0)
                    {
                        Undo.RecordObject(_currentStage, "Move Custom Area");
                        cond.Area = new RectInt(cond.Area.x + dx, cond.Area.y + dz, cond.Area.width, cond.Area.height);
                        for (int i = 0; i < cond.AreaCells.Count; i++)
                        {
                            Vector2Int cell = cond.AreaCells[i];
                            cond.AreaCells[i] = new Vector2Int(cell.x + dx, cell.y + dz);
                        }
                        MarkCurrentStageDirtyAndExport();
                        center = GetAreaCenter(cond);
                    }
                }
            }
            else
            {
                Vector3 size = new Vector3(Mathf.Max(1, cond.Area.width), 1.0f, Mathf.Max(1, cond.Area.height));
                center = new Vector3(cond.Area.x + size.x * 0.5f, 0, cond.Area.y + size.z * 0.5f);

                _boundsHandle.center = center;
                _boundsHandle.size = size;
                _boundsHandle.SetColor(WithAlpha(areaColor, 0.45f));

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
                    MarkCurrentStageDirtyAndExport();
                    center = GetAreaCenter(cond);
                }
            }

            DrawAreaOutline(cond, areaColor);

            string label = cond.Type == ConditionType.AreaPlayerCount
                ? "Area Count"
                : cond.Type == ConditionType.AreaHoldBeats ? "Hold Area" : "Area";
            DrawSceneBadge(center + Vector3.up * 1.1f, $"{GetEventSceneLabel(evt)}\n{label}", areaColor);
        }

        private static void DrawAreaOutline(ConditionInfoSO cond, Color areaColor)
        {
            List<Vector2Int> cells = BuildAreaCells(cond);
            if (cells.Count == 0)
                return;

            var cellSet = new HashSet<Vector2Int>(cells);
            Color outlineColor = Color.Lerp(areaColor, Color.white, 0.35f);
            outlineColor.a = 0.95f;
            Handles.color = outlineColor;

            foreach (var cell in cells)
            {
                if (!cellSet.Contains(new Vector2Int(cell.x - 1, cell.y)))
                    DrawAreaOutlineEdge(cell.x, cell.y, cell.x, cell.y + 1);

                if (!cellSet.Contains(new Vector2Int(cell.x + 1, cell.y)))
                    DrawAreaOutlineEdge(cell.x + 1, cell.y, cell.x + 1, cell.y + 1);

                if (!cellSet.Contains(new Vector2Int(cell.x, cell.y - 1)))
                    DrawAreaOutlineEdge(cell.x, cell.y, cell.x + 1, cell.y);

                if (!cellSet.Contains(new Vector2Int(cell.x, cell.y + 1)))
                    DrawAreaOutlineEdge(cell.x, cell.y + 1, cell.x + 1, cell.y + 1);
            }
        }

        private static void DrawAreaOutlineEdge(float x0, float z0, float x1, float z1)
        {
            const float outlineHeight = 0.08f;
            Vector3 start = new Vector3(x0, outlineHeight, z0);
            Vector3 end = new Vector3(x1, outlineHeight, z1);
            Handles.DrawAAPolyLine(5f, start, end);
        }

        private static List<Vector2Int> BuildAreaCells(ConditionInfoSO cond)
        {
            var cells = new List<Vector2Int>();
            if (cond == null)
                return cells;

            var seen = new HashSet<Vector2Int>();
            if (cond.AreaShape == StageAreaShapeType.CustomCells && cond.AreaCells != null && cond.AreaCells.Count > 0)
            {
                for (int i = 0; i < cond.AreaCells.Count; i++)
                {
                    Vector2Int cell = cond.AreaCells[i];
                    if (seen.Add(cell))
                        cells.Add(cell);
                }
                return cells;
            }

            int width = Mathf.Max(1, cond.Area.width);
            int height = Mathf.Max(1, cond.Area.height);
            for (int y = cond.Area.y; y < cond.Area.y + height; y++)
            {
                for (int x = cond.Area.x; x < cond.Area.x + width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (seen.Add(cell))
                        cells.Add(cell);
                }
            }

            return cells;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_currentStage == null)
            {
                return;
            }

            for (int i = 0; i < _currentStage.InitialSpawns.Count; i++)
            {
                DrawPlacementHandle(_currentStage.InitialSpawns[i], i, "Spawn", SpawnAccent, "M");
            }

            for (int i = 0; i < _currentStage.InitialObjects.Count; i++)
            {
                DrawPlacementHandle(_currentStage.InitialObjects[i], i, "Object", ObjectAccent, "O");
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

                List<Vector3> conditionPoints = new List<Vector3>();
                List<Vector3> actionPoints = new List<Vector3>();

                foreach (var cond in evt.Conditions)
                {
                    if (cond.Type == ConditionType.AreaEnter
                        || cond.Type == ConditionType.AreaExit
                        || cond.Type == ConditionType.AreaPlayerCount
                        || cond.Type == ConditionType.AreaHoldBeats)
                    {
                        DrawAreaHandle(evt, cond);
                        conditionPoints.Add(GetAreaCenter(cond));
                    }
                    else if (cond.Type == ConditionType.ObjectInteracted)
                    {
                        if (TryGetPreviewPositionByGroup(cond.TargetId, out var targetPos))
                        {
                            DrawSceneBadge(targetPos + Vector3.up * 1.75f, "Interact", ConditionAccent);
                            conditionPoints.Add(targetPos);
                        }
                    }
                    else if (cond.Type == ConditionType.ObjectPairInteracted)
                    {
                        if (TryGetPreviewPositionByGroup(cond.TargetId, out var firstPos))
                        {
                            DrawSceneBadge(firstPos + Vector3.up * 1.75f, "Pair A", ConditionAccent);
                            conditionPoints.Add(firstPos);
                        }

                        if (TryGetPreviewPositionByGroup(cond.SecondaryTargetId, out var secondPos))
                        {
                            DrawSceneBadge(secondPos + Vector3.up * 1.75f, "Pair B", ConditionAccent);
                            conditionPoints.Add(secondPos);
                        }

                        if (TryGetPreviewPositionByGroup(cond.TargetId, out var a)
                            && TryGetPreviewPositionByGroup(cond.SecondaryTargetId, out var b))
                        {
                            Handles.color = WithAlpha(ConditionAccent, 0.75f);
                            Handles.DrawAAPolyLine(4f, a + Vector3.up * 0.35f, b + Vector3.up * 0.35f);
                        }
                    }
                }

                foreach (var action in evt.Actions)
                {
                    if (DrawActionHandle(evt, action, out var actionPos))
                    {
                        actionPoints.Add(actionPos);
                    }
                }

                if (_showEventLinks && evt.Visual != null && evt.Visual.DrawSceneLinks)
                {
                    DrawEventLinks(conditionPoints, actionPoints, evt.Visual.SceneColor);
                }
            }
        }

        private void DrawPlacementHandle(SpawnInfoSO item, int index, string kind, Color accent, string icon)
        {
            if (item == null)
            {
                return;
            }

            Vector3 pos = item.Position;
            Handles.color = accent;
            Vector2Int footprint = ResolveObjectSize(item.ObjectSize);
            Vector3 footprintCenter = pos + new Vector3((footprint.x - 1) * 0.5f, 0.05f, (footprint.y - 1) * 0.5f);
            Handles.DrawWireCube(footprintCenter, new Vector3(footprint.x, 0.02f, footprint.y));
            Handles.DrawWireDisc(pos + Vector3.up * 0.05f, Vector3.up, 0.42f);
            Handles.SphereHandleCap(0, pos + Vector3.up * 0.12f, Quaternion.identity, 0.18f, EventType.Repaint);

            if (_showSceneLabels)
            {
                string sizeLabel = footprint.x > 1 || footprint.y > 1 ? $"\n{footprint.x}x{footprint.y}" : "";
                string label = $"{icon} {kind} {index}\n{GetPlacementDisplayName(item)}{sizeLabel}";
                DrawSceneBadge(pos + Vector3.up * 1.25f, label, accent);
            }

            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(pos, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_currentStage, $"Move {kind}");
                item.Position = RoundVector(newPos);
                MarkCurrentStageDirtyAndExport();
                if (_autoSyncPreview)
                {
                    SyncPreviewTransform(kind, index, item);
                }
            }
        }

        private bool DrawActionHandle(EventInfoSO evt, ActionInfoSO action, out Vector3 pos)
        {
            pos = action != null ? action.Position : Vector3.zero;
            if (action == null)
            {
                return false;
            }

            if (ActionUsesPosition(action.Type))
            {
                Color accent = GetActionSceneColor(action.Type);
                Handles.color = accent;
                if (action.Type == ActionType.SpawnObject)
                {
                    Vector2Int footprint = ResolveObjectSize(action.ObjectSize);
                    Handles.DrawWireCube(
                        pos + new Vector3((footprint.x - 1) * 0.5f, 0.08f, (footprint.y - 1) * 0.5f),
                        new Vector3(footprint.x, 0.02f, footprint.y));
                }
                Handles.DrawWireDisc(pos + Vector3.up * 0.08f, Vector3.up, 0.36f);

                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(pos, Quaternion.identity);
                DrawSceneBadge(pos + Vector3.up * 1.1f, $"{GetEventSceneLabel(evt)}\n{action.Type}", accent);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_currentStage, "Move Action");
                    action.Position = RoundVector(newPos);
                    MarkCurrentStageDirtyAndExport();
                }

                pos = action.Position;
                return true;
            }

            if (action.Type == ActionType.ShowGuide)
            {
                DrawSceneBadge(Vector3.up * 2.1f, $"{GetEventSceneLabel(evt)}\nGuide UI", BasicAccent);
            }
            else if (action.Type == ActionType.ShowTutorialPanel)
            {
                DrawSceneBadge(Vector3.up * 2.35f, $"{GetEventSceneLabel(evt)}\nTutorial Panel In", BasicAccent);
            }
            else if (action.Type == ActionType.HideTutorialPanel)
            {
                DrawSceneBadge(Vector3.up * 2.35f, $"{GetEventSceneLabel(evt)}\nTutorial Panel Out", BasicAccent);
            }

            return false;
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

        private void SyncScenePreview()
        {
            if (_currentStage == null)
                return;

            GameObject root = GetOrCreatePreviewRoot();
            ClearChildren(root.transform);

            if (_currentStage.MapPrefab != null)
            {
                GameObject mapPreview = InstantiatePreviewObject(_currentStage.MapPrefab, root.transform);
                mapPreview.name = "MapPrefab";
            }

            for (int i = 0; i < _currentStage.InitialSpawns.Count; i++)
            {
                CreatePlacementPreview(root.transform, _currentStage.InitialSpawns[i], i, "Spawn", SpawnAccent, PrimitiveType.Capsule);
            }

            for (int i = 0; i < _currentStage.InitialObjects.Count; i++)
            {
                CreatePlacementPreview(root.transform, _currentStage.InitialObjects[i], i, "Object", ObjectAccent, PrimitiveType.Cube);
            }

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            SceneView.RepaintAll();
        }

        private void PullPreviewTransformsToStage()
        {
            GameObject root = FindPreviewRoot();
            if (_currentStage == null || root == null)
                return;

            Undo.RecordObject(_currentStage, "Pull Stage Preview Transforms");
            PullPreviewList(root.transform, _currentStage.InitialSpawns, "Spawn");
            PullPreviewList(root.transform, _currentStage.InitialObjects, "Object");
            EditorUtility.SetDirty(_currentStage);
            if (_autoExportJson)
                ExportCurrentStageJson(showDialog: false);
            Repaint();
            SceneView.RepaintAll();
        }

        private void ClearScenePreview()
        {
            GameObject root = FindPreviewRoot();
            if (root == null)
                return;

            DestroyImmediate(root);
            SceneView.RepaintAll();
        }

        private GameObject GetOrCreatePreviewRoot()
        {
            GameObject root = FindPreviewRoot();
            if (root != null)
            {
                EnsurePreviewRuntimeVisibility(root);
                return root;
            }

            root = new GameObject(GetPreviewRootName());
            root.hideFlags = HideFlags.DontSaveInBuild;
            EnsurePreviewRuntimeVisibility(root);
            return root;
        }

        private static void EnsurePreviewRuntimeVisibility(GameObject root)
        {
            if (root == null)
                return;

            root.hideFlags = HideFlags.DontSaveInBuild;
            if (root.GetComponent<StagePreviewPlayModeVisibility>() == null)
                root.AddComponent<StagePreviewPlayModeVisibility>();
        }

        private GameObject FindPreviewRoot()
        {
            string rootName = GetPreviewRootName();
            return GameObject.Find(rootName);
        }

        private string GetPreviewRootName()
        {
            string mapId = _currentStage != null && !string.IsNullOrWhiteSpace(_currentStage.MapId)
                ? _currentStage.MapId.Trim()
                : "Stage";
            return PreviewRootPrefix + mapId;
        }

        private void CreatePlacementPreview(Transform root, SpawnInfoSO item, int index, string kind, Color accent, PrimitiveType fallbackPrimitive)
        {
            if (item == null || !item.PlaceInScene)
                return;

            GameObject prefab = ResolvePreviewPrefab(item);
            GameObject preview = prefab != null
                ? InstantiatePreviewObject(prefab, root)
                : GameObject.CreatePrimitive(fallbackPrimitive);

            preview.transform.SetParent(root, false);
            preview.name = BuildPreviewChildName(kind, index, item.EntityKey);
            ApplyPlacementTransform(preview.transform, item);

            if (prefab == null)
            {
                var renderer = preview.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = CreatePreviewMaterial(accent);
            }

            preview.hideFlags = HideFlags.DontSaveInBuild;
        }

        private GameObject InstantiatePreviewObject(GameObject prefab, Transform parent)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                instance = Instantiate(prefab);

            instance.transform.SetParent(parent, false);
            instance.hideFlags = HideFlags.DontSaveInBuild;
            return instance;
        }

        private Material CreatePreviewMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            mat.color = WithAlpha(color, 0.72f);
            return mat;
        }

        private void PullPreviewList(Transform root, List<SpawnInfoSO> list, string kind)
        {
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                Transform child = FindPreviewChild(root, kind, i);
                if (child == null || list[i] == null)
                    continue;

                list[i].Position = RoundVector(child.position);
                list[i].EulerAngles = RoundRuntimeEuler(child.eulerAngles);
                list[i].Scale = child.localScale == Vector3.zero ? Vector3.one : child.localScale;
            }
        }

        private void SyncPreviewTransform(string kind, int index, SpawnInfoSO item)
        {
            GameObject root = FindPreviewRoot();
            if (root == null)
                return;

            Transform child = FindPreviewChild(root.transform, kind, index);
            if (child == null)
                return;

            ApplyPlacementTransform(child, item);
        }

        private Transform FindPreviewChild(Transform root, string kind, int index)
        {
            string prefix = $"{kind}_{index:000}_";
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name.StartsWith(prefix, StringComparison.Ordinal))
                    return child;
            }

            return null;
        }

        private void ApplyPlacementTransform(Transform target, SpawnInfoSO item)
        {
            target.position = item.Position;
            target.rotation = Quaternion.Euler(ToRuntimeEuler(item.EulerAngles));
            target.localScale = item.Scale == Vector3.zero ? Vector3.one : item.Scale;
        }

        private static Vector3 ToRuntimeEuler(Vector3 eulerAngles)
            => new Vector3(0f, eulerAngles.y, 0f);

        private static Vector3 RoundRuntimeEuler(Vector3 eulerAngles)
            => RoundVector(ToRuntimeEuler(eulerAngles));

        private static Vector2Int ResolveObjectSize(Vector2Int objectSize)
            => new Vector2Int(Mathf.Max(1, objectSize.x), Mathf.Max(1, objectSize.y));

        private GameObject ResolvePreviewPrefab(SpawnInfoSO item)
        {
            if (item.PreviewPrefabOverride != null)
                return item.PreviewPrefabOverride;

            if (TryFindRegistry(item.EntityKey, out var registry) && registry.EntityDef != null)
                return registry.EntityDef.Prefab;

            return null;
        }

        private bool TryFindRegistry(string entityKey, out StageRegisteredEntity registry)
        {
            registry = null;
            if (_currentStage?.Registry == null || string.IsNullOrWhiteSpace(entityKey))
                return false;

            for (int i = 0; i < _currentStage.Registry.Count; i++)
            {
                var item = _currentStage.Registry[i];
                if (item == null)
                    continue;

                if (string.Equals(item.Key, entityKey, StringComparison.OrdinalIgnoreCase))
                {
                    registry = item;
                    return true;
                }
            }

            return false;
        }

        private static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(root.GetChild(i).gameObject);
            }
        }

        private static string BuildPreviewChildName(string kind, int index, string entityKey)
        {
            string safeKey = string.IsNullOrWhiteSpace(entityKey) ? "None" : entityKey.Trim().Replace(' ', '_');
            return $"{kind}_{index:000}_{safeKey}";
        }

        private string GetPlacementDisplayName(SpawnInfoSO item)
        {
            string key = string.IsNullOrWhiteSpace(item.EntityKey) ? "<no key>" : item.EntityKey.Trim();
            int groupId = item.OverrideGroupId;
            if (groupId < 0 && TryFindRegistry(item.EntityKey, out var registry))
                groupId = registry.DefaultGroupId;

            string label = string.IsNullOrWhiteSpace(item.Label) ? key : item.Label.Trim();
            return groupId > 0 ? $"{label}\nGroup {groupId}" : label;
        }

        private bool TryGetPreviewPositionByGroup(int groupId, out Vector3 position)
        {
            position = Vector3.zero;
            if (groupId <= 0 || _currentStage?.InitialObjects == null)
                return false;

            for (int i = 0; i < _currentStage.InitialObjects.Count; i++)
            {
                var item = _currentStage.InitialObjects[i];
                if (item == null)
                    continue;

                int itemGroupId = item.OverrideGroupId;
                if (itemGroupId < 0 && TryFindRegistry(item.EntityKey, out var registry))
                    itemGroupId = registry.DefaultGroupId;

                if (itemGroupId != groupId)
                    continue;

                position = item.Position;
                return true;
            }

            return false;
        }

        private static Vector3 GetAreaCenter(ConditionInfoSO cond)
        {
            if (cond != null
                && cond.AreaShape == StageAreaShapeType.CustomCells
                && cond.AreaCells != null
                && cond.AreaCells.Count > 0)
            {
                Vector2Int first = cond.AreaCells[0];
                int minX = first.x;
                int minY = first.y;
                int maxX = first.x;
                int maxY = first.y;

                for (int i = 1; i < cond.AreaCells.Count; i++)
                {
                    Vector2Int cell = cond.AreaCells[i];
                    minX = Mathf.Min(minX, cell.x);
                    minY = Mathf.Min(minY, cell.y);
                    maxX = Mathf.Max(maxX, cell.x);
                    maxY = Mathf.Max(maxY, cell.y);
                }

                return new Vector3(
                    minX + (maxX - minX + 1) * 0.5f,
                    0f,
                    minY + (maxY - minY + 1) * 0.5f);
            }

            return new Vector3(
                cond.Area.x + cond.Area.width * 0.5f,
                0f,
                cond.Area.y + cond.Area.height * 0.5f);
        }

        private static Vector3 RoundVector(Vector3 value)
        {
            return new Vector3(Mathf.Round(value.x), Mathf.Round(value.y), Mathf.Round(value.z));
        }

        private static bool ActionUsesPosition(ActionType type)
        {
            return type == ActionType.SpawnMonster
                   || type == ActionType.SpawnObject
                   || type == ActionType.OpenGate
                   || type == ActionType.SetSummonPortalActive
                   || type == ActionType.PlayVfx;
        }

        private static Color GetActionSceneColor(ActionType type)
        {
            switch (type)
            {
                case ActionType.SpawnMonster:
                    return SpawnAccent;
                case ActionType.SetSummonPortalActive:
                    return SpawnAccent;
                case ActionType.SpawnObject:
                    return ObjectAccent;
                case ActionType.OpenGate:
                    return ActionAccent;
                case ActionType.RemoveEntityGroup:
                    return ActionAccent;
                case ActionType.SetSceneObjectActive:
                    return ObjectAccent;
                case ActionType.SetGateDoorOpen:
                    return ActionAccent;
                case ActionType.PlayVfx:
                    return ConditionAccent;
                default:
                    return EventAccent;
            }
        }

        private void DrawEventLinks(List<Vector3> conditionPoints, List<Vector3> actionPoints, Color color)
        {
            if (conditionPoints == null || actionPoints == null)
                return;

            Handles.color = WithAlpha(color, 0.62f);
            foreach (var from in conditionPoints)
            {
                foreach (var to in actionPoints)
                {
                    Handles.DrawAAPolyLine(3f, from + Vector3.up * 0.45f, to + Vector3.up * 0.45f);
                }
            }
        }

        private static void DrawSceneBadge(Vector3 position, string text, Color accent)
        {
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(7, 7, 3, 3)
            };

            Handles.Label(position, text, style);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
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
