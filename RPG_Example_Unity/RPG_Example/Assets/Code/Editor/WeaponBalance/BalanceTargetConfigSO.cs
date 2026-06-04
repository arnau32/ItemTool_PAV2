using System;
using UnityEngine;

[CreateAssetMenu(menuName = "BetweenShadows/Balance/Balance Targets")]
public class BalanceTargetConfigSO : ScriptableObject
{
    #region Matchup Classification

    public enum MatchupLabel
    {
        Primary, // Weapon tier matches enemy zone — strict evaluation
        Easy,    // Weapon tier above enemy zone — expected comfortable
        Hard,    // Weapon tier below enemy zone — expected to struggle
        Extreme, // Very mismatched — expected out of strict range
        Trivial, // Weapon completely dominates — no balance concern
    }

    [Serializable]
    public struct TierZoneRule
    {
        [Tooltip("Player weapon tier (1–4).")]
        public int weaponTier;
        [Tooltip("Enemy zone (1–3).")]
        public int enemyZone;
        [Tooltip("Expected matchup difficulty label.")]
        public MatchupLabel label;
    }

    [Serializable]
    public struct MatchupTolerance
    {
        public MatchupLabel label;
        [Range(0.1f, 5f)]
        [Tooltip(">1 expands the acceptable range (lenient). <1 shrinks it (strict).")]
        public float tolerance;
    }

    [Header("Tier × Zone Compatibility")]
    [Tooltip("Maps each (weaponTier, enemyZone) pair to an expected difficulty label.")]
    public TierZoneRule[] tierZoneRules = new[]
    {
        new TierZoneRule { weaponTier = 1, enemyZone = 1, label = MatchupLabel.Primary },
        new TierZoneRule { weaponTier = 1, enemyZone = 2, label = MatchupLabel.Hard    },
        new TierZoneRule { weaponTier = 1, enemyZone = 3, label = MatchupLabel.Extreme },
        new TierZoneRule { weaponTier = 2, enemyZone = 1, label = MatchupLabel.Easy    },
        new TierZoneRule { weaponTier = 2, enemyZone = 2, label = MatchupLabel.Primary },
        new TierZoneRule { weaponTier = 2, enemyZone = 3, label = MatchupLabel.Hard    },
        new TierZoneRule { weaponTier = 3, enemyZone = 1, label = MatchupLabel.Trivial },
        new TierZoneRule { weaponTier = 3, enemyZone = 2, label = MatchupLabel.Easy    },
        new TierZoneRule { weaponTier = 3, enemyZone = 3, label = MatchupLabel.Primary },
        new TierZoneRule { weaponTier = 4, enemyZone = 1, label = MatchupLabel.Trivial },
        new TierZoneRule { weaponTier = 4, enemyZone = 2, label = MatchupLabel.Trivial },
        new TierZoneRule { weaponTier = 4, enemyZone = 3, label = MatchupLabel.Primary },
    };

    [Tooltip("Tolerance multiplier applied to target ranges per label. " +
             ">1 = lenient (range expands), <1 = strict (range shrinks).")]
    public MatchupTolerance[] matchupTolerances = new[]
    {
        new MatchupTolerance { label = MatchupLabel.Primary, tolerance = 1.0f },
        new MatchupTolerance { label = MatchupLabel.Easy,    tolerance = 0.8f },
        new MatchupTolerance { label = MatchupLabel.Hard,    tolerance = 1.5f },
        new MatchupTolerance { label = MatchupLabel.Extreme, tolerance = 2.5f },
        new MatchupTolerance { label = MatchupLabel.Trivial, tolerance = 5.0f },
    };

    #endregion

    #region Zone Targets

    [Serializable]
    public struct ZoneTargets
    {
        public int zone;

        [Tooltip("Enemy category this target applies to.")]
        public Enums.EnemyCategory category;

        [Tooltip("Target time-to-kill range in seconds (min, max).")]
        public Vector2 ttk;

        [Tooltip("Target hits the player needs to kill the enemy (min, max).")]
        public Vector2Int hitsToKill;

        [Tooltip("Target hits the enemy needs to kill the player (min, max).")]
        public Vector2Int hitsToDie;

        [Tooltip("Expected player DPS range for this zone + category.")]
        public Vector2 dpsRange;
    }

    [Header("Per-Zone & Category Targets")]
    [Tooltip("One entry per (Zone, Category) combination.")]
    public ZoneTargets[] zoneTargets = new[]
    {
        new ZoneTargets { zone = 1, category = Enums.EnemyCategory.Normal, ttk = new Vector2(4f, 9f),   hitsToKill = new Vector2Int(3, 6),  hitsToDie = new Vector2Int(4, 8),  dpsRange = new Vector2(20f, 40f) },
        new ZoneTargets { zone = 1, category = Enums.EnemyCategory.Fast,   ttk = new Vector2(3f, 7f),   hitsToKill = new Vector2Int(2, 5),  hitsToDie = new Vector2Int(3, 6),  dpsRange = new Vector2(20f, 40f) },
        new ZoneTargets { zone = 1, category = Enums.EnemyCategory.Tank,   ttk = new Vector2(8f, 18f),  hitsToKill = new Vector2Int(6, 12), hitsToDie = new Vector2Int(5, 10), dpsRange = new Vector2(15f, 35f) },
        new ZoneTargets { zone = 1, category = Enums.EnemyCategory.Elite,  ttk = new Vector2(10f, 22f), hitsToKill = new Vector2Int(8, 15), hitsToDie = new Vector2Int(4, 8),  dpsRange = new Vector2(20f, 40f) },
        new ZoneTargets { zone = 1, category = Enums.EnemyCategory.Boss,   ttk = new Vector2(25f, 60f), hitsToKill = new Vector2Int(15, 40),hitsToDie = new Vector2Int(5, 12), dpsRange = new Vector2(20f, 40f) },
    };

