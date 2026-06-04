using System.Collections.Generic;
using UnityEngine;

/// Implements ISavable for player-owned systems: health, stamina, equipped items.
///
/// EQUIPMENT TIMING PROBLEM:
///   ApplyFromSave() runs in sceneLoaded (before Start()). At that point,
///   WeaponHandler._ctx is null — calling EquipmentHandler.Equip() for a weapon
///   triggers SpawnWeapon() → RegisterWeapon() → _ctx.ComboController.SetWeapon()
///   → NullReferenceException.
///
/// SOLUTION — two-phase equip:
///   Phase 1 (ApplyFromSave): store pending equipment in _pendingEquip list.
///             Apply non-weapon items immediately (armor has no WeaponHandler dep).
///   Phase 2 (ApplyEquipmentDeferred): called by WeaponHandler.Initialize() once
///             _ctx is ready. Equips all pending items through the full flow.
///
/// SCENE TRANSITION SAVE BUG:
///   Unity's scene unload order is:
///     1. OnDisable() + OnDestroy() of scene objects
///     2. sceneUnloaded event fires
///   If we unregister from SaveService in OnDestroy(), the savable is already gone
///   when sceneUnloaded calls SaveService.Save() → CaptureAll() skips this system
///   → equippedItems are never written → next scene loads with empty equipment.
///   Fix: capture directly into CurrentSave in OnDisable() instead of unregistering.
///   SaveService.OnSceneUnloaded only needs to WriteToDisk() — the capture is already done.
///
/// Ordering contract (SaveService.RegisterSavable call order):
///   1. PlayerInventory   — inventory stacks
///   2. PlayerSaveHandler — health + equipment
[RequireComponent(typeof(StaminaSystem))]
[RequireComponent(typeof(EquipmentHandler))]
public class PlayerSaveHandler : MonoBehaviour, ISaveable
{
    [SerializeField] private CharacterHealthSystem _health;
    private StaminaSystem _stamina;
    private EquipmentHandler _equipment;
    private WeaponHandler _weaponHandler;
    private SaveService _save;

    private readonly List<ItemStack> _pendingEquip = new(4);

    private static readonly Enums.EquipSlot[] _equipSlots = (Enums.EquipSlot[])System.Enum.GetValues(typeof(Enums.EquipSlot));

    #region Unity Callbacks

    private void Awake()
    {
        // _health is wired explicitly via SetHealthReference() from PlayerController
        // after _healthSystem.Initialize() runs. GetComponent is a last-resort fallback
        // in case SetHealthReference is never called (e.g. missing script or test scenes).
        if (_health == null) _health = GetComponent<CharacterHealthSystem>();
        _stamina = GetComponent<StaminaSystem>();
        _equipment = GetComponent<EquipmentHandler>();
        _weaponHandler = GetComponent<WeaponHandler>();

        if (GameServices.TryGet(out _save))
        {
            _save.RegisterSaveable(this);
        }
    }

    // Called by PlayerController.ExecuteDeferredInitializations() after _healthSystem.Initialize().
    // Ensures _health always points to the correct CharacterHealthSystem regardless of GameObject layout.
    public void SetHealthReference(CharacterHealthSystem health)
    {
        _health = health;
    }

    private void OnDisable()
    {
        // Capture directly into CurrentSave while this object is still alive.
        // OnDisable fires BEFORE OnDestroy and BEFORE the sceneUnloaded event,
        // so SaveService.WriteToDisk() (triggered by sceneUnloaded) will always
        // see the correct equipment data even though this savable has been removed.
        //
        // DEATH PENALTY GUARD: if ApplyDeathPenalty() already ran this frame,
        // _deathPenaltyApplied is true, and we must NOT capture — doing so would
        // overwrite the cleared equippedItems/consumableSlots with the live scene
        // state (player still has weapon + armor on their GameObject until destroyed).
        if (_save == null) return;
        if (_save.IsDeathPenaltyApplied) return;

        CaptureToSave(_save.CurrentSave);
    }

