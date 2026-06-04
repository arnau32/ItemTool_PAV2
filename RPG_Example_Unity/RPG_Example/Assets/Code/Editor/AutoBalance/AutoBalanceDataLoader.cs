using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AutoBalanceDataLoader
{
    public static List<WeaponData> LoadWeapons()
        => LoadAll<WeaponData>("t:WeaponData");

    public static List<EnemyDefinition> LoadEnemies()
        => LoadAll<EnemyDefinition>("t:EnemyDefinition");

    public static List<CharacterBaseStatsSO> LoadBaseStats()
        => LoadAll<CharacterBaseStatsSO>("t:CharacterBaseStatsSO");

    public static List<UpgradeNodeSO> LoadUpgradeNodes()
        => LoadAll<UpgradeNodeSO>("t:UpgradeNodeSO");

    public static List<ConsumableItemData> LoadConsumables()
        => LoadAll<ConsumableItemData>("t:ConsumableItemData");

    public static List<EquipableItemData> LoadArmors()
    {
        var all = LoadAll<EquipableItemData>("t:EquipableItemData");
        all.RemoveAll(a => a is WeaponData);
        return all;
    }

    public static BalanceTargetConfigSO LoadBalanceTargets()
    {
        var guids = AssetDatabase.FindAssets("t:BalanceTargetConfigSO");
        return guids.Length > 0
            ? AssetDatabase.LoadAssetAtPath<BalanceTargetConfigSO>(AssetDatabase.GUIDToAssetPath(guids[0]))
            : null;
    }

    public static float GetStatBase(CharacterBaseStatsSO so, Enums.StatType type)
    {
        if (so == null) return 0f;
        var stats = so.Stats;
        for (int i = 0; i < stats.Count; i++)
            if (stats[i].type == type) return stats[i].baseValue;
        return 0f;
    }

    public static float GetUpgradeBonus(List<UpgradeNodeSO> nodes, int[] levels, Enums.StatType type)
    {
        if (nodes == null || levels == null) return 0f;
        Enums.UpgradeType target = StatToUpgrade(type);
        float total = 0f;
        for (int i = 0; i < nodes.Count && i < levels.Length; i++)
            if (nodes[i] != null && nodes[i].upgradeType == target)
                total += nodes[i].GetTotalValueAtLevel(levels[i]);
        return total;
    }

    public static float GetWeaponFlatAttack(WeaponData w)
    {
        if (w?.modifiers == null) return 0f;
        float sum = 0f;
        foreach (var m in w.modifiers)
            if (m.statTypeAffected == Enums.StatType.Attack && m.type == StatModifierType.Flat)
                sum += m.value;
        return sum;
    }

    public static float GetArmorStatFlat(IEnumerable<EquipableItemData> armors, Enums.StatType type)
    {
        float sum = 0f;
        if (armors == null) return 0f;
        foreach (var a in armors)
            if (a?.modifiers != null)
                foreach (var m in a.modifiers)
                    if (m.statTypeAffected == type && m.type == StatModifierType.Flat)
                        sum += m.value;
        return sum;
    }

    public static (float dmg, float time, float stam, int steps) GetLongestCombo(WeaponData w)
    {
        if (w?.combos == null || w.combos.Count == 0) return (0f, 1f, 0f, 0);

        Combo best = null;
        int bestLen = -1;
        float bestDmg = -1f;

        foreach (var combo in w.combos)
        {
            if (combo?.steps == null || combo.steps.Count == 0) continue;
            float dmg = 0f;
            foreach (var s in combo.steps) if (s?.attack != null) dmg += s.attack.damage;
            if (combo.steps.Count > bestLen || (combo.steps.Count == bestLen && dmg > bestDmg))
            {
                bestLen = combo.steps.Count;
                bestDmg = dmg;
                best    = combo;
            }
        }

        if (best == null) return (0f, 1f, 0f, 0);

        float totalDmg = 0f, totalTime = 0f, totalStam = 0f;
        int count = 0;
        foreach (var s in best.steps)
        {
            if (s?.attack == null) continue;
            totalDmg  += s.attack.damage;
            totalTime += s.attack.animationClip != null ? NormalizedTimeToSeconds(s.attack, 1f) : 0.5f;
            totalStam += s.attack.staminaCost;
            count++;
        }
        return (totalDmg, totalTime, totalStam, count);
    }

    public struct ComboStepMetrics
    {
        public string inputLabel;
        public string attackName;
        public float  animLength;
        public float  activeWindowTime;
        public float  timeConsumed;
        public float  damage;
        public float  damageMultiplier;
        public float  staminaCost;
        public float  poiseDamage;
        public float  stepDps;
    }

    public struct ComboMetrics
    {
        public ComboStepMetrics[] steps;
        public float totalDamage;
        public float totalTime;
        public float totalStamina;
        public float comboDps;
        public float staminaEfficiency;
        public string inputSequence;
    }

    public static ComboMetrics GetComboMetrics(Combo combo, float weaponFlatAttack)
    {
        if (combo?.steps == null || combo.steps.Count == 0)
            return new ComboMetrics { steps = System.Array.Empty<ComboStepMetrics>(), inputSequence = "" };

        int n = combo.steps.Count;
        var stepMetrics = new ComboStepMetrics[n];
        var sb = new System.Text.StringBuilder();
        float totalDmg = 0f, totalTime = 0f, totalStam = 0f;

        for (int i = 0; i < n; i++)
        {
            var step    = combo.steps[i];
            var atk     = step?.attack;
            bool isLast = i == n - 1;

            float animLen      = atk?.animationClip != null ? NormalizedTimeToSeconds(atk, 1f) : 0.5f;
            float timeConsumed = isLast
                ? animLen
                : (atk != null ? NormalizedTimeToSeconds(atk, atk.timeToChangeAnim) : animLen);

            float activeWindow = 0f;
            if (atk?.damageWindows != null)
                for (int d = 0; d < atk.damageWindows.Count; d++)
                    activeWindow += NormalizedTimeToSeconds(
                        atk,
                        atk.damageWindows[d].window.end,
                        atk.damageWindows[d].window.start);

            float mult  = atk?.damageMultiplier ?? 1f;
            float dmg   = weaponFlatAttack * mult;
            float stam  = atk?.staminaCost ?? 0f;
            float poise = atk != null ? atk.poiseDamage : 0f;
            float dps   = timeConsumed > 0f ? dmg / timeConsumed : 0f;
            string lbl  = InputShortLabel(step?.input ?? Enums.AttackInputs.LightInput);
            string name = string.IsNullOrEmpty(atk?.attackName) ? (atk?.name ?? "—") : atk.attackName;

            stepMetrics[i] = new ComboStepMetrics
            {
                inputLabel       = lbl,
                attackName       = name,
                animLength       = animLen,
                activeWindowTime = activeWindow,
                timeConsumed     = timeConsumed,
                damage           = dmg,
                damageMultiplier = mult,
                staminaCost      = stam,
                poiseDamage      = poise,
                stepDps          = dps,
            };

            totalDmg  += dmg;
            totalTime += timeConsumed;
            totalStam += stam;

            if (i > 0) sb.Append(" · ");
            sb.Append(lbl);
        }

        return new ComboMetrics
        {
            steps             = stepMetrics,
            totalDamage       = totalDmg,
            totalTime         = totalTime,
            totalStamina      = totalStam,
            comboDps          = totalTime > 0f ? totalDmg / totalTime : 0f,
            staminaEfficiency = totalStam > 0f ? totalDmg / totalStam : 0f,
            inputSequence     = sb.ToString(),
        };
    }

    // Integrates 1/speed(t) over [normStart, normEnd] using midpoint Riemann sum (64 steps).
    // When useAnimatorSpeedCurve is false the result degenerates to (normEnd - normStart) * clip.length.
    private static float NormalizedTimeToSeconds(AttackData atk, float normEnd, float normStart = 0f)
    {
        if (atk?.animationClip == null) return 0f;
        float clipLen = atk.animationClip.length;
        float range   = normEnd - normStart;
        if (range <= 0f) return 0f;

        if (!atk.useAnimatorSpeedCurve
            || atk.animatorSpeedCurve == null
            || atk.animatorSpeedCurve.length == 0)
            return range * clipLen;

        const int STEPS = 64;
        float dt  = range / STEPS;
        float sum = 0f;
        for (int i = 0; i < STEPS; i++)
        {
            float t     = normStart + (i + 0.5f) * dt;
            float speed = Mathf.Max(0.01f, atk.animatorSpeedCurve.Evaluate(Mathf.Clamp01(t)));
            sum += 1f / speed;
        }
        return clipLen * sum * dt;
    }

    private static string InputShortLabel(Enums.AttackInputs input) => input switch
    {
        Enums.AttackInputs.LightInput  => "L",
        Enums.AttackInputs.HeavyInput  => "H",
        Enums.AttackInputs.Skill       => "S",
        Enums.AttackInputs.Run_Attack  => "R",
        _                              => "?",
    };

    public static float GetAvgPoiseDamage(WeaponData w)
    {
        if (w?.combos == null || w.combos.Count == 0) return 0f;

        Combo best = null;
        int bestLen = -1;
        foreach (var combo in w.combos)
        {
            if (combo?.steps == null || combo.steps.Count == 0) continue;
            if (combo.steps.Count > bestLen)
            {
                bestLen = combo.steps.Count;
                best    = combo;
            }
        }
        if (best == null) return 0f;

        float sum = 0f;
        int count = 0;
        foreach (var s in best.steps)
        {
            if (s?.attack == null) continue;
            sum += s.attack.poiseDamage;
            count++;
        }
        return count > 0 ? sum / count : 0f;
    }

    public static float GetEnemyWeaponMult(EnemyDefinition def)
    {
        if (def?.prefab == null) return 1f;
        var h = def.prefab.GetComponent<EnemyWeaponHandler>();
        if (h?.baseWeapon == null || h.baseWeapon.Length == 0) return 1f;
        float sum = 0f;
        int count = 0;
        for (int i = 0; i < h.baseWeapon.Length; i++)
        {
            if (h.baseWeapon[i] == null) continue;
            sum += h.baseWeapon[i].enemyDamageMultiplier;
            count++;
        }
        return count > 0 ? sum / count : 1f;
    }

    public static float GetEnemyDefense(EnemyDefinition def)
        => GetStatBase(def?.baseStats, Enums.StatType.Defense);

    public static float GetEnemyHealth(EnemyDefinition def)
        => GetStatBase(def?.baseStats, Enums.StatType.Health);

    public static float GetEnemyAttack(EnemyDefinition def)
        => GetStatBase(def?.baseStats, Enums.StatType.Attack);

    private static Enums.UpgradeType StatToUpgrade(Enums.StatType s) => s switch
    {
        Enums.StatType.Health  => Enums.UpgradeType.Health,
        Enums.StatType.Defense => Enums.UpgradeType.Defense,
        Enums.StatType.Stamina => Enums.UpgradeType.Stamina,
        Enums.StatType.Speed   => Enums.UpgradeType.Speed,
        Enums.StatType.Attack  => Enums.UpgradeType.Attack,
        Enums.StatType.Weight  => Enums.UpgradeType.Weight,
        _                      => Enums.UpgradeType.Health,
    };

    private static List<T> LoadAll<T>(string filter) where T : Object
    {
        var list = new List<T>();
        foreach (var guid in AssetDatabase.FindAssets(filter))
        {
            var obj = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (obj != null) list.Add(obj);
        }
        return list;
    }
}
