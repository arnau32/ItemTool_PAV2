using UnityEngine;

public static class AutoBalanceSimulator
{
    public struct SimConfig
    {
        public float playerHealth;
        public float playerAttack;
        public float playerDefense;
        public float playerStamina;
        public float weaponFlatAttack;
        public float comboTotalDamage;
        public float comboTotalTime;
        public float comboTotalStamina;
        public int   comboStepCount;

        public float enemyHealth;
        public float enemyAttackBase;
        public float enemyWeaponMult;
        public float enemyDefense;

        public float defenseK;
        public float reductionCap;

        // Player defense redesign — armor % + bonfire % direct reduction (0–1).
        // When useDirectPlayerDef is true, playerDefense and the K formula are ignored for player.
        public bool  useDirectPlayerDef;
        public float playerArmorDefPct;
        public float playerBonfireDefPct;

        // Poise
        public float enemyMaxPoise;
        public float weaponPoiseDamagePerHit;
    }

    public struct SimResult
    {
        public float playerDamagePerHit;
        public float enemyDamagePerHit;
        public float playerDPS;
        public float ttk;
        public int   hitsToKill;
        public int   hitsToDie;
        public float playerEHP;
        public float playerDefReduction;
        public float enemyDefReduction;
        public float staminaPerCombo;
        public float comboAvgTime;

        // Stored for display breakdown when useDirectPlayerDef = true.
        public float playerArmorDefPct;
        public float playerBonfireDefPct;

        // Poise
        public int  hitsToStagger;
        public bool staggerBeforeKill;

        public bool  valid;
    }

    public static SimResult Run(SimConfig c)
    {
        var r = new SimResult { valid = true };

        float avgComboBonus = c.comboStepCount > 0 ? c.comboTotalDamage / c.comboStepCount : 0f;
        float playerRawHit  = c.playerAttack + c.weaponFlatAttack + avgComboBonus;
        float enemyRawHit   = c.enemyAttackBase * Mathf.Max(c.enemyWeaponMult, 0.01f);

        r.enemyDefReduction = DimReturns(c.enemyDefense, c.defenseK, c.reductionCap);

        if (c.useDirectPlayerDef)
        {
            r.playerArmorDefPct   = c.playerArmorDefPct;
            r.playerBonfireDefPct = c.playerBonfireDefPct;
            r.playerDefReduction  = Mathf.Clamp01(c.playerArmorDefPct + c.playerBonfireDefPct);
        }
        else
        {
            r.playerDefReduction = DimReturns(c.playerDefense, c.defenseK, c.reductionCap);
        }

        r.playerDamagePerHit = Mathf.Max(playerRawHit * (1f - r.enemyDefReduction),  1f);
        r.enemyDamagePerHit  = Mathf.Max(enemyRawHit  * (1f - r.playerDefReduction), 1f);

        r.comboAvgTime    = c.comboStepCount > 0 && c.comboTotalTime > 0f
            ? c.comboTotalTime / c.comboStepCount : 1f;
        r.staminaPerCombo = c.comboTotalStamina;

        r.playerDPS  = r.comboAvgTime > 0f ? r.playerDamagePerHit / r.comboAvgTime : 0f;
        r.ttk        = r.playerDPS > 0f ? c.enemyHealth / r.playerDPS : float.MaxValue;
        r.hitsToKill = Mathf.CeilToInt(c.enemyHealth  / Mathf.Max(r.playerDamagePerHit, 0.001f));
        r.hitsToDie  = Mathf.CeilToInt(c.playerHealth / Mathf.Max(r.enemyDamagePerHit,  0.001f));
        r.playerEHP  = r.playerDefReduction < 1f ? c.playerHealth / (1f - r.playerDefReduction) : 99999f;

        r.hitsToStagger    = c.weaponPoiseDamagePerHit > 0.001f
            ? Mathf.CeilToInt(c.enemyMaxPoise / c.weaponPoiseDamagePerHit)
            : 9999;
        r.staggerBeforeKill = r.hitsToStagger < r.hitsToKill;

        return r;
    }

    private static float DimReturns(float defense, float k, float cap)
        => Mathf.Min(defense / (defense + Mathf.Max(k, 0.001f)), cap);
}
