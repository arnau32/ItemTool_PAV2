using UnityEngine;

[CreateAssetMenu(fileName = "Complete Morran Contract", menuName = "Dialogue/Actions/Morran/Complete Contract")]
public class CompleteMorranContractAction : DialogueAction
{
    public override void Execute()
    {
        if (!GameServices.TryGet<MorranContractService>(out var svc))
        {
            Debug.LogWarning("[CompleteMorranContractAction] MorranContractService not registered.");
            return;
        }

        if (!svc.CanComplete)
        {
            Debug.LogWarning("[CompleteMorranContractAction] Player does not have the required items.");
            return;
        }

        svc.Complete();
    }
}
