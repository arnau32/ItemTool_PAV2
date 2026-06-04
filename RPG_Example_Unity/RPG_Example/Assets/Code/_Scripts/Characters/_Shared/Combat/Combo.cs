using System;
using System.Collections.Generic;

[Serializable]
public class ComboStep
{
    public Enums.AttackInputs input;
    public AttackData attack;
}

[System.Serializable]
public class Combo
{
    public List<ComboStep> steps = new List<ComboStep>();
    
    public bool MatchesPrefix(List<Enums.AttackInputs> seq)
    {
        if (seq.Count > steps.Count) return false;
        for (int i = 0; i < seq.Count; i++)
            if (steps[i].input != seq[i]) return false;
        return true;
    }

    public bool IsNextInput(Enums.AttackInputs input, int index)
        => index < steps.Count && steps[index].input == input;

    public AttackData GetAttackAt(int index)
        => (index < steps.Count) ? steps[index].attack : null;

    public bool IsComplete(List<Enums.AttackInputs> seq)
        => seq.Count == steps.Count && MatchesPrefix(seq);
}
