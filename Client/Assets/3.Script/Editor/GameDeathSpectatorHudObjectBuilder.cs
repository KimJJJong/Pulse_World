using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameDeathSpectatorHudObjectBuilder
{
    private const string PrefabPath = "Assets/Resources/UI/GameDeathSpectatorHud.prefab";

    [MenuItem("RhythmRPG/Editors/UI/Create Game Death HUD Prefab")]
    public static void CreatePrefab()
    {
        EnsureResourcesUiFolder();

        GameObject hudObject = CreateHudObject();
        try
        {
            PrefabUtility.SaveAsPrefabAsset(hudObject, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[GameDeathSpectatorHudObjectBuilder] Saved {PrefabPath}.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hudObject);
        }
    }

    [MenuItem("RhythmRPG/Editors/UI/Place Game Death HUD In Active Scene")]
    public static void PlaceInActiveScene()
    {
        GameDeathSpectatorHud existing = FindSceneHud();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("[GameDeathSpectatorHudObjectBuilder] GameDeathSpectatorHud already exists in the active scene.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject hudObject;
        if (prefab != null)
        {
            hudObject = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (hudObject == null)
                hudObject = CreateHudObject();
        }
        else
        {
            hudObject = CreateHudObject();
        }

        Undo.RegisterCreatedObjectUndo(hudObject, "Place Game Death HUD");
        Selection.activeGameObject = hudObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[GameDeathSpectatorHudObjectBuilder] Placed GameDeathSpectatorHud in the active scene.");
    }

    private static GameObject CreateHudObject()
    {
        var go = new GameObject(
            nameof(GameDeathSpectatorHud),
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(GameDeathSpectatorHud));

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;

        go.GetComponent<GameDeathSpectatorHud>().RebuildPlacedUiForEditor();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
        return go;
    }

    private static GameDeathSpectatorHud FindSceneHud()
    {
        var candidates = Resources.FindObjectsOfTypeAll<GameDeathSpectatorHud>();
        foreach (var candidate in candidates)
        {
            if (candidate != null && candidate.gameObject.scene.IsValid())
                return candidate;
        }

        return null;
    }

    private static void EnsureResourcesUiFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
            AssetDatabase.CreateFolder("Assets/Resources", "UI");
    }
}
