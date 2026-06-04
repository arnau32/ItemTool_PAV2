
public interface IWeaponUser
{
    void AttackStarted(int weaponIndex);
    void EnableSlot(Enums.WeaponHand hand, Enums.ColliderSlot slot);
    void DisableSlot(Enums.WeaponHand hand, Enums.ColliderSlot slot);
}
