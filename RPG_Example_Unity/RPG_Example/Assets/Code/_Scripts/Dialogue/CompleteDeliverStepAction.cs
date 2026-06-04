using UnityEngine;

[CreateAssetMenu(fileName = "Complete Deliver Step", menuName = "Dialogue/Actions/Complete Deliver Step")]
public class CompleteDeliverStepAction : DialogueAction
{
    [Tooltip("Must match the QuestInfoSO asset name exactly (case-sensitive).")]
    public string questId;

    [Tooltip("If true, the outstanding required items are removed from the player's inventory on completion.")]
    public bool consumeItems = true;

    public override void Execute()
    {
        if (string.IsNullOrEmpty(questId))
        {
            Debug.LogWarning("[CompleteDeliverStepAction] questId is empty — assign it in the inspector.");
            return;
        }

        if (!GameServices.TryGet<QuestService>(out var qs))
        {
            Debug.LogWarning("[CompleteDeliverStepAction] QuestService not registered.");
            return;
        }

        var quest = qs.GetQuestById(questId);
        if (quest == null)
        {
            Debug.LogWarning($"[CompleteDeliverStepAction] Quest '{questId}' not found.");
            return;
        }

        var stepSO = quest.GetCurrentStepSO();
        if (stepSO == null)
        {
            Debug.LogWarning($"[CompleteDeliverStepAction] Quest '{questId}' has no active step (state: {quest.state}).");
            return;
        }

        var step = stepSO as DeliverItemStepSO;
        if (step == null)
        {
            Debug.LogWarning($"[CompleteDeliverStepAction] Quest '{questId}': current step is '{stepSO.GetType().Name}', expected DeliverItemStepSO.");
            return;
        }

        step.ForceComplete(consumeItems);
    }
}
