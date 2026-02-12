using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls; // [NEW]

namespace RhythmRPG.Editor.StageBuilder
{
    public class StageEditorWindow : EditorWindow
    {
        private StageDataSO _currentStage;
        private Vector2 _scrollPos;
        private BoxBoundsHandle _boundsHandle = new BoxBoundsHandle(); // [NEW]

        [MenuItem("RhythmRPG/Stage Editor")]
        public static void ShowWindow()
        {
            GetWindow<StageEditorWindow>("Stage Editor");
        }

// ... (Keep OnGUI logic same until DrawAreaHandle) ...

        private void DrawAreaHandle(EventInfoSO evt, ConditionInfoSO cond)
        {
             // 1. Map RectInt (2D X,Y) to World Bounds (3D X,Z)
            // RectInt.Y is treated as World Z.
            Vector3 center = new Vector3(cond.Area.x + cond.Area.width * 0.5f, 0, cond.Area.y + cond.Area.height * 0.5f);
            Vector3 size = new Vector3(cond.Area.width, 1.0f, cond.Area.height); // Height 1.0 visible enough

            _boundsHandle.center = center;
            _boundsHandle.size = size;
            _boundsHandle.SetColor(new Color(0f, 1f, 0f, 0.5f));

            EditorGUI.BeginChangeCheck();
            _boundsHandle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_currentStage, "Resize Area");

                // 2. Map World Bounds back to RectInt
                // Round to Int for Grid
                int minX = Mathf.RoundToInt(_boundsHandle.center.x - _boundsHandle.size.x * 0.5f);
                int minZ = Mathf.RoundToInt(_boundsHandle.center.z - _boundsHandle.size.z * 0.5f);
                int width = Mathf.Max(1, Mathf.RoundToInt(_boundsHandle.size.x));
                int height = Mathf.Max(1, Mathf.RoundToInt(_boundsHandle.size.z));

                cond.Area = new RectInt(minX, minZ, width, height);
                EditorUtility.SetDirty(_currentStage);
            }
            
            Handles.Label(center, $"Event {evt.EventId}\nArea");
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
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_currentStage == null) return;

            // Draw Spawns
            for (int i = 0; i < _currentStage.InitialSpawns.Count; i++)
            {
                var spawn = _currentStage.InitialSpawns[i];
                Vector3 pos = spawn.Position;
                
                // Draw Handle
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(pos, Quaternion.identity);
                Handles.Label(pos + Vector3.up * 1f, $"Spawn {i}\n({spawn.EntityKey})");

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_currentStage, "Move Spawn");
                    // Snap to integer grid if needed, or keep float
                    spawn.Position = new Vector3(Mathf.Round(newPos.x), Mathf.Round(newPos.y), Mathf.Round(newPos.z));
                    EditorUtility.SetDirty(_currentStage);
                }
            }

            // Draw Objects
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

            // Draw Events (Actions & Conditions)
            if (_currentStage.Events != null)
            {
                foreach (var evt in _currentStage.Events)
                {
                    // Draw Area Conditions
                    foreach (var cond in evt.Conditions)
                    {
                        if (cond.Type == ConditionType.AreaEnter)
                        {
                            DrawAreaHandle(evt, cond);
                        }
                    }

                    // Draw Action Locations (if any)
                    foreach (var action in evt.Actions)
                    {
                        DrawActionHandle(evt, action);
                    }
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
                Handles.Label(pos, $"Event {evt.EventId}\n{action.Type}");
                
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_currentStage, "Move Action");
                    action.Position = new Vector3(Mathf.Round(newPos.x), Mathf.Round(newPos.y), Mathf.Round(newPos.z));
                    EditorUtility.SetDirty(_currentStage);
                }
            }
        }
    }
    
    public static class Extensions
    {
        // Helper to draw Rect on XZ plane
        public static Vector3[] ToWorldRect(this Rect r)
        {
            return new Vector3[]
            {
                new Vector3(r.x, 0, r.y),
                new Vector3(r.x + r.width, 0, r.y),
                new Vector3(r.x + r.width, 0, r.y + r.height),
                new Vector3(r.x, 0, r.y + r.height)
            };
        }
    }
}
