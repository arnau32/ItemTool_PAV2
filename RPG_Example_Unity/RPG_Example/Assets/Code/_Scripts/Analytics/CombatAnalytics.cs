using UnityEngine;

// String constants for mechanic names — used by both Unity and the GAS script column values.
public static class MechanicNames
{
    public const string Dodge       = "dodge";
    public const string Parry       = "parry";
    public const string LightAttack = "light_attack";
    public const string HeavyAttack = "heavy_attack";
    public const string WeaponSkill = "weapon_skill";
    public const string LockOn      = "lock_on";
    public const string Heal        = "heal";
}

public static class CombatAnalytics
{
    private static CombatAnalyticsService AnalyticsService => GameServices.Get<CombatAnalyticsService>();

    public static bool IsSessionActive => AnalyticsService != null && AnalyticsService.HasActiveSession;

    public static void EnsureSession()
    {
        AnalyticsService.TryStartSessionIfNone();
    }

    public static void EndSession(string reason)
    {
        AnalyticsService.EndSession(reason);
    }

    #region Player

    public static void PlayerAttackStarted(AttackData attack, Enums.AttackInputs? inputType = null)
    {
        if (attack == null) return;
        EnsureSession();
        AnalyticsService.PlayerAttackStarted(attack, inputType);
    }

    public static void PlayerAttackResolved(AttackData attack, bool hit)
    {
        if (attack == null) return;
        EnsureSession();
        AnalyticsService.PlayerAttackResolved(attack, hit);
    }

    public static void PlayerDealtDamage(float damage, Component target)
    {
        if (damage <= 0f) return;
        EnsureSession();
        AnalyticsService.PlayerDealtDamage(damage, target);
    }

    public static void PlayerTookDamage(float damage, Enums.HitType hitType, bool wasInDodgeFrames)
    {
        if (damage <= 0f) return;
        EnsureSession();
        AnalyticsService.PlayerTookDamage(damage, hitType, wasInDodgeFrames);
    }

    public static void PlayerDodged(bool lockedOn, Vector2 cardinal, float staminaCost)
    {
        EnsureSession();
        AnalyticsService.PlayerDodged(lockedOn, cardinal, staminaCost);
    }

    public static void PlayerStaminaChanged(float current, float max)
    {
        EnsureSession();
        AnalyticsService.PlayerStaminaChanged(current, max);
    }

    public static void PlayerHealed01(float delta01)
    {
        if (delta01 <= 0f) return;
        EnsureSession();
        AnalyticsService.PlayerHealed(delta01);
    }

    public static void PlayerLockOnToggled(bool isLockedOn)
    {
        EnsureSession();
        AnalyticsService.PlayerLockOnToggled(isLockedOn);
    }

    public static void WeaponEquipped(string weaponId, string weaponType)
    {
        if (string.IsNullOrEmpty(weaponId)) return;
        EnsureSession();
        AnalyticsService.WeaponEquipped(weaponId, weaponType);
    }

    #endregion

    #region Enemies

    public static void EnemySpawned(EnemyBase enemy)
    {
        if (enemy == null) return;
        EnsureSession();
        AnalyticsService.EnemySpawned(enemy);
    }

    public static void EnemyDied(EnemyBase enemy)
    {
        if (enemy == null) return;
        EnsureSession();
        AnalyticsService.EnemyKilled(enemy);
    }

    public static void EnemyDealtDamage(EnemyBase enemy, float damage, Enums.HitType hitType)
    {
        if (enemy == null || damage <= 0f) return;
        EnsureSession();
        AnalyticsService.EnemyDealtDamageToPlayer(enemy, damage, hitType);
    }

    public static void EnemyTookDamage(EnemyBase enemy, float damage, Enums.HitType hitType)
    {
        if (enemy == null || damage <= 0f) return;
        EnsureSession();
        AnalyticsService.EnemyTookDamageFromPlayer(enemy, damage, hitType);
    }

    #endregion

    #region Utility AI

