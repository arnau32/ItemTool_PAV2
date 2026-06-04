using UnityEngine;

[CreateAssetMenu(fileName = "HealAction", menuName = "Dialogue/Actions/Heal")]
public class HealNPCAction : DialogueAction
{
    public override void Execute()
    {
        if (HealerNpc.Current == null)
        {
            Debug.LogWarning("[HealAction] No HealerNpc.Current in scene.");
            return;
        }

        HealerNpc.Current.ExecuteHeal();
    }
}
