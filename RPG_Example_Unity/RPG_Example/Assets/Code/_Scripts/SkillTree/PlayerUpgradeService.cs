using System;
using UnityEngine;

/// Manages permanent stat upgrades purchased at the skill tree NPC.
/// Upgrades modify CharacterStat.baseValue directly — not via modifiers —
/// so they stack with equipment and buffs on top of a higher base.
///
/// Initialization contract (must run after CharacterStats and CharacterHealthSystem are ready):
///   Call SetHealthReference() then ApplyUpgrades() from PlayerController.ExecuteDeferredInitializations,
///   right after _healthSystem.Initialize() and before ApplyVitalsAfterInit().
[RequireComponent(typeof(CharacterStats))]
public class PlayerUpgradeService : MonoBehaviour, ISaveable
{
    #region Fields

    [SerializeField] private UpgradeNodeSO[] _nodes;

    private int[] _levels;
    private CharacterStats _stats;
    private CharacterHealthSystem _health;
    private SaveService _save;

    #endregion

    #region Properties

    public int NodeCount => _nodes != null ? _nodes.Length : 0;
    public event Action OnUpgradesChanged;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _levels = new int[_nodes != null ? _nodes.Length : 0];

        if (GameServices.TryGet(out _save))
            _save.RegisterSaveable(this);
    }

    private void OnDestroy()
    {
        _save?.UnregisterSaveable(this);
    }

    #endregion

    #region Public API

    public void SetHealthReference(CharacterHealthSystem health)
    {
        _health = health;
    }

    /// Applies all saved upgrade levels to CharacterStat.baseValue.
    /// Must be called after CharacterStats and CharacterHealthSystem are initialized.
    /// Inventory capacity is intentionally excluded: PlayerInventoryCapacityController
    /// restores its own level via ISaveable.ApplyFromSave independently.
    public void ApplyUpgrades()
    {
        for (int i = 0; i < _nodes.Length; i++)
        {
            if (_levels[i] <= 0) continue;
            if (_nodes[i].upgradeType == Enums.UpgradeType.Inventory) continue;

            float totalValue = _nodes[i].GetTotalValueAtLevel(_levels[i]);
            ApplyValueToStat(_nodes[i], totalValue);
        }
    }

    public UpgradeNodeSO GetNode(int index) => index < _nodes.Length ? _nodes[index] : null;

    public int GetLevel(int index) => index < _levels.Length ? _levels[index] : 0;

    public int GetCost(int index)
    {
        if (index >= _nodes.Length) return 0;
        return _nodes[index].GetCostForLevel(_levels[index]);
    }

    public bool IsMaxLevel(int index)
        => index < _nodes.Length && _levels[index] >= _nodes[index].maxLevel;

    /// Attempts to purchase one level of the upgrade at the given index.
    /// Returns false if at max level or insufficient AuraDust.
    public bool TryUpgrade(int index)
    {
        if (index >= _nodes.Length) return false;

        var node         = _nodes[index];
        int currentLevel = _levels[index];

        if (currentLevel >= node.maxLevel) return false;

        int cost = node.GetCostForLevel(currentLevel);
        if (!PlayerInventory.Instance.TrySpend(cost)) return false;

        float valueDelta = node.GetTotalValueAtLevel(currentLevel + 1) - node.GetTotalValueAtLevel(currentLevel);
        _levels[index]++;
        ApplyValueToStat(node, valueDelta);

        OnUpgradesChanged?.Invoke();

        _save?.Save();

        return true;
    }

    #endregion

    #region ISaveable

    public void CaptureToSave(SaveData data)
    {
        data.upgrades.levels.Clear();
        for (int i = 0; i < _levels.Length; i++)
            data.upgrades.levels.Add(_levels[i]);
    }

    public void ApplyFromSave(SaveData data)
    {
        for (int i = 0; i < _levels.Length && i < data.upgrades.levels.Count; i++)
            _levels[i] = data.upgrades.levels[i];
    }

    #endregion

    #region Internal Logic

    private void ApplyValueToStat(UpgradeNodeSO node, float value)
    {
        switch (node.upgradeType)
        {
            case Enums.UpgradeType.Health:
                ApplyHealthUpgrade(value);
                break;
            case Enums.UpgradeType.Defense:
            case Enums.UpgradeType.Stamina:
            case Enums.UpgradeType.Speed:
            case Enums.UpgradeType.Attack:
            case Enums.UpgradeType.Weight:
                ApplyStatUpgrade(node.upgradeType, value);
                break;
            case Enums.UpgradeType.Inventory:
                ExpandInventory();
                break;
        }
    }

    private void ApplyHealthUpgrade(float value)
    {
        var stat = _stats.GetStat(Enums.StatType.Health);
        if (stat == null) return;

        float previousMax = _health != null ? _health.MaxHealth : 0f;
        stat.baseValue += value;

        if (_health != null)
            _health.NotifyMaxHealthChanged(previousMax);
    }

    private void ApplyStatUpgrade(Enums.UpgradeType upgradeType, float value)
    {
        Enums.StatType statType = upgradeType switch
        {
            Enums.UpgradeType.Defense => Enums.StatType.Defense,
            Enums.UpgradeType.Stamina => Enums.StatType.Stamina,
            Enums.UpgradeType.Speed   => Enums.StatType.Speed,
            Enums.UpgradeType.Attack  => Enums.StatType.Attack,
            Enums.UpgradeType.Weight  => Enums.StatType.Weight,
            _                         => Enums.StatType.Health
        };

        var stat = _stats.GetStat(statType);
        if (stat == null) return;

        stat.baseValue += value;
    }

    private void ExpandInventory()
    {
        PlayerInventoryCapacityController.Instance.UpgradeCapacity();
    }

    #endregion
}
