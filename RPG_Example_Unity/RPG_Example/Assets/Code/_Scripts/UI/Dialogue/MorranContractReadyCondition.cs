using UnityEngine;

/// <summary>
/// Returns true when the player is carrying all items required by the active contract.
/// Use this to show the "I've got everything" / hand-in option.
/// </summary>
[CreateAssetMenu(fileName = "MorranContractReady", menuName = "Dialogue/Conditions/Morran/Contract Ready")]
public class MorranContractReadyCondition : DialogueCondition
{
    public override bool Evaluate()
    {
        return GameServices.TryGet<MorranContractService>(out var svc) && svc.CanComplete;
    }
}
