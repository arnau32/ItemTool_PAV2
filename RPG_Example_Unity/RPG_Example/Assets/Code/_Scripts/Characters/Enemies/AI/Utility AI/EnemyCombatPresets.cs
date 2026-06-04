using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Example configuration presets for enemies using the V2 combat system.
/// These are reference configurations you can use as starting points.
/// </summary>
public static class EnemyCombatPresets
{
    /// <summary>
    /// Aggressive melee enemy that constantly pressures the player
    /// Example: Fast sword-wielding knight
    /// </summary>
    public static class AggressiveMelee
    {
        // Planner Settings
        public const float EvaluationRate = 0.12f;
        public const float MinScoreToAct = 30f;
        public const float StickinessMultiplier = 1.6f;
        public const float SwitchPenalty = 0.7f;
        public const float InterruptThreshold = 1.2f;

        // Positioning Priority
        public const bool EnablePositioningPriority = true;
        public const float PositioningBonus = 2.5f;
        public const float PositioningTolerance = 0.4f;

        // Pressure Action
        public const float PressureDistance = 2.5f;
        public const float PressureSpeed = 2.4f;
        public const float PressureDuration = 1.5f;
        public const bool EnableLateralMovement = true;
        public const float LateralIntensity = 0.3f;
        public const bool EnableFeints = true;
        public const float FeintChance = 0.35f;

        // CloseGap Action
        public const float CloseGapPreferred = 2.0f;
        public const float CloseGapDuration = 0.5f;
        public const float CloseGapTolerance = 0.3f;

        // BackOff Action (when overwhelmed)
        public const float BackOffTargetRange = 3.5f;
        public const float BackOffDuration = 0.4f;
        public const float BackOffSpeed = 2.0f;

        // Light Attack
        public const float LightAttackRange = 2.2f;
        public const float LightAttackCooldown = 0.8f;
    }

    /// <summary>
    /// Defensive tank enemy that maintains distance and punishes aggression
    /// Example: Heavy knight with shield
    /// </summary>
    public static class DefensiveTank
    {
        // Planner Settings
        public const float EvaluationRate = 0.15f;
        public const float MinScoreToAct = 35f;
        public const float StickinessMultiplier = 2.0f;
        public const float SwitchPenalty = 0.65f;
        public const float InterruptThreshold = 1.3f;

        // Positioning Priority
        public const bool EnablePositioningPriority = true;
        public const float PositioningBonus = 3.0f; // Higher - more careful positioning
        public const float PositioningTolerance = 0.3f;

        // Pressure Action (more cautious)
        public const float PressureDistance = 3.5f;
        public const float PressureSpeed = 1.6f;
        public const float PressureDuration = 1.0f;
        public const bool EnableLateralMovement = true;
        public const float LateralIntensity = 0.2f;
        public const bool EnableFeints = false; // Don't feint - wait for player mistake

        // CloseGap Action (less aggressive)
        public const float CloseGapPreferred = 3.0f;
        public const float CloseGapDuration = 0.7f;
        public const float CloseGapTolerance = 0.5f;

        // BackOff Action (uses more)
        public const float BackOffTargetRange = 4.0f;
        public const float BackOffDuration = 0.6f;
        public const float BackOffSpeed = 1.8f;

        // Heavy Attack (counter attack)
        public const float HeavyAttackRange = 3.0f;
        public const float HeavyAttackCooldown = 2.0f;
    }

    /// <summary>
    /// Balanced enemy with mix of pressure and caution
    /// Example: Standard soldier/bandit
    /// </summary>
    public static class Balanced
    {
        // Planner Settings
        public const float EvaluationRate = 0.15f;
        public const float MinScoreToAct = 32f;
        public const float StickinessMultiplier = 1.8f;
        public const float SwitchPenalty = 0.65f;
        public const float InterruptThreshold = 1.25f;

        // Positioning Priority
        public const bool EnablePositioningPriority = true;
        public const float PositioningBonus = 2.5f;
        public const float PositioningTolerance = 0.5f;

        // Pressure Action
        public const float PressureDistance = 2.8f;
        public const float PressureSpeed = 2.0f;
        public const float PressureDuration = 1.2f;
        public const bool EnableLateralMovement = true;
        public const float LateralIntensity = 0.35f;
        public const bool EnableFeints = true;
        public const float FeintChance = 0.25f;

        // CloseGap Action
        public const float CloseGapPreferred = 2.5f;
        public const float CloseGapDuration = 0.6f;
        public const float CloseGapTolerance = 0.4f;

        // BackOff Action
        public const float BackOffTargetRange = 3.2f;
        public const float BackOffDuration = 0.5f;
        public const float BackOffSpeed = 1.8f;

        // Light Attack
        public const float LightAttackRange = 2.5f;
        public const float LightAttackCooldown = 1.0f;
    }

    /// <summary>
    /// Boss enemy with advanced behavior
    /// </summary>
    public static class BossEnemy
    {
        // Planner Settings
        public const float EvaluationRate = 0.10f; // Faster reactions
        public const float MinScoreToAct = 25f; // More willing to act
        public const float StickinessMultiplier = 1.5f; // Switch tactics more
        public const float SwitchPenalty = 0.7f;
        public const float InterruptThreshold = 1.15f;

        // Positioning Priority
        public const bool EnablePositioningPriority = true;
        public const float PositioningBonus = 2.8f;
        public const float PositioningTolerance = 0.6f; // More forgiving

        // Pressure Action (intense)
        public const float PressureDistance = 2.2f;
        public const float PressureSpeed = 2.6f;
        public const float PressureDuration = 1.8f;
        public const bool EnableLateralMovement = true;
        public const float LateralIntensity = 0.45f;
        public const bool EnableFeints = true;
        public const float FeintChance = 0.45f; // Frequent feints

        // Multiple attack ranges
        public const float LightAttackRange = 2.5f;
        public const float HeavyAttackRange = 3.5f;
        public const float SpecialAttackRange = 4.0f;
    }
}
