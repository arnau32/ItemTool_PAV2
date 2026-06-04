// Shared contract for any character that can trigger and gate weapon slots during combat.
// Implemented by both PlayerCombat and EnemyCombat.
public interface ICombatWeaponController
{
    void TriggerAttackStart(int weaponIndex);
    void EnableWeaponSlot(Enums.WeaponHand hand, Enums.ColliderSlot slot);
    void DisableWeaponSlot(Enums.WeaponHand hand, Enums.ColliderSlot slot);
}