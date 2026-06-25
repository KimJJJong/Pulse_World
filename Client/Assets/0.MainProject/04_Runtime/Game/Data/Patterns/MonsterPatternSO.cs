using System.Collections.Generic;
using UnityEngine;
// PatternsDto.cs에 정의된 타입(MonsterPatternDef, PhaseDef 등)을 사용

[CreateAssetMenu(fileName = "NewMonsterPattern", menuName = "RhythmRPG/Monster Pattern SO")]
public class MonsterPatternSO : ScriptableObject
{
    // 데이터를 직접 감쌈 (Wrapper)
    public MonsterPatternDef Data = new MonsterPatternDef();

    // Editor 편의를 위한 접근자
    public List<PhaseDef> Phases => Data.Phases;
}
// 중복된 클래스 정의 제거함 (PhaseData, SelectorData, ConditionType 등은 PatternsDto.cs 사용)
