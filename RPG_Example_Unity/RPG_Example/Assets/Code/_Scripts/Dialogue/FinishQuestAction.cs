using UnityEngine;

[CreateAssetMenu(fileName = "Finish Quest", menuName = "Dialogue/Actions/Finish Quest", order = 1)]
public class FinishQuestAction : DialogueAction
{
    [Tooltip("Must match the QuestInfoSO asset name exactly (case-sensitive).")]
    public string questId;

    [Tooltip("If true, items required by CollectItem/DeliverItem steps are removed from inventory on finish.")]
    public bool consumeRequiredItems = true;

    public override void Execute()
    {
        if (string.IsNullOrEmpty(questId))
        {
            Debug.LogWarning("[FinishQuestAction] questId is empty — assign it in the inspector.");
            return;
        }

        if (!GameServices.TryGet<QuestService>(out var qs))
        {
            Debug.LogWarning("[FinishQuestAction] QuestService not registered.");
            return;
        }

        qs.FinishQuest(questId, consumeRequiredItems);
    }
}
