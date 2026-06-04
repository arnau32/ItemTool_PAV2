using UnityEngine;

[CreateAssetMenu(fileName = "HealerNpcData", menuName = "NPCs/Healer NPC Data")]
public class HealerNpcSO : ScriptableObject
{
    [Tooltip("Must match exactly the npcId used in NpcLevelService and LevelUpNpcAction.")]
    public string npcId;

    [Tooltip("Maximum level this NPC can reach.")]
    [Min(1)] public int maxLevel = 3;

    [Tooltip("Fraction of MaxHealth restored per level. Index 0 = level 1, index 1 = level 2, etc. Length must equal maxLevel.")]
    [Range(0f, 1f)] public float[] healPercentPerLevel;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (healPercentPerLevel != null && healPercentPerLevel.Length != maxLevel)
        {
            System.Array.Resize(ref healPercentPerLevel, maxLevel);
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
