using System;
using System.Collections.Generic;
using Gameplay.Items;
using UnityEngine;

public class WeaponHandler : MonoBehaviour, IWeaponRegistry, IWeaponVfxUser
{
    #region Fields

    [SerializeField] private GameObject _slashSpawnerPoint;

    public WeaponData baseWeapon;
    public List<WeaponInstance> ActiveWeapons { get; private set; } = new();

    private PlayerContext _ctx;
    private EquipmentHandler _equipmentHandler;

    public GameObject SlashSpawnerPoint => _slashSpawnerPoint;

    public VFXRigContainer ActiveVfxRig { get; private set; }

    private WeaponInstance _rightWeapon;
    private WeaponInstance _leftWeapon;

    #endregion

    #region Events

    public event Action<DamageContext> OnWeaponDealtDamage;
    public event Action<AttackColliderHandler> OnParryContact;
    public event Action<EnemyProjectile> OnProjectileParryContact;
    public event Action OnWeaponsCleared;

    #endregion

    #region Initialization

    public void Initialize(PlayerContext ctx)
    {
        _ctx = ctx;
        _equipmentHandler = _ctx.EquipmentHandler;

        var saveHandler = GetComponent<PlayerSaveHandler>();
        if (saveHandler != null && saveHandler.HasPendingEquipment())
        {
            var restoredWeapon = saveHandler.ApplyEquipmentDeferred(_equipmentHandler);
            if (restoredWeapon != null) return;
        }

        _equipmentHandler.Equip(baseWeapon);
    }

    #endregion

    #region Weapon Registration

    public void NotifyWeaponDataEquipped(WeaponData weaponData)
    {
        if (_ctx == null || weaponData == null) return;

        _ctx.ComboController.SetWeapon(weaponData);
        ApplyDodgeSetFromWeapon(weaponData);
        UpdateAnimatorForWeapon(weaponData);
    }

    public void RegisterWeapon(WeaponInstance weapon)
    {
        if (weapon == null) return;
        if (ActiveWeapons.Contains(weapon)) return;

        ActiveWeapons.Add(weapon);

        weapon.OnDealtDamage += HandleWeaponDealtDamage;
        weapon.OnParryContact += HandleWeaponParryContact;
        weapon.OnProjectileParryContact += HandleWeaponProjectileParryContact;

        _ctx.ComboController.SetWeapon(weapon.data);
        ApplyDodgeSetFromWeapon(weapon.data);

        UpdateAnimator();
        weapon.ConfigureForOwner(transform, _ctx.FactionComponent.Faction);

        CacheHandWeapon(weapon);
        weapon.DisableAllWeaponVfx();
    }

    public void ClearWeapons()
    {
        for (int i = 0; i < ActiveWeapons.Count; i++)
        {
            UnregisterWeapon(ActiveWeapons[i]);
        }

        ActiveWeapons.Clear();

        _rightWeapon = null;
        _leftWeapon = null;

        _ctx.ComboController.SetWeapon(baseWeapon);
        ApplyDodgeSetFromWeapon(baseWeapon);

        OnWeaponsCleared?.Invoke();

        UpdateAnimator();
    }

    public void ClearEquipedWeaponToNew()
    {
        for (int i = 0; i < ActiveWeapons.Count; i++)
        {
            UnregisterWeapon(ActiveWeapons[i]);
        }

        ActiveWeapons.Clear();

        _rightWeapon = null;
        _leftWeapon = null;

        UpdateAnimator();
    }

    #endregion

    #region IWeaponVfxUser

    public void ActivateAttack(int id) => ActiveVfxRig?.ActivateAttack(id);
    public void DeactiveAttack(int id) => ActiveVfxRig?.DeactiveAttack(id);

    public void SetActiveVfxRig(VFXRigContainer rig)
    {
        ActiveVfxRig = rig;
        ActiveVfxRig?.DeactivateAll();
    }

    #endregion

    #region Public API

    public void ActivateWeaponVfx(Enums.WeaponHand hand, int vfxId)
    {
        var w = GetWeapon(hand);
        if (w == null) return;

        w.SetWeaponVfx(vfxId, true);
    }

    public void DeactiveWeaponVfx(Enums.WeaponHand hand, int vfxId)
    {
        var w = GetWeapon(hand);
        if (w == null) return;

        w.SetWeaponVfx(vfxId, false);
    }

    // Timeline-friendly wrappers (UnityEvent)
    public void WeaponTrailOnRight(int vfxId) => ActivateWeaponVfx(Enums.WeaponHand.Right, vfxId);
    public void WeaponTrailOffRight(int vfxId) => DeactiveWeaponVfx(Enums.WeaponHand.Right, vfxId);
    public void WeaponTrailOnLeft(int vfxId) => ActivateWeaponVfx(Enums.WeaponHand.Left, vfxId);
    public void WeaponTrailOffLeft(int vfxId) => DeactiveWeaponVfx(Enums.WeaponHand.Left, vfxId);

    #endregion

    #region Helpers

    private void CacheHandWeapon(WeaponInstance weapon)
    {
        switch (weapon.Hand)
        {
            case Enums.WeaponHand.Right:
                _rightWeapon = weapon;
                break;
            case Enums.WeaponHand.Left:
                _leftWeapon = weapon;
                break;
        }
    }

    private void HandleWeaponDealtDamage(DamageContext target) => OnWeaponDealtDamage?.Invoke(target);
    private void HandleWeaponParryContact(AttackColliderHandler e) => OnParryContact?.Invoke(e);
    private void HandleWeaponProjectileParryContact(EnemyProjectile p) => OnProjectileParryContact?.Invoke(p);

    private void ApplyDodgeSetFromWeapon(WeaponData weaponData)
    {
        _ctx.Movement.DodgeSystem.SetDodgeSet(weaponData.dodgeSet);
    }

    private void UpdateAnimator()
    {
        if (ActiveWeapons.Count == 0)
        {
            _ctx.Animation.UpdateRuntimeAnimationController(baseWeapon.animatorOverride);
            _ctx.Animation.OnAnimatorControllerChanged();
            return;
        }

        var weapon = ActiveWeapons[0];
        _ctx.Animation.UpdateRuntimeAnimationController(
            weapon.data.animatorOverride != null
                ? weapon.data.animatorOverride
                : baseWeapon.animatorOverride);
        _ctx.Animation.OnAnimatorControllerChanged();
    }

    private void UpdateAnimatorForWeapon(WeaponData weapon)
    {
        if (_ctx?.Animation == null) return;

        var overrideController = weapon.animatorOverride != null
            ? weapon.animatorOverride
            : baseWeapon.animatorOverride;

        _ctx.Animation.UpdateRuntimeAnimationController(overrideController);
        _ctx.Animation.OnAnimatorControllerChanged();
    }

    private void UnregisterWeapon(WeaponInstance weapon)
    {
        if (weapon == null) return;

        weapon.OnDealtDamage -= HandleWeaponDealtDamage;
        weapon.OnParryContact -= HandleWeaponParryContact;
        weapon.OnProjectileParryContact -= HandleWeaponProjectileParryContact;
    }

    private WeaponInstance GetWeapon(Enums.WeaponHand hand)
    {
        return hand == Enums.WeaponHand.Left ? _leftWeapon : _rightWeapon;
    }

    #endregion
}