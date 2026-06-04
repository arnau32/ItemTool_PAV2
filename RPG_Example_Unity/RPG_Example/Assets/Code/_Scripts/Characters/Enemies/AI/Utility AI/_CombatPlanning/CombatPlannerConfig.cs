using UnityEngine;

[CreateAssetMenu(fileName = "Combat Planner Config", menuName = "Enemies/Combat Planner", order = 0)]
public class CombatPlannerConfig : ScriptableObject
{
    [Header("Planner")] public float evaluationRate = 0.15f;
    public float minScoreToAct = 35f;
    public float stickinessMultiplier = 1.8f;
    public float switchPenalty = 0.65f;
    public float interruptThreshold = 1.25f;
    public bool hasMultipleAttakcs = true;

    [Header("Defense Priority")]
    [Tooltip("Multiplier applied to Defense actions when the target is winding up an attack. " +
             "Scaled by the enemy's temperament defend weight, so aggressive enemies dodge less.")]
    public float defensePriorityMultiplier = 3f;

    [Header("Attack Tokens")]
    [Tooltip("If true, this enemy participates in the faction attack token system. Disable for bosses or enemies that should always be able to attack.")]
    public bool useAttackTokens = true;
}