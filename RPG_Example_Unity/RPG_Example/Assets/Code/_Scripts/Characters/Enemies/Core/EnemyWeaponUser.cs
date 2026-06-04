public class EnemyWeaponUser : IWeaponUser
{
    private readonly EnemyCombat _combat;

    public EnemyWeaponUser(EnemyCombat combat)
    {
        _combat = combat;
    }
    
    public void AttackStarted(int weaponIndex) => _combat.TriggerAttackStart(weaponIndex);
    public void EnableSlot(Enums.WeaponHand hand, Enums.ColliderSlot slot) => _combat.EnableWeaponSlot(hand, slot);
    public void DisableSlot(Enums.WeaponHand hand, Enums.ColliderSlot slot) => _combat.DisableWeaponSlot(hand, slot);
}
