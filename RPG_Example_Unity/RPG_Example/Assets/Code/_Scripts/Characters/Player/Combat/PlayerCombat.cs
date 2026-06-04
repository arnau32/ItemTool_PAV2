using Gameplay.Items;
using UnityEngine;

public class PlayerCombat : MonoBehaviour, ICombatWeaponController
{
    private WeaponHandler _weaponHandler;

    private void Awake()
    {
        _weaponHandler = GetComponent<WeaponHandler>();
    }

    #region ICombatWeaponController

    public void TriggerAttackStart(int weaponIndex)
    {
        if (HasWeapon(weaponIndex))
        {
            GetWeaponInstance(weaponIndex).OnAttackStarted();
        }
    }

    public void EnableWeaponSlot(Enums.WeaponHand hand, Enums.ColliderSlot slot)
    {
        int idx = (int)hand;
        if (!HasWeapon(idx)) return;

        GetWeaponInstance(idx).EnableSlot(slot);
    }

    public void DisableWeaponSlot(Enums.WeaponHand hand, Enums.ColliderSlot slot)
    {
        int idx = (int)hand;
        if (!HasWeapon(idx)) return;

        GetWeaponInstance(idx).DisableSlot(slot);
    }

    #endregion

    #region Helpers

    private WeaponInstance GetWeaponInstance(int i) => _weaponHandler.ActiveWeapons[i];
    private bool HasWeapon(int index) => _weaponHandler.ActiveWeapons.Count > index;

    #endregion
}