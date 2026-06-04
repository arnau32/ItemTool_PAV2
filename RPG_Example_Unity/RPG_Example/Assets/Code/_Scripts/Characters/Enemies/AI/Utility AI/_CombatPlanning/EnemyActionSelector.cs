using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// Evaluates available actions each tick and selects the best one to execute.
/// Owns: utility scoring, temperament weighting, positioning priority logic,
/// optimal range detection, and action cooldown checks.
/// Extracted from EnemyCombatPlanner to isolate the decision-making responsibility.
public sealed class EnemyActionSelector
{
    #region Fields

    private readonly CombatPlannerConfig _config;
    private readonly List<EnemyAction> _actions;
    private readonly Dictionary<EnemyAction, float> _cooldowns;
    private readonly Dictionary<AttackAction, float> _optimalRangeCache;

    private readonly bool _enablePositioningPriority;
    private readonly float _positioningBonus;
    private readonly float _positioningTolerance;
    private readonly float _relaxedToleranceMultiplier;
    private readonly float _defensePriorityMultiplier;

    #endregion

    public EnemyActionSelector(
        CombatPlannerConfig config,
        List<EnemyAction> actions,
        Dictionary<EnemyAction, float> cooldowns,
        Dictionary<AttackAction, float> optimalRangeCache,
        bool enablePositioningPriority,
        float positioningBonus,
        float positioningTolerance,
        float relaxedToleranceMultiplier,
        float defensePriorityMultiplier)
    {
        _config = config;
        _actions = actions;
        _cooldowns = cooldowns;
        _optimalRangeCache = optimalRangeCache;
        _enablePositioningPriority = enablePositioningPriority;
        _positioningBonus = positioningBonus;
        _positioningTolerance = positioningTolerance;
        _relaxedToleranceMultiplier = relaxedToleranceMultiplier;
        _defensePriorityMultiplier = defensePriorityMultiplier;
    }

    /// Evaluates all available actions and returns the best one, or null if nothing should change.
    public EnemyAction Select(ScoreContext sc, float now, bool attacksLocked, EnemyAction currentAction, float currentActionScore,
        Enums.ActionCategory? commitCategory, float commitUntil)
    {
        AttackAction bestAttack = null;
        float bestAttackScore = float.NegativeInfinity;
        EnemyAction bestPositioning = null;
        float bestPositioningScore = float.NegativeInfinity;
        EnemyAction bestOther = null;
        float bestOtherScore = float.NegativeInfinity;
        bool hasReadyAttack = false;

        for (int i = 0; i < _actions.Count; i++)
        {
            EnemyAction action = _actions[i];
            if (action == null) continue;
            if (_cooldowns.TryGetValue(action, out float readyAt) && now < readyAt) continue;
            if (attacksLocked && action.category == Enums.ActionCategory.Attack) continue;
            if (!sc.attackTokenAvailable && action.category == Enums.ActionCategory.Attack) continue;
            if (commitCategory != null && now < commitUntil && action.category != commitCategory) continue;

            float score = action.Evaluate(sc);
            if (score < 0f) continue;

            if (action.category == Enums.ActionCategory.Attack) hasReadyAttack = true;

            score *= GetTemperamentWeightFor(action.category, sc.style);

            if (action == currentAction)
            {
                score *= _config.stickinessMultiplier;
            }
            else if (currentAction != null && action.category != Enums.ActionCategory.Attack)
            {
                score *= _config.switchPenalty;
            }

            switch (action.category)
            {
                case Enums.ActionCategory.Attack:
                {
                    if (action is not AttackAction atk) continue;

                    if (_config.hasMultipleAttakcs)
                    {
                        score *= atk.selectionWeight;
                        if (atk.randomness > 0f)
                            score += Random.Range(-atk.randomness, atk.randomness);
                        if (action == currentAction)
                            score *= (1f - atk.repeatPenalty);
                    }

                    if (score > bestAttackScore)
                    {
                        bestAttackScore = score;
                        bestAttack = atk;
                    }

                    break;
                }

                case Enums.ActionCategory.Movement when action.isMaintenanceMovement:
                {
                    if (score > bestPositioningScore)
                    {
                        bestPositioningScore = score;
                        bestPositioning = action;
                    }

                    break;
                }

                default:
                {
                    if (score > bestOtherScore)
                    {
                        bestOtherScore = score;
                        bestOther = action;
                    }

                    break;
                }
            }
        }

        sc.hasReadyAttack = hasReadyAttack;

        return ResolveChoice(sc, now, attacksLocked, bestAttack, bestAttackScore,
            bestPositioning, bestPositioningScore, bestOther, bestOtherScore,
            currentAction, currentActionScore);
    }

    #region Helpers

