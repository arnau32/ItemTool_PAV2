using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "LootTable", menuName = "Expedition/Loot/Loot Table")]
public class LootTable : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Header("Entry Type")]
        public Enums.LootEntryType entryType = Enums.LootEntryType.Item;

        [Header("Item")]
        public ItemData item;

        [Header("Nested Loot Table")]
        public LootTable nestedTable;

        [Header("Cantidad")]
        [Min(1)] public int minQuantity = 1;
        [Min(1)] public int maxQuantity = 1;

        [Header("Peso")]
        [Min(0)] public int weight = 1;

        [Header("Equipable Override")]
        public Enums.LootEntryEquipableOverrideMode equipableOverrideMode =
            Enums.LootEntryEquipableOverrideMode.None;

        public Enums.ItemRarity overrideFixedRarity = Enums.ItemRarity.Common;
        
    }

    public List<Entry> guaranteedEntries = new();
    public List<Entry> weightedEntries = new();

    [Header("Random Drop Count")]
    [Min(0)] public int minRandomPicks = 0;
    [Min(0)] public int maxRandomPicks = 0;

#if UNITY_EDITOR
    private void OnValidate()
    {
        minRandomPicks = Mathf.Max(0, minRandomPicks);
        maxRandomPicks = Mathf.Max(minRandomPicks, maxRandomPicks);

        ValidateEntries(guaranteedEntries, clampWeight: false);
        ValidateEntries(weightedEntries, clampWeight: true);
    }

    private void ValidateEntries(List<Entry> entries, bool clampWeight)
    {
        if (entries == null)
            return;

        foreach (Entry entry in entries)
        {
            if (entry == null)
                continue;

            entry.minQuantity = Mathf.Max(1, entry.minQuantity);
            entry.maxQuantity = Mathf.Max(entry.minQuantity, entry.maxQuantity);

            if (clampWeight)
                entry.weight = Mathf.Max(0, entry.weight);

            if (entry.entryType == Enums.LootEntryType.LootTable && entry.nestedTable == this)
            {
                Debug.LogWarning($"LootTable '{name}' cannot reference itself directly.");
                entry.nestedTable = null;
            }

            bool validEquipableOverrideTarget =
                entry.entryType == Enums.LootEntryType.Item &&
                entry.item is EquipableItemData;

            if (!validEquipableOverrideTarget)
            {
                entry.equipableOverrideMode = Enums.LootEntryEquipableOverrideMode.None;
            }
        }
    }
#endif
}