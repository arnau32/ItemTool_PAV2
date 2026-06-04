using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "Give Item", menuName = "Dialogue/Actions/Give Item")]
public class GiveItemDialogueAction : DialogueAction
{
    public QuestReward reward;

    public override void Execute()
    {
        if (!GameServices.TryGet<QuestService>(out var svc))
        {
            Debug.LogWarning("[GiveItemDialogueAction] QuestService not found.");
            return;
        }

        svc.GrantRewardAsync(reward).Forget();
    }
}