    private void OnDestroy()
    {
        _save?.UnregisterSaveable(this);
    }

    #endregion

    #region ISaveable

    public void CaptureToSave(SaveData data)
    {
        CaptureHealth(data);
        CaptureEquipment(data);
        CaptureConsumables(data);
    }

    public void ApplyFromSave(SaveData data)
    {
        // Health/stamina deferred to ApplyVitalsAfterInit() — MaxHealth not ready yet.
        StageEquipmentFromSave(data);
    }

    /// Restores health and stamina after CharacterStats is initialized
    /// (MaxHealth/MaxStamina are computed from equipment modifiers).
    /// Called by PlayerController.ExecuteDeferredInitializations().
    public void ApplyVitalsAfterInit(SaveData data)
    {
        _health?.SetCurrentHealth(data.player.currentHealth);
        _stamina?.SetCurrentStamina(data.player.currentStamina);
    }

    /// Phase 2 of equipment restore. Called by WeaponHandler.Initialize() once
    /// _ctx is ready. Equips all pending items through the full pipeline.
    /// Returns the WeaponData that was equipped (null if none).
    public WeaponData ApplyEquipmentDeferred(EquipmentHandler equipment)
    {
        WeaponData restoredWeapon = null;

        for (int i = 0; i < _pendingEquip.Count; i++)
        {
            var stack = _pendingEquip[i];
            equipment.Equip(stack);

            if (stack.data is WeaponData wd)
            {
                restoredWeapon = wd;
            }
        }

        _pendingEquip.Clear();
        return restoredWeapon;
    }

    public bool HasPendingEquipment() => _pendingEquip.Count > 0;

    #endregion

    #region Capture

    private void CaptureHealth(SaveData data)
    {
        data.player.currentHealth  = _health  != null && _health.IsAlive  ? _health.CurrentHealth   : -1f;
        data.player.currentStamina = _stamina != null                      ? _stamina.CurrentStamina : -1f;
    }

    private void CaptureEquipment(SaveData data)
    {
        data.inventory.equippedItems.Clear();

        for (int i = 0; i < _equipSlots.Length; i++)
        {
            Enums.EquipSlot slot = _equipSlots[i];
            var item = _equipment.Get(slot);
            if (item == null) continue;

            if (item is WeaponData wd && IsBaseWeapon(wd)) continue;

            var stack = _equipment.GetStack(slot);

            data.inventory.equippedItems.Add(
                ItemStackSaveHelper.CaptureEquipSlot(slot, item, stack)
            );
        }
    }

    private void CaptureConsumables(SaveData data)
    {
        data.inventory.consumableSlots.Clear();

        for (int i = 0; i < 4; i++)
        {
            var slot = ContainerRegistry.GetConsumableSlot(i);
            if (slot?.currentStack == null) continue;

            data.inventory.consumableSlots.Add(new ConsumableSlotSaveData
            {
                slotNumber = i,
                uniqueID = slot.currentStack.data?.uniqueID,
                quantity = slot.currentStack.quantity
            });
        }
    }

    #endregion

    #region Apply (private)

    /// Phase 1: resolve saved items and split into immediate (armor) vs deferred (weapons).
    private void StageEquipmentFromSave(SaveData data)
    {
        _pendingEquip.Clear();

        if (!GameServices.TryGet<SaveService>(out var save)) return;

        for (int i = 0; i < data.inventory.equippedItems.Count; i++)
        {
            var entry = data.inventory.equippedItems[i];
            var itemData = save.ResolveItem(entry.uniqueID) as EquipableItemData;
            if (itemData == null) continue;

            var stack = ItemStackSaveHelper.RestoreEquipableStack(entry, itemData);

            if (itemData is WeaponData)
            {
                _pendingEquip.Add(stack);
            }
            else
            {
                _equipment.Equip(stack);
            }
        }
    }

    private bool IsBaseWeapon(WeaponData weapon)
    {
        return _weaponHandler != null && _weaponHandler.baseWeapon == weapon;
    }

    #endregion
}