    private EnemyAction ResolveChoice(ScoreContext sc, float now, bool attacksLocked, AttackAction bestAttack, float bestAttackScore,
        EnemyAction bestPositioning, float bestPositioningScore, EnemyAction bestOther,
        float bestOtherScore, EnemyAction currentAction, float currentActionScore)
    {
        EnemyAction bestNonAttack = null;
        float bestNonAttackScore = float.NegativeInfinity;

        bool needsPositioning = _enablePositioningPriority && bestAttack != null && bestPositioning != null && !attacksLocked && NeedsPositioningForAttack(bestAttack, sc);

        if (bestPositioning != null)
        {
            float posScore = needsPositioning ? bestPositioningScore * _positioningBonus : bestPositioningScore;
            if (posScore > bestNonAttackScore)
            {
                bestNonAttackScore = posScore;
                bestNonAttack = bestPositioning;
            }
        }

        if (bestOther != null && bestOtherScore > bestNonAttackScore)
        {
            bestNonAttackScore = bestOtherScore;
            bestNonAttack = bestOther;
        }

        EnemyAction chosen = null;
        float chosenScore = float.NegativeInfinity;

        // When the positioning system decides it's time to attack (no positioning needed),
        // that decision must not be vetoed by the interruptThreshold below.
        bool forcedAttack = false;

        if (_enablePositioningPriority && bestAttack != null && !attacksLocked)
        {
            if (needsPositioning && bestNonAttack != null
                                 && bestNonAttack.category == Enums.ActionCategory.Movement
                                 && bestNonAttack.isMaintenanceMovement)
            {
                // Best non-attack is a positioning action — prefer it.
                chosen = bestNonAttack;
                chosenScore = bestNonAttackScore;
            }
            else if (needsPositioning && bestNonAttack != null)
            {
                // No positioning action available — apply relaxed tolerance.
                float optimalRange = GetOptimalRangeForAction(bestAttack, sc);
                float relaxedTolerance = _positioningTolerance * _relaxedToleranceMultiplier;

                if (Mathf.Abs(sc.distance - optimalRange) <= relaxedTolerance)
                {
                    chosen = bestAttack;
                    chosenScore = bestAttackScore;
                    forcedAttack = true;
                }
                else
                {
                    chosen = bestNonAttack;
                    chosenScore = bestNonAttackScore;
                }
            }
            else
            {
                // When player is attacking, Defense actions compete against the attack.
                // The boost is scaled by the enemy's defend temperament weight so aggressive
                // enemies dodge rarely while defensive enemies prioritise it.
                if (bestNonAttack != null
                    && bestNonAttack.category == Enums.ActionCategory.Defense
                    && sc.targetWindup > 0f
                    && _defensePriorityMultiplier > 0f)
                {
                    float boostedDefense = bestNonAttackScore * _defensePriorityMultiplier * sc.style.defend;
                    if (boostedDefense > bestAttackScore)
                    {
                        chosen = bestNonAttack;
                        chosenScore = boostedDefense;
                    }
                    else
                    {
                        chosen = bestAttack;
                        chosenScore = bestAttackScore;
                        forcedAttack = true;
                    }
                }
                else
                {
                    // Enemy is in range, no positioning needed, no defense priority → attack.
                    chosen = bestAttack;
                    chosenScore = bestAttackScore;
                    forcedAttack = true;
                }
            }
        }
        else
        {
            if (bestAttack != null && bestAttackScore > chosenScore)
            {
                chosen = bestAttack;
                chosenScore = bestAttackScore;
            }

            if (bestNonAttack != null && bestNonAttackScore > chosenScore)
            {
                chosen = bestNonAttack;
                chosenScore = bestNonAttackScore;
            }
        }

        if (chosen == null)
        {
            if (currentAction != null) return null;
            if (bestNonAttack == null) return null;

            chosen = bestNonAttack;
            chosenScore = bestNonAttackScore;
        }

        if (currentAction == null) return chosen;

        if (chosenScore < _config.minScoreToAct) return null;
        if (chosen == currentAction) return null;
        if (!currentAction.canBeInterrupted) return null;
        // forcedAttack bypasses the stickiness threshold: the positioning system already
        // decided the enemy is in range and should attack now.
        if (!forcedAttack && currentActionScore > 0f && chosenScore < currentActionScore * _config.interruptThreshold) return null;

        return chosen;
    }

    private bool NeedsPositioningForAttack(EnemyAction attackAction, ScoreContext sc)
    {
        if (attackAction == null) return false;

        float optimalRange = GetOptimalRangeForAction(attackAction, sc);
        if (optimalRange <= 0f) return false;

        return Mathf.Abs(sc.distance - optimalRange) > _positioningTolerance;
    }

    private float GetOptimalRangeForAction(EnemyAction action, ScoreContext sc)
    {
        if (action is not AttackAction attackAction) return 0f;

        if (attackAction.positioningOptimalRangeOverride > 0f)
        {
            return attackAction.positioningOptimalRangeOverride;
        }

        if (_optimalRangeCache.TryGetValue(attackAction, out float cached)) return cached;

        float detected = 0f;

        var considerations = attackAction.considerations;
        if (considerations != null)
        {
            for (int i = 0; i < considerations.Count; i++)
            {
                var c = considerations[i];

                if (c is DistanceBellConsideration bell)
                {
                    detected = bell.optimalRange;
                    break;
                }

                if (c is not DistanceRangeConsideration range) continue;

                detected = (range.minDistance + range.maxDistance) * 0.5f;
                break;
            }
        }

        if (detected <= 0f && attackAction is LightAttackAction { hardMaxDistance: > 0f } la)
        {
            detected = la.hardMaxDistance * 0.8f;
        }

        _optimalRangeCache[attackAction] = detected;
        return detected;
    }

    private static float GetTemperamentWeightFor(Enums.ActionCategory category, TemperamentWeights style)
    {
        return category switch
        {
            Enums.ActionCategory.Attack => style.attack,
            Enums.ActionCategory.Defense => style.defend,
            Enums.ActionCategory.Disengage => style.disengage,
            Enums.ActionCategory.Movement => style.pressure,
            Enums.ActionCategory.Flank => style.flank,
            Enums.ActionCategory.Bait => style.bait,
            Enums.ActionCategory.Special => style.special,
            _ => 1f
        };
    }

    #endregion
}