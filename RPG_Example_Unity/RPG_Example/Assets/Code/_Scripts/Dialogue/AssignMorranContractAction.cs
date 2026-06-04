using UnityEngine;

[CreateAssetMenu(fileName = "Assign Morran Contract", menuName = "Dialogue/Actions/Morran/Assign Contract")]
public class AssignMorranContractAction : DialogueAction
{
    public override void Execute()
    {
        if (!GameServices.TryGet<MorranContractService>(out var svc))
        {
            Debug.LogWarning("[AssignMorranContractAction] MorranContractService not registered.");
            return;
        }

        svc.AssignRandom();
    }
}
