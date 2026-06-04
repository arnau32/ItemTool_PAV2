using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "StatModifierDescriptionConfig", menuName = "Items/Stat Modifier Description Config")]
public class StatModifierDescriptionConfig : ScriptableObject
{
    [Serializable]
    public struct StatEntry
    {
        public Enums.StatType statType;
        public LocalizedString localizedName;
        public Sprite icon;
        public string iconClass;
    }

    [SerializeField] private List<StatEntry> _entries = new();

    private Dictionary<Enums.StatType, StatEntry> _lookup;

    private void OnEnable() => BuildLookup();

    private void BuildLookup()
    {
        _lookup = new Dictionary<Enums.StatType, StatEntry>(_entries.Count);
        foreach (var entry in _entries)
            _lookup[entry.statType] = entry;
    }

    public bool TryGetEntry(Enums.StatType statType, out StatEntry entry)
    {
        if (_lookup == null) BuildLookup();
        return _lookup.TryGetValue(statType, out entry);
    }
}
