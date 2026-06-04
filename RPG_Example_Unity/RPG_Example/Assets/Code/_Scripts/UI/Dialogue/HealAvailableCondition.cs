using UnityEngine;

[CreateAssetMenu(fileName = "HealAvailableCondition", menuName = "Dialogue/Conditions/Heal Available")]
public class HealAvailableCondition : DialogueCondition
{
    public override bool Evaluate()
    {
        return HealerNpc.Current != null && HealerNpc.Current.CanHeal;
    }
}
