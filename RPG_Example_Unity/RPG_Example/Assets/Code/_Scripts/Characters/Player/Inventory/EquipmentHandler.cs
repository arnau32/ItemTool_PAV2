using System;
using System.Collections;
using System.Collections.Generic;
using Gameplay.Items;
using UnityEngine;
using VInspector;

[DisallowMultipleComponent]
public class EquipmentHandler : MonoBehaviour
{
    #region Fields

    public SerializedDictionary<Enums.EquipSlot, Transform> _bindings;
    public SerializedDictionary<Enums.WeaponHandType, Transform[]> _weaponBindings;

    [Header("VFX Rigs (prefabs) by Weapon Family")]
    public SerializedDictionary<Enums.WeaponFamily, GameObject> _vfxRigByFamily;

    [Header("Quality VFX")]
    [SerializeField] private bool _enableQualityVfx;
    [SerializeField] private RarityColorConfig _rarityColors;

    private CharacterStats _stats;
    [SerializeField] private CharacterHealthSystem _health;
    [Tooltip("When true, weapon StatModifiers are not applied to CharacterStats. Enable on enemy prefabs.")]
    [SerializeField] private bool _ignoreWeaponModifiers;

    private IWeaponRegistry _weaponRegistry;
    private readonly Dictionary<Enums.EquipSlot, EquipableItemData> _equipped       = new();
    private readonly Dictionary<Enums.EquipSlot, List<GameObject>>  _spawned        = new();
    private readonly Dictionary<Enums.EquipSlot, ItemStack>         _equippedStacks = new();

    #endregion

    public WeaponHandler WeaponHandler => _weaponRegistry as WeaponHandler;

    #region Events

    public event Action<WeaponData> OnEquipWeapon;
    public event Action OnUnEquipWeapon;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDestroy()
    {
        if (_weaponRegistry is WeaponHandler wh)
        {
            wh.OnWeaponsCleared -= HandleWeaponsCleared;
        }
    }

    private void EnsureInitialized()
    {
        if (_stats == null) _stats = GetComponent<CharacterStats>();

        if (_weaponRegistry != null) return;
        
        _weaponRegistry = GetComponent<IWeaponRegistry>();

        if (_weaponRegistry is WeaponHandler wh)
        {
            wh.OnWeaponsCleared -= HandleWeaponsCleared;
            wh.OnWeaponsCleared += HandleWeaponsCleared;
        }
    }

    private void HandleWeaponsCleared()
    {
        if (_weaponRegistry is WeaponHandler wh)
        {
            Equip(wh.baseWeapon);
        }
    }

    #endregion

    #region Public API

    public bool Equip(EquipableItemData item)
    {
        if (item == null) return false;
        _equippedStacks.Remove(item.equipSlot);
        return EquipCore(item, item.modifiers, item.itemRarity);
    }

    public bool Equip(ItemStack stack)
    {
        if (stack?.data is not EquipableItemData equipable) return false;
        bool ok = EquipCore(equipable, stack.GetEffectiveModifiers(), stack.GetEffectiveRarity());
        if (ok) _equippedStacks[equipable.equipSlot] = stack;
        return ok;
    }

    public ItemStack GetStack(Enums.EquipSlot slot)
    {
        _equippedStacks.TryGetValue(slot, out var s);
        return s;
    }

    private bool EquipCore(EquipableItemData item, List<StatModifier> modifiers, Enums.ItemRarity rarity = Enums.ItemRarity.Common)
    {
        EnsureInitialized();

        var slot = item.equipSlot;

        if (_equipped.ContainsKey(slot)) { Unequip(slot, true); }

        _equipped[slot] = item;
        _spawned[slot] = new List<GameObject>();

        if (item is WeaponData weaponData)
        {
            _weaponRegistry?.NotifyWeaponDataEquipped(weaponData);

            SpawnWeapon(weaponData, slot, rarity);
            SpawnVFXRig(weaponData, slot);
            OnEquipWeapon?.Invoke(weaponData);

            string weaponAnalyticsId = !string.IsNullOrEmpty(weaponData.uniqueID) ? weaponData.uniqueID : weaponData.itemNameID;
            CombatAnalytics.WeaponEquipped(weaponAnalyticsId, weaponData.familyType.ToString());

            if (!_ignoreWeaponModifiers)
                ApplyStats(item, modifiers);
        }
        else
        {
            SpawnEquipment(item, slot);
            ApplyStats(item, modifiers);
        }

        return true;
    }

