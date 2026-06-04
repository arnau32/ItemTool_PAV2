using UnityEngine;

[CreateAssetMenu(fileName = "Start Quest", menuName = "Dialogue/Actions/Start Quest", order = 0)]
public class StartQuestAction : DialogueAction
{
    [Tooltip("Must match the QuestInfoSO asset name exactly (case-sensitive).")] public string questId;

    public override void Execute()
    {
        if (string.IsNullOrEmpty(questId))
        {
            Debug.LogWarning("[StartQuestAction] questId is empty — assign it in the inspector.");
            return;
        }

        if (!GameServices.TryGet<QuestService>(out var qs))
        {
            Debug.LogWarning("[StartQuestAction] QuestService not registered.");
            return;
        }

        qs.StartQuest(questId);
    }
}