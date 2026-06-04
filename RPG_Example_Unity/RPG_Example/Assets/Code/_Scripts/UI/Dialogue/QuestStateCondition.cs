using UnityEngine;

/// <summary>
/// Condition: shows a DialogueOption only when the specified quest is in the
/// expected state. When the quest is CanFinish and checkInventory is true,
/// also verifies the player currently holds all required items.
/// </summary>
[CreateAssetMenu(menuName = "Dialogue/Conditions/Quest State")]
public class QuestStateCondition : DialogueCondition
{
    [Tooltip("Must match the QuestInfoSO asset name exactly.")]
    public string questId;

    [Tooltip("The state the quest must be in for this option to appear.")]
    public Enums.QuestState requiredState;

    [Tooltip("When true and requiredState is CanFinish, also checks that all CollectItem " +
             "steps have their items in inventory. Use this on turn-in dialogue options.")]
    public bool checkInventory = false;

    public override bool Evaluate()
    {
        if (string.IsNullOrEmpty(questId))
        {
            Debug.LogWarning("[QuestStateCondition] questId is empty.");
            return false;
        }

        if (!GameServices.TryGet<QuestService>(out var qs))
            return false;

        var quest = qs.GetQuestById(questId);
        if (quest == null || quest.state != requiredState) return false;

        if (checkInventory && requiredState == Enums.QuestState.CanFinish)
            return qs.HasRequiredItemsForFinish(questId);

        return true;
    }
}