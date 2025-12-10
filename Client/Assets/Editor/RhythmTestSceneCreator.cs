using UnityEditor;
using UnityEngine;

public static class RhythmTestSceneCreator
{
    [MenuItem("Tools/Rhythm/Generate Test Scene Objects")]
    public static void GenerateTestEnvironment()
    {
        Debug.Log("[RhythmTest] Generating test environment...");

        CreateIfNotExists<ClientGameState>("ClientGameState");
        CreateIfNotExists<ClientHandlers>("ClientHandlers");
        CreateIfNotExists<RhythmClient>("RhythmClient");
        var boardView = CreateIfNotExists<BoardView>("BoardView");
        CreateIfNotExists<RhythmInputController>("RhythmInputController");
        CreateIfNotExists<BeatDebugUI_TMP>("BeatDebugUI_TMP");

        CreateCamera();
        SetupBoardViewPrefabs(boardView);

        Debug.Log("[RhythmTest] Done. 🎵 테스트 환경이 씬에 생성되었습니다.");
    }

    // ---------------------------
    // Utility
    // ---------------------------

    private static void CreateCamera()
    {
        if (Camera.main != null) return;

        GameObject cam = new GameObject("MainCamera");
        Camera camera = cam.AddComponent<Camera>();
        cam.tag = "MainCamera";

        cam.transform.position = new Vector3(5, 10, -10);
        cam.transform.rotation = Quaternion.Euler(45, 45, 0);

        Debug.Log("[RhythmTest] Main Camera created");
    }

    private static T CreateIfNotExists<T>(string name) where T : Component
    {
        var existing = Object.FindFirstObjectByType<T>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject(name);
        var comp = go.AddComponent<T>();

        Debug.Log($"[RhythmTest] Created: {name}");
        return comp;
    }
    private static void SetupBoardViewPrefabs(BoardView boardView)
    {
        if (boardView == null)
            return;

        // Player Prefab
        if (boardView.playerPrefab == null)
        {
            var playerTemplate = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerTemplate.name = "PlayerPrefab_Template";
            playerTemplate.transform.SetParent(boardView.transform, false);

            if (playerTemplate.TryGetComponent<Renderer>(out var rend))
            {
                var mat = rend.sharedMaterial;
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Standard"));
                    rend.sharedMaterial = mat;
                }
                mat.color = Color.blue;
            }

            boardView.playerPrefab = playerTemplate;
        }

        // Monster Prefab
        if (boardView.monsterPrefab == null)
        {
            var monsterTemplate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            monsterTemplate.name = "MonsterPrefab_Template";
            monsterTemplate.transform.SetParent(boardView.transform, false);

            if (monsterTemplate.TryGetComponent<Renderer>(out var rend))
            {
                var mat = rend.sharedMaterial;
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Standard"));
                    rend.sharedMaterial = mat;
                }
                mat.color = Color.red;
            }

            boardView.monsterPrefab = monsterTemplate;
        }

        // Tile Prefab
        if (boardView.tilePrefab == null)
        {
            var tileTemplate = GameObject.CreatePrimitive(PrimitiveType.Quad);
            tileTemplate.name = "TilePrefab_Template";
            tileTemplate.transform.SetParent(boardView.transform, false);
            tileTemplate.transform.rotation = Quaternion.Euler(90, 0, 0); // 바닥으로

            if (tileTemplate.TryGetComponent<Renderer>(out var rend))
            {
                var mat = rend.sharedMaterial;
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Standard"));
                    rend.sharedMaterial = mat;
                }
                mat.color = new Color(0.3f, 0.3f, 0.3f);
            }

            boardView.tilePrefab = tileTemplate;
        }

        Debug.Log("[RhythmTest] BoardView prefabs set (Player/Monster/Tile).");
    }

}