    #endregion

    #region Rarity

    [Serializable]
    public struct RarityRange
    {
        public Enums.ItemRarity rarity;
        public float MinMultiplier;
        public float MaxMultiplier;
        public bool PlayerOnly;
    }

    [Header("Rarity Multiplier Ranges")]
    [Tooltip("Min/max multiplier per rarity — must match RarityUtility.GetMultiplierRange at runtime.")]
    public RarityRange[] rarityRanges = new[]
    {
        new RarityRange { rarity = Enums.ItemRarity.Common,    MinMultiplier = 1.0f, MaxMultiplier = 1.4f },
        new RarityRange { rarity = Enums.ItemRarity.Rare, MinMultiplier = 1.4f, MaxMultiplier = 1.6f },
        new RarityRange { rarity = Enums.ItemRarity.Epic,      MinMultiplier = 1.6f, MaxMultiplier = 1.8f },
        new RarityRange { rarity = Enums.ItemRarity.Legendary, MinMultiplier = 1.8f, MaxMultiplier = 2.0f },
    };

    [Header("Rarity Multiplier Targets")]
    [Tooltip("Expected stat multiplier of Rare vs Common.")]
    public float uncommonRatio  = 1.20f;
    [Tooltip("Expected stat multiplier of Epic vs Common.")]
    public float epicRatio      = 1.45f;
    [Tooltip("Expected stat multiplier of Legendary vs Common.")]
    public float legendaryRatio = 1.75f;

    [Header("Consumable Thresholds")]
    [Tooltip("Maximum allowed TTK reduction from a single consumable (0.25 = 25%).")]
    [Range(0f, 1f)] public float maxConsumableTTKImpact = 0.25f;
    [Tooltip("Maximum heal-over-time per second relative to incoming DPS.")]
    [Range(0f, 2f)] public float maxHoTToDPSRatio       = 0.80f;

    #endregion

    #region Public API

    public bool TryGetRarityRange(Enums.ItemRarity rarity, out float min, out float max)
    {
        min = 1f; max = 1f;
        if (rarityRanges == null) return false;
        for (int i = 0; i < rarityRanges.Length; i++)
        {
            if (rarityRanges[i].rarity == rarity)
            {
                min = rarityRanges[i].MinMultiplier;
                max = rarityRanges[i].MaxMultiplier;
                return true;
            }
        }
        return false;
    }

    // Exact match on zone + category, then zone-only fallback.
    public bool TryGetZoneTargets(int zone, Enums.EnemyCategory category, out ZoneTargets targets)
    {
        for (int i = 0; i < zoneTargets.Length; i++)
            if (zoneTargets[i].zone == zone && zoneTargets[i].category == category)
            { targets = zoneTargets[i]; return true; }

        for (int i = 0; i < zoneTargets.Length; i++)
            if (zoneTargets[i].zone == zone)
            { targets = zoneTargets[i]; return true; }

        targets = default;
        return false;
    }

    /// Returns the matchup label for a (weaponTier, enemyZone) pair.
    public MatchupLabel GetMatchupLabel(int weaponTier, int enemyZone)
    {
        if (tierZoneRules != null)
            for (int i = 0; i < tierZoneRules.Length; i++)
                if (tierZoneRules[i].weaponTier == weaponTier && tierZoneRules[i].enemyZone == enemyZone)
                    return tierZoneRules[i].label;

        // Fallback heuristic
        if (weaponTier < enemyZone) return MatchupLabel.Hard;
        if (weaponTier > enemyZone) return MatchupLabel.Easy;
        return MatchupLabel.Primary;
    }

    /// Returns the tolerance multiplier for a given matchup label.
    public float GetTolerance(MatchupLabel label)
    {
        if (matchupTolerances != null)
            for (int i = 0; i < matchupTolerances.Length; i++)
                if (matchupTolerances[i].label == label)
                    return Mathf.Max(0.1f, matchupTolerances[i].tolerance);
        return 1f;
    }

    /// Returns zone targets adjusted by the tolerance for the given weapon tier vs enemy zone.
    public bool TryGetMatchupTargets(int weaponTier, int enemyZone, Enums.EnemyCategory category,
        out ZoneTargets adjusted, out MatchupLabel label)
    {
        label = GetMatchupLabel(weaponTier, enemyZone);
        if (!TryGetZoneTargets(enemyZone, category, out var raw))
        {
            adjusted = default;
            return false;
        }

        float tol = GetTolerance(label);
        adjusted = new ZoneTargets
        {
            zone       = raw.zone,
            category   = raw.category,
            ttk        = new Vector2(raw.ttk.x / tol, raw.ttk.y * tol),
            hitsToKill = new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(raw.hitsToKill.x / tol)),
                Mathf.RoundToInt(raw.hitsToKill.y * tol)),
            hitsToDie  = new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(raw.hitsToDie.x / tol)),
                Mathf.RoundToInt(raw.hitsToDie.y * tol)),
            dpsRange   = new Vector2(raw.dpsRange.x / tol, raw.dpsRange.y * tol),
        };
        return true;
    }

    #endregion
}
