using UnityEngine;
/// <summary>
/// Condition: shows a DialogueOption when the quest is NOT in the given state.
/// </summary>
[CreateAssetMenu(menuName = "Dialogue/Conditions/Quest State NOT")]
public class QuestStateNotCondition : DialogueCondition
{
    [Tooltip("Must match the QuestInfoSO asset name exactly.")]
    public string questId;

    [Tooltip("The state the quest must NOT be in for this option to appear.")]
    public Enums.QuestState excludedState;

    public override bool Evaluate()
    {
        if (string.IsNullOrEmpty(questId))
        {
            Debug.LogWarning("[QuestStateNotCondition] questId is empty.");
            return false;
        }

        if (!GameServices.TryGet<QuestService>(out var qs))
            return true; // Service missing — fail open so dialogue doesn't break.

        var quest = qs.GetQuestById(questId);
        return quest == null || quest.state != excludedState;
    }
}