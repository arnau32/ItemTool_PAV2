using System;
using UnityEngine;
using UnityEngine.Localization;


[Serializable]
public struct ManualLevelEntry
{
    public float value;
    public int cost;
}

[CreateAssetMenu(fileName = "UpgradeNode", menuName = "SkillTree/Upgrade Node")]
public class UpgradeNodeSO : ScriptableObject
{
    #region Identity

    [Header("Identity")] 
    public LocalizedString displayName;
    public Sprite icon;

    #endregion

    #region Stat

    [Header("Stat")] 
    public Enums.UpgradeType upgradeType;
    public int maxLevel = 5;

    #endregion

    #region Progression

    [Header("Progression")] 
    
    public Enums.ProgressionType progressionType = Enums.ProgressionType.Linear;

    // ── Manual ───────────────────────────────────────────────────────────────
    public ManualLevelEntry[] manualLevels = new ManualLevelEntry[5];

    // ── Linear ───────────────────────────────────────────────────────────────
    // value(n) = linearValueBase + linearValueIncrement * (n-1)   (increment)
    // cost(n)  = linearCostBase  + linearCostStep       * (n-1)   (n = levels purchased)
    public float linearValueBase = 10f;
    public float linearValueIncrement = 5f;
    public int linearCostBase = 100;
    public int linearCostStep = 50;

    // ── Multiplicative ───────────────────────────────────────────────────────
    // value(n) = multiValueBase * multiValueMultiplier^(n-1)       (increment)
    // cost(n)  = multiCostBase  * multiCostMultiplier^n             (n = levels purchased)
    public float multiValueBase = 10f;
    public float multiValueMultiplier = 1.2f;
    public int multiCostBase = 100;
    public float multiCostMultiplier = 1.5f;

    // ── Power ────────────────────────────────────────────────────────────────
    // value(n) = powerA * n^powerB + powerC                         (total)
    // cost(n)  = powerCostBase * powerCostMultiplier^n
    public float powerA = 5f;
    public float powerB = 1.5f;
    public float powerC = 0f;
    public int powerCostBase = 100;
    public float powerCostMultiplier = 1.5f;

    // ── Exponential ──────────────────────────────────────────────────────────
    // value(n) = expValueBase * expValueGrowth^(n-1)                (increment)
    // cost(n)  = expCostBase  * expCostGrowth^n
    public float expValueBase = 10f;
    public float expValueGrowth = 1.5f;
    public int expCostBase = 100;
    public float expCostGrowth = 2f;

    // ── Logarithmic ──────────────────────────────────────────────────────────
    // value(n) = logA * log(n + logK) + logC                        (total)
    // cost(n)  = logCostBase * logCostMultiplier^n
    public float logA = 30f;
    public float logK = 1f;
    public float logC = 0f;
    public int logCostBase = 100;
    public float logCostMultiplier = 1.5f;

    // ── Diminishing Returns ──────────────────────────────────────────────────
    // Exponential DR:  value(n) = drMaxValue * (1 - e^(-drK * n))  (total)
    // Hyperbolic DR:   value(n) = (n / (n + drK)) * drMaxValue     (total)
    // cost(n)  = drCostBase * drCostMultiplier^n
    public float drMaxValue = 50f;
    public float drK = 0.5f;
    public bool drUseHyperbolic = false;
    public int drCostBase = 100;
    public float drCostMultiplier = 1.5f;

    #endregion

    #region Public API

    /// Returns the cumulative stat bonus at the given level (0 = no upgrades).
    public float GetTotalValueAtLevel(int level)
    {
        if (level <= 0) return 0f;
        level = Mathf.Clamp(level, 0, maxLevel);

        switch (progressionType)
        {
            case Enums.ProgressionType.Manual:
            {
                float total = 0f;
                for (int i = 0; i < level && i < manualLevels.Length; i++)
                    total += manualLevels[i].value;
                return total;
            }

            case Enums.ProgressionType.Linear:
                return level * linearValueBase + linearValueIncrement * level * (level - 1) / 2f;

            case Enums.ProgressionType.Multiplicative:
                if (Mathf.Approximately(multiValueMultiplier, 1f))
                    return multiValueBase * level;
                return multiValueBase * (Mathf.Pow(multiValueMultiplier, level) - 1f)
                       / (multiValueMultiplier - 1f);

            case Enums.ProgressionType.Power:
                return powerA * Mathf.Pow(level, powerB) + powerC;

            case Enums.ProgressionType.Exponential:
                if (Mathf.Approximately(expValueGrowth, 1f))
                    return expValueBase * level;
                return expValueBase * (Mathf.Pow(expValueGrowth, level) - 1f)
                       / (expValueGrowth - 1f);

            case Enums.ProgressionType.Logarithmic:
                return logA * Mathf.Log(Mathf.Max(level + logK, 0.0001f)) + logC;

            case Enums.ProgressionType.DiminishingReturns:
                if (drUseHyperbolic)
                    return level / (level + drK) * drMaxValue;
                return drMaxValue * (1f - Mathf.Exp(-drK * level));

            default:
                return 0f;
        }
    }

    /// Returns the cost in AuraDust to purchase the next level.
    /// currentLevel = how many levels the player has already bought (0 = none).
    public int GetCostForLevel(int currentLevel)
    {
        if (currentLevel >= maxLevel) return 0;

        switch (progressionType)
        {
            case Enums.ProgressionType.Manual:
                return currentLevel < manualLevels.Length ? manualLevels[currentLevel].cost : 0;

            case Enums.ProgressionType.Linear:
                return linearCostBase + linearCostStep * currentLevel;

            case Enums.ProgressionType.Multiplicative:
                return Mathf.RoundToInt(multiCostBase * Mathf.Pow(multiCostMultiplier, currentLevel));

            case Enums.ProgressionType.Power:
                return Mathf.RoundToInt(powerCostBase * Mathf.Pow(powerCostMultiplier, currentLevel));

            case Enums.ProgressionType.Exponential:
                return Mathf.RoundToInt(expCostBase * Mathf.Pow(expCostGrowth, currentLevel));

            case Enums.ProgressionType.Logarithmic:
                return Mathf.RoundToInt(logCostBase * Mathf.Pow(logCostMultiplier, currentLevel));

            case Enums.ProgressionType.DiminishingReturns:
                return Mathf.RoundToInt(drCostBase * Mathf.Pow(drCostMultiplier, currentLevel));

            default:
                return 0;
        }
    }

    #endregion
}