    public bool Unequip(Enums.EquipSlot slot, bool isNew)
    {
        if (!_equipped.TryGetValue(slot, out var item)) return false;

        EnsureInitialized();

        float prevMax = _health != null ? _health.MaxHealth : 0f;
        bool affectsHealth = ItemAffectsHealth(item);

        _stats.RemoveModifiersFromSource(item);

        if (affectsHealth && _health != null)
            _health.NotifyMaxHealthChanged(prevMax);

        if (_spawned.TryGetValue(slot, out var list))
        {
            for (int i = 0; i < list.Count; i++)
            {
                Destroy(list[i]);
            }
        }

        _spawned.Remove(slot);
        _equipped.Remove(slot);
        _equippedStacks.Remove(slot);

        if (slot != Enums.EquipSlot.Weapon) return true;

        if (_weaponRegistry != null)
        {
            if (isNew) _weaponRegistry.ClearEquipedWeaponToNew();
            else _weaponRegistry.ClearWeapons();
        }

        OnUnEquipWeapon?.Invoke();

        return true;
    }

    public WeaponData GetCurrentWeapon()
    {
        if (_equipped.TryGetValue(Enums.EquipSlot.Weapon, out var item)) return item as WeaponData;
        return null;
    }

    public EquipableItemData Get(Enums.EquipSlot slot)
    {
        _equipped.TryGetValue(slot, out var i);
        return i;
    }

    #endregion

    #region Weapon Spawning

    private void RegisterWeaponInstance(GameObject go, Enums.EquipSlot slot)
    {
        _spawned[slot].Add(go);

        if (!Application.isEditor)
        {
            StartCoroutine(DelayedWeaponRegistration(go));
            return;
        }

        var w = go.GetComponent<WeaponInstance>();
        if (w != null && _weaponRegistry != null)
        {
            _weaponRegistry.RegisterWeapon(w);
        }
        else
        {
            Debug.LogWarning($"[EquipmentHandler] WeaponInstance not found on {go.name} in editor");
        }
    }

    private IEnumerator DelayedWeaponRegistration(GameObject go)
    {
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        if (go == null)
        {
            Debug.LogWarning("[EquipmentHandler] Weapon GameObject was destroyed before registration");
            yield break;
        }

        var w = go.GetComponent<WeaponInstance>();
        if (w == null)
        {
            Debug.LogError($"[EquipmentHandler] WeaponInstance still not found on {go.name} after frame delay!");
            yield break;
        }

        if (_weaponRegistry != null)
        {
            _weaponRegistry.RegisterWeapon(w);
        }
        else
        {
            Debug.LogError("[EquipmentHandler] WeaponRegistry is null when trying to register weapon!");
        }
    }

