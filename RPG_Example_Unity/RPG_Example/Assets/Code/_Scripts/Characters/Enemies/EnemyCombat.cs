using System;
using Gameplay.Items;
using UnityEngine;

public class EnemyCombat : MonoBehaviour, ICombatWeaponController
{
    [SerializeField] private WeaponInstance _equippedWeapon;

    private WeaponInstance _rightWeapon;
    private WeaponInstance _leftWeapon;

    public event Action<DamageContext> OnWeaponDealtDamage;

    private void OnEnable()
    {
        if (_equippedWeapon != null)
            _equippedWeapon.OnDealtDamage += HandleWeaponDamage;
        if (_leftWeapon != null)
            _leftWeapon.OnDealtDamage += HandleWeaponDamage;
    }

    private void OnDisable()
    {
        if (_equippedWeapon != null)
            _equippedWeapon.OnDealtDamage -= HandleWeaponDamage;
        if (_leftWeapon != null)
            _leftWeapon.OnDealtDamage -= HandleWeaponDamage;
    }

    #region Public API

    public void SetEquippedWeapon(WeaponInstance weapon)
    {
        if (_equippedWeapon == weapon) return;

        if (isActiveAndEnabled && _equippedWeapon != null)
            _equippedWeapon.OnDealtDamage -= HandleWeaponDamage;

        _equippedWeapon = weapon;

        if (isActiveAndEnabled && _equippedWeapon != null)
            _equippedWeapon.OnDealtDamage += HandleWeaponDamage;
    }

    // Called by EnemyWeaponHandler for every registered weapon so EnableWeaponSlot
    // can route to the correct WeaponInstance by hand, enabling dual-weapon damage windows.
    public void RegisterWeaponByHand(WeaponInstance weapon)
    {
        if (weapon == null) return;

        if (weapon.Hand == Enums.WeaponHand.Left)
        {
            // Unsubscribe old left weapon before replacing.
            if (_leftWeapon != null && isActiveAndEnabled)
                _leftWeapon.OnDealtDamage -= HandleWeaponDamage;

            _leftWeapon = weapon;

            // Route left-weapon hits through OnWeaponDealtDamage.
            // Right weapon is already handled by SetEquippedWeapon.
            if (isActiveAndEnabled)
                _leftWeapon.OnDealtDamage += HandleWeaponDamage;
        }
        else
        {
            _rightWeapon = weapon;
        }
    }

    #endregion

    #region ICombatWeaponController

    public void TriggerAttackStart(int weaponIndex = 0)
    {
        if (_equippedWeapon == null) return;
        _equippedWeapon.OnAttackStarted();
    }

    public void EnableWeaponSlot(Enums.WeaponHand hand, Enums.ColliderSlot slot)
    {
        var weapon = ResolveWeapon(hand);
        weapon?.EnableSlot(slot);
    }

    public void DisableWeaponSlot(Enums.WeaponHand hand, Enums.ColliderSlot slot)
    {
        var weapon = ResolveWeapon(hand);
        weapon?.DisableSlot(slot);
    }

    #endregion

    #region Helpers

    // Selects the weapon for the requested hand. Falls back to _equippedWeapon for
    // single-weapon enemies where hand-specific registration never ran.
    private WeaponInstance ResolveWeapon(Enums.WeaponHand hand)
    {
        var byHand = hand == Enums.WeaponHand.Left ? _leftWeapon : _rightWeapon;
        return byHand != null ? byHand : _equippedWeapon;
    }

    private void HandleWeaponDamage(DamageContext target)
    {
        OnWeaponDealtDamage?.Invoke(target);
    }

    #endregion
}