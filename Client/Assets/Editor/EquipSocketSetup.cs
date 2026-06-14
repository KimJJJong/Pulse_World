#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using RhythmRPG.Visual;

/// <summary>
/// 플레이어 캐릭터 프리팹에 CharacterVisualController + CharacterEquipSockets를 자동으로 추가하고
/// 기존 본(handslot.r, head, chest 등)을 소켓 필드에 연결합니다.
/// Menu: RhythmRPG/Editors/Setup/Equipment Sockets
/// </summary>
public class EquipSocketSetup
{
    private const string PlayersFolder = "Assets/Resources/Entity/Players";

    [MenuItem("RhythmRPG/Editors/Setup/Equipment Sockets")]
    public static void SetupAll()
    {
        var prefabPaths = FindPlayerPrefabPaths();

        if (prefabPaths.Length == 0)
        {
            Debug.LogWarning($"[EquipSocketSetup] No player prefabs found in '{PlayersFolder}'.");
            EditorUtility.DisplayDialog(
                "완료",
                $"'{PlayersFolder}' 폴더에서 처리할 프리팹을 찾지 못했습니다.\n\n플레이어 프리팹을 이 폴더로 옮긴 뒤 다시 실행해 주세요.",
                "OK");
            return;
        }

        int successCount = 0;
        foreach (var path in prefabPaths)
        {
            if (SetupPrefab(path)) successCount++;
        }

        // BoardView 안의 PlayerPrefab_Template도 처리
        string[] boardViewPaths = new[]
        {
            "Assets/Resources/BoardView.prefab",
            "Assets/Resources/GameInit/BoardView.prefab",
        };
        foreach (var path in boardViewPaths)
        {
            SetupPlayerTemplateInBoardView(path);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[EquipSocketSetup] Done. {successCount}/{prefabPaths.Length} entity prefabs processed.");
        EditorUtility.DisplayDialog("완료", $"{successCount}개 캐릭터 프리팹 소켓 셋업 완료!\n이제 플레이 해보세요.", "OK");
    }

    private static string[] FindPlayerPrefabPaths()
    {
        if (!AssetDatabase.IsValidFolder(PlayersFolder))
            return Array.Empty<string>();

        return AssetDatabase
            .FindAssets("t:Prefab", new[] { PlayersFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path)
            .ToArray();
    }

    private static bool SetupPrefab(string path)
    {
        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefabAsset == null)
        {
            Debug.LogWarning($"[EquipSocketSetup] Prefab not found: {path}");
            return false;
        }

        using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
        {
            var root = scope.prefabContentsRoot;
            ApplyToRoot(root, path);
        }
        return true;
    }

    private static void SetupPlayerTemplateInBoardView(string boardViewPath)
    {
        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(boardViewPath);
        if (prefabAsset == null) return;

        using (var scope = new PrefabUtility.EditPrefabContentsScope(boardViewPath))
        {
            var root = scope.prefabContentsRoot;
            var template = root.transform.Find("PlayerPrefab_Template");
            if (template == null)
            {
                Debug.LogWarning($"[EquipSocketSetup] PlayerPrefab_Template not found in {boardViewPath}");
                return;
            }
            ApplyToRoot(template.gameObject, boardViewPath + "/PlayerPrefab_Template");
        }
    }

    private static void ApplyToRoot(GameObject root, string label)
    {
        var sockets = root.GetComponent<CharacterEquipSockets>();
        if (sockets == null)
        {
            sockets = root.AddComponent<CharacterEquipSockets>();
            Debug.Log($"[EquipSocketSetup] Added CharacterEquipSockets to '{label}'");
        }

        var visual = root.GetComponent<CharacterVisualController>();
        if (visual == null)
        {
            visual = root.AddComponent<CharacterVisualController>();
            Debug.Log($"[EquipSocketSetup] Added CharacterVisualController to '{label}'");
        }

        WireSocket(root, ref sockets.RightHandSocket,  "handslot.r", "hand.r", "wrist.r");
        WireSocket(root, ref sockets.LeftHandSocket,   "handslot.l", "hand.l", "wrist.l");
        WireSocket(root, ref sockets.HeadSocket,       "head");
        WireSocket(root, ref sockets.BodySocket,       "chest", "spine");
        WireSocket(root, ref sockets.PantsSocket,      "hips");
        WireSocket(root, ref sockets.GlovesSocket,     "wrist.r", "hand.r");
        WireSocket(root, ref sockets.ShoesSocket,      "foot.r", "toes.r");
        WireSocket(root, ref sockets.AccessorySocket,  "head");
        WireSocket(root, ref sockets.BackSocket,       "spine", "chest");

        Debug.Log($"[EquipSocketSetup] '{label}' sockets wired:" +
            $" R={sockets.RightHandSocket?.name ?? "NULL"}" +
            $" L={sockets.LeftHandSocket?.name ?? "NULL"}" +
            $" Head={sockets.HeadSocket?.name ?? "NULL"}" +
            $" Accessory={sockets.AccessorySocket?.name ?? "NULL"}" +
            $" Body={sockets.BodySocket?.name ?? "NULL"}" +
            $" Pants={sockets.PantsSocket?.name ?? "NULL"}");
    }

    private static void WireSocket(GameObject root, ref Transform socketField, params string[] candidates)
    {
        if (socketField != null) return;
        foreach (var boneName in candidates)
        {
            var bone = FindDeep(root.transform, boneName);
            if (bone != null) { socketField = bone; return; }
        }
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