    private void SpawnWeapon(WeaponData weaponData, Enums.EquipSlot slot, Enums.ItemRarity rarity)
    {
        var sockets = _weaponBindings[weaponData.handType];

        switch (weaponData.handType)
        {
            case Enums.WeaponHandType.Single:
            {
                var go = Instantiate(weaponData.prefab, sockets[0]);
                ApplyQualityVfx(go, rarity);
                RegisterWeaponInstance(go, slot);
                break;
            }

            case Enums.WeaponHandType.DualSymmetric:
            case Enums.WeaponHandType.DualAsymetric:
            {
                SpawnDualWeapons(weaponData, sockets, slot, rarity);
                break;
            }

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void SpawnDualWeapons(WeaponData weaponData, Transform[] sockets, Enums.EquipSlot slot, Enums.ItemRarity rarity)
    {
        var goR = Instantiate(weaponData.prefab, sockets[0]);
        var goL = Instantiate(weaponData.prefabVariant, sockets[1]);

        var wR = goR.GetComponent<WeaponInstance>();
        if (wR != null) wR.SetHand(Enums.WeaponHand.Right);

        var wL = goL.GetComponent<WeaponInstance>();
        if (wL != null) wL.SetHand(Enums.WeaponHand.Left);

        ApplyQualityVfx(goR, rarity);
        ApplyQualityVfx(goL, rarity);

        RegisterWeaponInstance(goR, slot);
        RegisterWeaponInstance(goL, slot);
    }

    private void ApplyQualityVfx(GameObject weaponGo, Enums.ItemRarity rarity)
    {
        if (!_enableQualityVfx) return;
        if (rarity == Enums.ItemRarity.Common) return;
        if (_rarityColors == null) return;
        var instance = weaponGo.GetComponent<WeaponInstance>();
        if (instance == null) return;
        instance.ApplyQualityColor(_rarityColors.GetColor(rarity));
    }

    private void SpawnVFXRig(WeaponData weaponData, Enums.EquipSlot slot)
    {
        _weaponRegistry?.SetActiveVfxRig(null);

        if (weaponData == null) return;
        if (_weaponRegistry == null) return;
        if (_weaponRegistry.SlashSpawnerPoint == null) return;

        if (_vfxRigByFamily == null ||
            !_vfxRigByFamily.TryGetValue(weaponData.familyType, out var rigPrefabOrInstance) ||
            rigPrefabOrInstance == null)
        {
            return;
        }

        VFXRigContainer rig;

        if (rigPrefabOrInstance.scene.IsValid())
        {
            // Already a scene instance — use directly without instantiating.
            rig = rigPrefabOrInstance.GetComponent<VFXRigContainer>();
        }
        else
        {
            var parent = _weaponRegistry.SlashSpawnerPoint.transform;
            var go = Instantiate(rigPrefabOrInstance, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            _spawned[slot].Add(go);
            rig = go.GetComponent<VFXRigContainer>();
        }

        _weaponRegistry.SetActiveVfxRig(rig);
    }

    #endregion

    #region Equipment Spawning

    private void SpawnEquipment(EquipableItemData item, Enums.EquipSlot slot)
    {
        var parent = _bindings[slot];
        if (item.prefab == null) return;
        var go = Instantiate(item.prefab, parent);
        _spawned[slot].Add(go);
    }

    #endregion

    #region Stats

    private void ApplyStats(EquipableItemData item, List<StatModifier> modifiers)
    {
        if (modifiers == null || modifiers.Count == 0) return;

        if (_stats == null)
        {
            Debug.LogError($"[EquipmentHandler] CharacterStats not found on {name}, cannot apply modifiers.", this);
            return;
        }

        float prevMax = _health != null ? _health.MaxHealth : 0f;
        bool affectsHealth = false;

        for (int i = 0; i < modifiers.Count; i++)
        {
            var m = modifiers[i];

            int effectiveOrder = m.order != 0 ? m.order : (int)m.type;
            var clone = new StatModifier(m.value, m.type, effectiveOrder, item, m.statTypeAffected);

            _stats.AddModifier(m.statTypeAffected, clone);

            if (m.statTypeAffected == Enums.StatType.Health)
            {
                affectsHealth = true;
            }
        }

        if (affectsHealth && _health != null)
            _health.NotifyMaxHealthChanged(prevMax);
    }

    private static bool ItemAffectsHealth(EquipableItemData item)
    {
        if (item?.modifiers == null) return false;

        for (int i = 0; i < item.modifiers.Count; i++)
        {
            if (item.modifiers[i].statTypeAffected == Enums.StatType.Health) return true;
        }

        return false;
    }

    #endregion
}