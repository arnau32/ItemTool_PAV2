using UnityEngine;

public static class EnemyTransitions
{
    /// Determines the correct state to return to after a hit interruption.
    /// Uses AlertLevel so the enemy re-enters the correct awareness tier
    /// instead of always falling back to CombatWander or Idle.
    public static IState DecidePostHit(EnemyContext ctx)
    {
        if (ctx?.StateFactory == null) return null;

        Enums.AlertLevel level = ctx.Perception.AlertLevel;

        if (ctx.BehaviorProfile.canEnterCombat
            && level == Enums.AlertLevel.Combat
            && ctx.Movement.DistanceToTarget() <= ctx.CombatPlanner.combatRange)
        {
            return ctx.StateFactory.CombatWander;
        }

        if (ctx.BehaviorProfile.canChase && level == Enums.AlertLevel.Combat)
        {
            return ctx.StateFactory.Chasing;
        }

        if (level == Enums.AlertLevel.Alert)
        {
            return ctx.StateFactory.Alert;
        }

        if (level == Enums.AlertLevel.Suspicious)
        {
            ctx.StateFactory.Investigate.Configure(activeSearch: false);
            return ctx.StateFactory.Investigate;
        }

        return ctx.StateFactory.Idle;
    }
}