#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using ET = RhythmRPG.Editor.StageBuilder.EntityType;

/// <summary>
/// EntityDefinitionSO 에셋을 Resources/Data/ 에 생성/업데이트하고
/// Resources/Entity/ 의 프리팹을 자동으로 연결합니다.
/// EntityId = 서버의 AppearanceId(플레이어) 또는 ModelId(몬스터)
/// Menu: RhythmRPG/Editors/Setup/Entity Definitions
/// </summary>
public class EntityDefinitionSetup
{
    // SO를 생성/업데이트할 Resources/Data 폴더들
    // (두 곳 모두 관리 — 0.MainProject/Resources/Data 는 신규, Resources/Data 는 기존)
    static readonly string[] SO_FOLDERS = new[]
    {
        "Assets/Resources/Data",
        "Assets/0.MainProject/Resources/Data",
    };

    [MenuItem("RhythmRPG/Editors/Setup/Entity Definitions")]
    public static void SetupAll()
    {
        // 폴더 생성 보장
        foreach (var folder in SO_FOLDERS)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                var parts = folder.Split('/');
                var parent = string.Join("/", parts, 0, parts.Length - 1);
                AssetDatabase.CreateFolder(parent, parts[parts.Length - 1]);
                Debug.Log($"[EntityDefinitionSetup] Created folder: {folder}");
            }
        }

        // 엔티티 정의 목록
        // EntityId  : 서버가 패킷에 보내는 ID (AppearanceId 또는 ModelId)
        // soName    : SO 파일명 = EntityData.json ResourcePath 의 마지막 세그먼트
        // prefabName: Resources/Entity/ 의 프리팹 이름
        var defs = new[]
        {
            (id: 10,   soName: "Entity_10_Player_Barbarian",    prefabName: "Barbarian_InGame", prefabAssetPath: "",                                                   type: ET.Player),
            (id: 11,   soName: "Entity_11_Player_Mage",         prefabName: "Mage",             prefabAssetPath: "",                                                   type: ET.Player),
            (id: 12,   soName: "Entity_12_Player_Rogu",         prefabName: "",                 prefabAssetPath: "Assets/0.MainProject/Resources/Entity/Rogue.prefab", type: ET.Player),
            (id: 1000, soName: "Entity_1000_Monster_BlackPawn", prefabName: "",                 prefabAssetPath: "",                                                   type: ET.Monster),
            (id: 1001, soName: "Entity_1001_Monster_WhitePawn", prefabName: "",                 prefabAssetPath: "",                                                   type: ET.Monster),
        };

        int total = 0;

        foreach (var folder in SO_FOLDERS)
        {
            foreach (var d in defs)
            {
                string soPath = $"{folder}/{d.soName}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<RhythmRPG.Editor.StageBuilder.EntityDefinitionSO>(soPath);

                RhythmRPG.Editor.StageBuilder.EntityDefinitionSO so;
                if (existing == null)
                {
                    so = ScriptableObject.CreateInstance<RhythmRPG.Editor.StageBuilder.EntityDefinitionSO>();
                    AssetDatabase.CreateAsset(so, soPath);
                    Debug.Log($"[EntityDefinitionSetup] Created: {soPath}");
                }
                else
                {
                    so = existing;
                }

                so.EntityId   = d.id;
                so.EntityName = d.soName;
                so.Type       = d.type;

                // 프리팹 연결
                if (!string.IsNullOrEmpty(d.prefabAssetPath))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(d.prefabAssetPath);
                    if (prefab != null)
                    {
                        so.Prefab = prefab;
                        Debug.Log($"[EntityDefinitionSetup] ✅ {d.soName} → Prefab '{d.prefabAssetPath}'");
                    }
                    else
                    {
                        Debug.LogWarning($"[EntityDefinitionSetup] Prefab '{d.prefabAssetPath}' not found.");
                    }
                }
                else if (!string.IsNullOrEmpty(d.prefabName))
                {
                    var prefab = Resources.Load<GameObject>($"Entity/{d.prefabName}");
                    if (prefab != null)
                    {
                        so.Prefab = prefab;
                        Debug.Log($"[EntityDefinitionSetup] ✅ {d.soName} → Prefab '{d.prefabName}'");
                    }
                    else
                    {
                        Debug.LogWarning($"[EntityDefinitionSetup] Prefab 'Resources/Entity/{d.prefabName}' not found.");
                    }
                }

                EditorUtility.SetDirty(so);
                total++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[EntityDefinitionSetup] Done. {total} SO(s) processed.");
        EditorUtility.DisplayDialog(
            "완료",
            $"EntityDefinitionSO 셋업 완료! ({total}개 처리)\n\n" +
            "이제 플레이하면 EntityId 10=Barbarian, 11=Mage, 12=Rogu로 소환됩니다.",
            "OK");
    }
}
#endif