    public static void AIActionChosen(EnemyBase enemy, string actionName, Enums.ActionCategory category, float score, bool switched)
    {
        if (enemy == null || string.IsNullOrEmpty(actionName)) return;
        EnsureSession();
        AnalyticsService.AIActionChosen(enemy, actionName, category, score, switched);
    }

    #endregion

    #region Encounters

    /// <summary>Call when the player enters combat with a specific enemy instance.</summary>
    public static void EncounterStarted(EnemyBase enemy, Vector3 position)
    {
        if (enemy == null) return;
        EnsureSession();
        AnalyticsService.EncounterStarted(enemy, position);
    }

    /// <summary>Call when the encounter resolves — enemy died or player died.</summary>
    public static void EncounterEnded(EnemyBase enemy, bool playerDied, Vector3 playerPosition = default)
    {
        if (enemy == null) return;
        EnsureSession();
        AnalyticsService.EncounterEnded(enemy, playerDied, playerPosition);
    }

    #endregion

    #region Mechanics

    /// <summary>Call on every parry attempt. success = parry landed and negated the hit.</summary>
    public static void ParryAttempted(bool success, float staminaCost = 0f)
    {
        EnsureSession();
        AnalyticsService.ParryAttempted(success, staminaCost);
    }

    #endregion

    #region State Machine

    /// <summary>Call from the player combat state machine whenever a new state is entered.</summary>
    public static void PlayerStateEntered(string stateName)
    {
        if (string.IsNullOrEmpty(stateName)) return;
        EnsureSession();
        AnalyticsService.PlayerStateEntered(stateName);
    }

    /// <summary>Call when the state machine rejects a player-requested transition.</summary>
    public static void InvalidTransitionAttempted(string currentState, string attemptedState)
    {
        if (string.IsNullOrEmpty(currentState)) return;
        EnsureSession();
        AnalyticsService.InvalidTransitionAttempted(currentState, attemptedState);
    }

    #endregion

    #region Extraction

    /// <summary>
    /// Call once per run when the run concludes (extraction or death).
    /// Must be called before EndSession so the data is included in the upload.
    /// </summary>
    public static void ExtractionRunEnded(
        bool    extracted,
        float   totalLootValue,
        float   lootValueExtracted,
        string  deathZone,
        string  killerEnemyType,
        Vector3 deathPosition)
    {
        EnsureSession();
        AnalyticsService.ExtractionRunEnded(
            extracted, totalLootValue, lootValueExtracted,
            deathZone, killerEnemyType, deathPosition);
    }

    #endregion

    #region Scene

    /// <summary>
    /// Call for in-game zone transitions that do NOT load a new Unity scene.
    /// Real Unity scene loads are tracked automatically via SceneManager.sceneLoaded.
    /// </summary>
    public static void SceneEntered(string sceneName)
    {
        if (!IsSessionActive || string.IsNullOrEmpty(sceneName)) return;
        AnalyticsService.SceneEntered(sceneName);
    }

    #endregion

    #region Loot

    public static void LootSpawned(string itemId, string itemType, float value, int quantity, Vector3 pos, string itemRarity = "")
    {
        if (string.IsNullOrEmpty(itemId)) return;
        AnalyticsService.LootSpawned(itemId, itemType, value, quantity, pos, itemRarity);
    }

    public static void LootPickedUp(string itemId, string itemType, float value, int quantity, Vector3 pos, string itemRarity = "")
    {
        if (string.IsNullOrEmpty(itemId)) return;
        EnsureSession();
        AnalyticsService.LootPickedUp(itemId, itemType, value, quantity, pos, itemRarity);
    }

    public static void LootIgnored(string itemId, string itemType, float value, int quantity, Vector3 pos, string itemRarity = "")
    {
        if (!IsSessionActive || string.IsNullOrEmpty(itemId)) return;
        AnalyticsService.LootIgnored(itemId, itemType, value, quantity, pos, itemRarity);
    }

    #endregion
}
