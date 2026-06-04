using System.Collections;
using System.Collections.Generic;
using Gameplay.Items;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EquipmentHandler), typeof(EnemyCombat))]
public sealed class EnemyWeaponHandler : MonoBehaviour, IWeaponRegistry, IWeaponVfxUser
{
    #region Fields

    [Header("Weapon")] public WeaponData[] baseWeapon;
    public WeaponData equipedWeapon;

    [SerializeField] private GameObject _slashSpawnerPoint;
    public List<WeaponInstance> ActiveWeapons { get; private set; } = new List<WeaponInstance>(2);
    public GameObject SlashSpawnerPoint => _slashSpawnerPoint;
    public VFXRigContainer ActiveVfxRig { get; private set; }

    private EquipmentHandler _equipmentHandler;
    private EnemyCombat _combat;
    private EnemyContext _ctx;

    private WeaponInstance _rightWeapon;
    private WeaponInstance _leftWeapon;

    private bool _isInitialized;

    #endregion

    #region Events

    public event System.Action OnWeaponsCleared;

    #endregion

    #region Initialization

    public void Initialize(EnemyContext ctx)
    {
        if (_isInitialized)
        {
            Debug.LogWarning("[EnemyWeaponHandler] Already initialized.", this);
            return;
        }

        _ctx = ctx;
        if (_equipmentHandler == null) _equipmentHandler = GetComponent<EquipmentHandler>();
        if (_combat == null) _combat = _ctx.EnemyCombat;

        if (baseWeapon != null)
        {
            int indx = Random.Range(0, baseWeapon.Length);
            equipedWeapon = baseWeapon[indx];
            _equipmentHandler.Equip(equipedWeapon);
            if (!Application.isEditor)
            {
                StartCoroutine(ValidateWeaponsAfterFrame());
                return;
            }
        }

        _isInitialized = true;
    }

    private void OnDisable()
    {
        // Pool-reuse safety: reset so Initialize() runs fully on the next SetActive(true).
        // EquipmentHandler.EquipCore() will Unequip (destroy old weapon GO) then SpawnWeapon
        // (instantiate a fresh one), restoring the full event chain for the new cycle.
        _isInitialized = false;
    }

    private void OnDestroy()
    {
        OnWeaponsCleared = null;
    }

    #endregion

    #region IWeaponRegistry

    public void RegisterWeapon(WeaponInstance weapon)
    {
        if (weapon == null)
        {
            Debug.LogError("[EnemyWeaponHandler] Attempted to register null weapon.", this);
            return;
        }

        if (ActiveWeapons.Contains(weapon))
        {
            Debug.LogWarning($"[EnemyWeaponHandler] Weapon {weapon.name} already registered.", this);
            return;
        }

        ActiveWeapons.Add(weapon);

        if (_combat != null)
        {
            // First weapon also becomes the legacy _equippedWeapon for backward-compat callers.
            if (ActiveWeapons.Count == 1)
                _combat.SetEquippedWeapon(weapon);

            // Always register by hand so dual-weapon attacks route to the correct WeaponInstance.
            _combat.RegisterWeaponByHand(weapon);
        }
        else
        {
            Debug.LogError("[EnemyWeaponHandler] EnemyCombat is null during RegisterWeapon.", this);
        }

        if (_ctx != null && _ctx.FactionComponent != null)
            weapon.ConfigureForOwner(transform, _ctx.FactionComponent.Faction);
        else
            Debug.LogError("[EnemyWeaponHandler] Cannot configure weapon — context or faction is null.", this);

        CacheHandWeapon(weapon);
        weapon.DisableAllWeaponVfx();
    }

    public void NotifyWeaponDataEquipped(WeaponData weaponData)
    {
    }

    public void ClearEquipedWeaponToNew()
    {
        ActiveWeapons.Clear();
        _rightWeapon = null;
        _leftWeapon = null;

        _combat?.SetEquippedWeapon(null);
    }

    public void ClearWeapons()
    {
        ClearEquipedWeaponToNew();
        OnWeaponsCleared?.Invoke();
    }

    #endregion

    #region IWeaponVfxUser

    public void ActivateAttack(int id)
    {
        if (ActiveVfxRig == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[EnemyWeaponHandler] No ActiveVfxRig to activate attack {id}.", this);
#endif
            return;
        }

        ActiveVfxRig.ActivateAttack(id);
    }

    public void DeactiveAttack(int id)
    {
        if (ActiveVfxRig == null) return;
        ActiveVfxRig.DeactiveAttack(id);
    }

    public void SetActiveVfxRig(VFXRigContainer rig)
    {
        ActiveVfxRig = rig;
        ActiveVfxRig?.DeactivateAll();

        if (rig is ProjectileVfxRig projRig && _ctx != null)
            projRig.Initialize(_ctx);
    }

    #endregion

    #region Public API

    public void ActivateWeaponVfx(Enums.WeaponHand hand, int vfxId)
    {
        var w = GetWeapon(hand);
        if (w == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[EnemyWeaponHandler] No weapon for hand {hand} when activating VFX {vfxId}.", this);
#endif
            return;
        }

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

    private IEnumerator ValidateWeaponsAfterFrame()
    {
        yield return null;
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        float timeout = Time.time + 0.5f;
        while (ActiveWeapons.Count == 0 && Time.time < timeout)
        {
            yield return null;
        }

        if (ActiveWeapons.Count == 0 && baseWeapon != null)
        {
            Debug.LogError($"[EnemyWeaponHandler] Weapon registration failed for {name}. No weapons after timeout.", this);
        }
        else
        {
            for (int i = 0; i < ActiveWeapons.Count; i++)
            {
                ValidateWeaponVfx(ActiveWeapons[i]);
            }
        }

        _isInitialized = true;
    }

    private void ValidateWeaponVfx(WeaponInstance weapon)
    {
        if (weapon == null) return;
        if (weapon.GetComponent<WeaponVfxController>() == null) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[EnemyWeaponHandler] Weapon {weapon.name} VFX controller validated.", this);
#endif
    }

    private void CacheHandWeapon(WeaponInstance weapon)
    {
        if (weapon == null) return;

        switch (weapon.Hand)
        {
            case Enums.WeaponHand.Right:
                _rightWeapon = weapon;
                break;
            case Enums.WeaponHand.Left:
                _leftWeapon = weapon;
                break;
            default:
                Debug.LogError($"[EnemyWeaponHandler] Unknown weapon hand: {weapon.Hand}.", this);
                break;
        }
    }

    private WeaponInstance GetWeapon(Enums.WeaponHand hand)
    {
        return hand == Enums.WeaponHand.Left ? _leftWeapon : _rightWeapon;
    }
    
    #endregion
}