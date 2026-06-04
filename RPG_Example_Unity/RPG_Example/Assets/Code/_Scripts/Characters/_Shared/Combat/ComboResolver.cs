using System.Collections.Generic;
using UnityEngine;

public class ComboResolver
{
    public WeaponData currentWeapon;

    private Combo activeCombo;
    private int stepIndex = 0;

    public void ResetCombo()
    {
        activeCombo = null;
        stepIndex = 0;
    }

    public AttackData ResolveNextAttack(Enums.AttackInputs input)
    {
        if (currentWeapon == null || currentWeapon.combos == null || currentWeapon.combos.Count == 0)
        {
            Debug.LogWarning("[ComboResolver] No hay combos configurados.");
            return null;
        }

        if (activeCombo == null)
        {
            for (int ci = 0; ci < currentWeapon.combos.Count; ci++)
            {
                var combo = currentWeapon.combos[ci];
                if (combo?.steps == null || combo.steps.Count == 0) continue;
                if (combo.steps[0].input != input) continue;

                activeCombo = combo;
                stepIndex   = 0;
                var first   = activeCombo.GetAttackAt(stepIndex);
                return first;
            }

            return null;
        }

        var nextStep   = stepIndex + 1;
        int totalSteps = activeCombo.steps.Count;
        int activeIdx  = currentWeapon.combos.IndexOf(activeCombo);

        if (nextStep < totalSteps && activeCombo.steps[nextStep].input == input)
        {
            stepIndex = nextStep;
            var next = activeCombo.GetAttackAt(stepIndex);
            return next;
        }

        int desiredLen = stepIndex + 2;
        for (int ci = 0; ci < currentWeapon.combos.Count; ci++)
        {
            var combo = currentWeapon.combos[ci];
            if (combo == null || combo.steps == null || combo.steps.Count == 0) continue;
            if (combo.steps.Count < desiredLen) continue;
            if (!MatchesPrefixPlusInput(combo, activeCombo, stepIndex, input)) continue;

            activeCombo = combo;
            stepIndex   = desiredLen - 1;
            var branched = activeCombo.GetAttackAt(stepIndex);
            return branched;
        }

        ResetCombo();
        return null;
    }
    
    private static bool MatchesPrefixPlusInput(Combo candidate, Combo current, int currentStepIndex, Enums.AttackInputs newInput)
    {
        // Compare prefix [0..currentStepIndex] against the current active combo
        for (var i = 0; i <= currentStepIndex; i++)
        {
            if (candidate.steps[i].input != current.steps[i].input) return false;
        }

        var last = currentStepIndex + 1;
        return candidate.steps[last].input == newInput;
    }
}
