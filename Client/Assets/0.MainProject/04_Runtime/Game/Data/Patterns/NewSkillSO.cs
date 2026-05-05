using UnityEngine;
using GameShared.Data; // Shared 네임스페이스 사용

namespace Client.Data // 클라용 네임스페이스
{
    [CreateAssetMenu(fileName = "NewSkill", menuName = "RhythmRPG/New Skill Definition")]
    public class NewSkillSO : ScriptableObject
    {
        // 인스펙터에서 바로 편집 가능 (SerializeReference 덕분)
        public NewSkillDef Data = new NewSkillDef();
    }
}
