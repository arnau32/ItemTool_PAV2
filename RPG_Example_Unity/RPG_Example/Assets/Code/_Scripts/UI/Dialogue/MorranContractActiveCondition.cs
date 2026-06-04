using UnityEngine;

/// <summary>
/// Returns true when Morran has an active contract assigned to the player.
/// Use this to show the "what do you need?" / "I'm working on it" branch.
/// </summary>
[CreateAssetMenu(fileName = "MorranContractActive", menuName = "Dialogue/Conditions/Morran/Contract Active")]
public class MorranContractActiveCondition : DialogueCondition
{
    public override bool Evaluate()
    {
        return GameServices.TryGet<MorranContractService>(out var svc) && svc.HasActive;
    }
}
