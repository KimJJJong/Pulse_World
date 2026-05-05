using UnityEngine;

[CreateAssetMenu(menuName = "UI/HudConfig")]
public class HudConfig : ScriptableObject
{
    [Header("HP")]
    public Color hpColor = Color.red;
    public Color hpDangerColor = Color.yellow;
    public float hpDangerRate = 0.3f;

    [Header("SP")]
    public Color spColor = Color.blue;

    [Header("Icons")]
    public Sprite[] skillIcons;   // 슬롯 인덱스 기준
    public Sprite[] itemIcons;

    [Header("Layout")]
    public int maxSkillSlots = 6;
}
