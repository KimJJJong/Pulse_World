#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Linq;

public sealed class SkillEditorWindow : EditorWindow
{
    [SerializeField] private SkillsAsset _asset;
    private ReorderableList _list;

    [MenuItem("RhythmRPG/Editors/Skill Editor")]
    public static void Open() => GetWindow<SkillEditorWindow>("Skill Editor");

    private void OnEnable()
    {
        if (_asset == null) return;
        BuildList();
    }

    private void BuildList()
    {
        var skills = _asset.Data.Skills;
        _list = new ReorderableList(skills, typeof(SkillDef), true, true, true, true);

        _list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Skills");

        _list.onAddCallback = _ =>
        {
            skills.Add(new SkillDef { SkillId = "NewSkill" });
            EditorUtility.SetDirty(_asset);
        };

        _list.onRemoveCallback = _ =>
        {
            if (_list.index >= 0 && _list.index < skills.Count)
                skills.RemoveAt(_list.index);
            EditorUtility.SetDirty(_asset);
        };

        _list.drawElementCallback = (rect, index, active, focused) =>
        {
            var s = skills[index];
            rect.y += 2;
            float w = rect.width;

            var r0 = new Rect(rect.x, rect.y, w * 0.5f, EditorGUIUtility.singleLineHeight);
            var r1 = new Rect(rect.x + w * 0.52f, rect.y, w * 0.48f, EditorGUIUtility.singleLineHeight);

            s.SkillId = EditorGUI.TextField(r0, s.SkillId);
            s.Shape = (SkillAoeShape)EditorGUI.EnumPopup(r1, s.Shape);
        };
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);

        _asset = (SkillsAsset)EditorGUILayout.ObjectField("Skills Asset", _asset, typeof(SkillsAsset), false);

        if (_asset == null)
        {
            EditorGUILayout.HelpBox("SkillsAsset를 지정해줘. (Assets/Data/Skills/Skills.asset 같은 식)", MessageType.Info);
            return;
        }

        if (_list == null) BuildList();

        EditorGUI.BeginChangeCheck();
        _list.DoLayoutList();

        // 상세 편집 영역
        var skills = _asset.Data.Skills;
        if (_list.index >= 0 && _list.index < skills.Count)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Selected Skill", EditorStyles.boldLabel);

            var s = skills[_list.index];

            s.SkillId = EditorGUILayout.TextField("SkillId", s.SkillId);
            s.CooldownBeats = EditorGUILayout.IntField("CooldownBeats", s.CooldownBeats);
            s.Damage = EditorGUILayout.IntField("Damage", s.Damage);
            s.BlockedByWall = EditorGUILayout.Toggle("BlockedByWall", s.BlockedByWall);

            s.HitPlayers = EditorGUILayout.Toggle("HitPlayers", s.HitPlayers);
            s.HitMonsters = EditorGUILayout.Toggle("HitMonsters", s.HitMonsters);

            s.Shape = (SkillAoeShape)EditorGUILayout.EnumPopup("Shape", s.Shape);
            s.ParamA = EditorGUILayout.IntField("ParamA", s.ParamA);
            s.ParamB = EditorGUILayout.IntField("ParamB", s.ParamB);

            // 방향은 Line/Rect에서 주로 사용
            s.DirType = (SkillDirType)EditorGUILayout.EnumPopup("DirType", s.DirType);
            if (s.DirType == SkillDirType.Fixed)
                s.FixedDir = (FixedDir)EditorGUILayout.EnumPopup("FixedDir", s.FixedDir);

            s.Ticks = EditorGUILayout.IntField("Ticks", s.Ticks);
            s.TickIntervalBeats = EditorGUILayout.IntField("TickIntervalBeats", s.TickIntervalBeats);

            // 간단 검증
            if (string.IsNullOrWhiteSpace(s.SkillId))
                EditorGUILayout.HelpBox("SkillId는 비면 안됨", MessageType.Warning);
        }

        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(_asset);

        EditorGUILayout.Space(10);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Export skills.json"))
                EditorCommon.ExportJson(_asset.Data, "skills.json");

            if (GUILayout.Button("Auto Fix IDs (Trim)"))
            {
                foreach (var s in _asset.Data.Skills)
                    s.SkillId = (s.SkillId ?? "").Trim();
                EditorUtility.SetDirty(_asset);
            }
        }
    }

    public static string[] GetSkillIds(SkillsAsset skills)
    {
        if (skills == null) return new string[0];
        return skills.Data.Skills
            .Select(x => x.SkillId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
    }
}
#endif
