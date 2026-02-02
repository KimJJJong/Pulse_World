using UnityEditor;
using UnityEngine;

namespace RhythmRPG.Editor.StageBuilder
{
    public class StageEditorWindow : EditorWindow
    {
        private StageDataSO _currentStage;
        private Vector2 _scrollPos;

        [MenuItem("RhythmRPG/Stage Editor")]
        public static void ShowWindow()
        {
            GetWindow<StageEditorWindow>("Stage Editor");
        }

        private void OnGUI()
        {
            GUILayout.Label("RhythmRPG Stage Builder", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 1. Selector
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

            // 2. Toolbar
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Export JSON", GUILayout.Height(30)))
            {
                StageExporter.Export(_currentStage);
                EditorUtility.DisplayDialog("Export Success", $"Exported {_currentStage.MapId}.json", "OK");
            }
            if (GUILayout.Button("Ping File", GUILayout.Width(100)))
            {
                EditorGUIUtility.PingObject(_currentStage);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            DrawLine();
            EditorGUILayout.Space();

            // 3. Editor Content (Inspector Style)
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            SerializedObject so = new SerializedObject(_currentStage);
            so.Update();

            EditorGUILayout.PropertyField(so.FindProperty("MapId"));
            EditorGUILayout.PropertyField(so.FindProperty("Description"));
            
            EditorGUILayout.Space();
            GUILayout.Label("Rhythm Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("Rhythm"));

            EditorGUILayout.Space();
            GUILayout.Label("Entity Registry (Define Entities & Keys)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("Registry"));

            EditorGUILayout.Space();
            GUILayout.Label("Initial Spawns (Monsters)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("InitialSpawns"));

            EditorGUILayout.Space();
            GUILayout.Label("Initial Objects (Gate/Trap/Etc)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("InitialObjects"));
            
            EditorGUILayout.Space();
            GUILayout.Label("Events", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("Events"));

            so.ApplyModifiedProperties();
            
            EditorGUILayout.EndScrollView();
        }

        private void CreateNewStageData()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create New Stage Data", "NewStage", "asset", "Save Stage Data");
            if (string.IsNullOrEmpty(path)) return;

            var newData = CreateInstance<StageDataSO>();
            AssetDatabase.CreateAsset(newData, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _currentStage = newData;
        }

        private void DrawLine()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        }
    }
}
