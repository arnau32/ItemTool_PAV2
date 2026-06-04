using System.Collections.Generic;
using UnityEngine;

public static class LootSystem
{
    private const int MaxNestedLootDepth = 8;

    /// <summary>
    /// Devuelve la loot table del punto si tiene; si no, usa una default de la expedicion.
    /// Si ninguna existe, devuelve null.
    /// </summary>
    public static LootTable ResolveLootTable(LootPoint point, ExpeditionDefinition def)
    {
        if (point != null && point.LootTables != null && point.LootTables.Count > 0)
        {
            int idx = Random.Range(0, point.LootTables.Count);
            return point.LootTables[idx];
        }

        if (def != null && def.defaultLootTables != null && def.defaultLootTables.Count > 0)
        {
            int idx = Random.Range(0, def.defaultLootTables.Count);
            return def.defaultLootTables[idx];
        }

        return null;
    }

    public static List<ItemStack> Roll(LootTable table)
    {
        return RollInternal(table, new HashSet<LootTable>(), 0);
    }

    private static List<ItemStack> RollInternal(LootTable table, HashSet<LootTable> visited, int depth)
    {
        var results = new List<ItemStack>();

        if (table == null)
            return results;

        if (depth > MaxNestedLootDepth)
        {
            Debug.LogError($"[LootSystem] Max nested loot table depth exceeded at '{table.name}'.");
            return results;
        }

        if (!visited.Add(table))
        {
            Debug.LogError($"[LootSystem] Recursive loot table loop detected at '{table.name}'.");
            return results;
        }

        AddGuaranteedDrops(table, results, visited, depth);
        AddWeightedDrops(table, results, visited, depth);

        visited.Remove(table);
        return results;
    }

    private static void AddGuaranteedDrops(LootTable table, List<ItemStack> results, HashSet<LootTable> visited, int depth)
    {
        if (table.guaranteedEntries == null)
            return;

        foreach (LootTable.Entry entry in table.guaranteedEntries)
        {
            if (!IsValidGuaranteedEntry(entry))
                continue;

            ResolveEntry(entry, results, visited, depth);
        }
    }

    private static void AddWeightedDrops(LootTable table, List<ItemStack> results, HashSet<LootTable> visited, int depth)
    {
        if (table.weightedEntries == null || table.weightedEntries.Count == 0)
            return;

        List<LootTable.Entry> pool = BuildWeightedPool(table.weightedEntries);
        if (pool.Count == 0)
            return;

        int minDrops = Mathf.Max(0, table.minRandomPicks);
        int maxDrops = Mathf.Max(minDrops, table.maxRandomPicks);

        int maxPossibleDrops = Mathf.Min(maxDrops, pool.Count);
        int minPossibleDrops = Mathf.Min(minDrops, maxPossibleDrops);

        int rollCount = Random.Range(minPossibleDrops, maxPossibleDrops + 1);

        for (int i = 0; i < rollCount; i++)
        {
            LootTable.Entry selected = SelectByWeight(pool);
            if (selected == null)
                break;

            ResolveEntry(selected, results, visited, depth);

            // sin reemplazo
            pool.Remove(selected);
        }
    }

    private static void ResolveEntry(LootTable.Entry entry, List<ItemStack> results, HashSet<LootTable> visited, int depth)
    {
        if (entry == null)
            return;

        int quantity = Random.Range(entry.minQuantity, entry.maxQuantity + 1);
        if (quantity <= 0)
            return;

        switch (entry.entryType)
        {
            case Enums.LootEntryType.Item:
                AddItemResults(entry.item, entry, quantity, results);
                break;

            case Enums.LootEntryType.LootTable:
                if (entry.nestedTable == null)
                    return;

                for (int i = 0; i < quantity; i++)
                {
                    results.AddRange(RollInternal(entry.nestedTable, visited, depth + 1));
                }
                break;
        }
    }

    private static void AddItemResults(ItemData item, LootTable.Entry entry, int quantity, List<ItemStack> results)
    {
        if (item == null || quantity <= 0)
            return;

        if (item is EquipableItemData equipable)
        {
            for (int i = 0; i < quantity; i++)
            {
                results.Add(CreateEquipableStack(equipable, entry));
            }
        }
        else
        {
            results.Add(CreateRegularStack(item, quantity));
        }
    }

    private static List<LootTable.Entry> BuildWeightedPool(List<LootTable.Entry> entries)
    {
        var pool = new List<LootTable.Entry>();

        foreach (LootTable.Entry entry in entries)
        {
            if (!IsValidWeightedEntry(entry))
                continue;

            pool.Add(entry);
        }

        return pool;
    }

    private static LootTable.Entry SelectByWeight(List<LootTable.Entry> pool)
    {
        if (pool == null || pool.Count == 0)
            return null;

        int totalWeight = 0;
        foreach (LootTable.Entry entry in pool)
            totalWeight += Mathf.Max(0, entry.weight);

        if (totalWeight <= 0)
            return null;

        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (LootTable.Entry entry in pool)
        {
            cumulative += Mathf.Max(0, entry.weight);

            if (roll < cumulative)
                return entry;
        }

        return pool[pool.Count - 1];
    }

    private static bool IsValidGuaranteedEntry(LootTable.Entry entry)
    {
        if (entry == null)
            return false;

        if (entry.minQuantity <= 0 || entry.maxQuantity < entry.minQuantity)
            return false;

        return entry.entryType switch
        {
            Enums.LootEntryType.Item => entry.item != null,
            Enums.LootEntryType.LootTable => entry.nestedTable != null,
            _ => false
        };
    }

    private static bool IsValidWeightedEntry(LootTable.Entry entry)
    {
        if (entry == null)
            return false;

        if (entry.minQuantity <= 0 || entry.maxQuantity < entry.minQuantity || entry.weight <= 0)
            return false;

        return entry.entryType switch
        {
            Enums.LootEntryType.Item => entry.item != null,
            Enums.LootEntryType.LootTable => entry.nestedTable != null,
            _ => false
        };
    }

    private static ItemStack CreateRegularStack(ItemData item, int quantity)
    {
        return new ItemStack
        {
            data = item,
            RootVisual = null,
            quantity = quantity,

            isInstanced = false,
            runtimeInstanceId = null,
            rolledRarity = item != null ? item.itemRarity : Enums.ItemRarity.Common,
            rolledModifiers = new List<StatModifier>(),

            gridX = 0,
            gridY = 0,
            rotationIndex = 0,
            rotated = false
        };
    }

    private static ItemStack CreateEquipableStack(EquipableItemData equipable, LootTable.Entry entry)
    {
        Enums.ItemRarity finalRarity = ResolveEquipableRarity(equipable, entry);
        List<StatModifier> finalModifiers = ResolveEquipableModifiers(equipable, entry, finalRarity);

        return new ItemStack
        {
            data = equipable,
            RootVisual = null,
            quantity = 1,

            isInstanced = true,
            runtimeInstanceId = System.Guid.NewGuid().ToString(),
            rolledRarity = finalRarity,
            rolledModifiers = finalModifiers,

            gridX = 0,
            gridY = 0,
            rotationIndex = 0,
            rotated = false
        };
    }

    private static Enums.ItemRarity ResolveEquipableRarity(EquipableItemData equipable, LootTable.Entry entry)
    {
        if (entry != null && entry.equipableOverrideMode != Enums.LootEntryEquipableOverrideMode.None)
        {
            switch (entry.equipableOverrideMode)
            {
                case Enums.LootEntryEquipableOverrideMode.FixedRarityRandomStats:
                case Enums.LootEntryEquipableOverrideMode.FixedRarityFixedStats:
                    return entry.overrideFixedRarity;

                case Enums.LootEntryEquipableOverrideMode.RandomRarityRandomStats:
                {
                    Enums.ItemRarity rolled = RarityUtility.RollRandomRarity();
                    return rolled;
                }
            }
        }

        switch (equipable.rollMode)
        {
            case Enums.EquipableRollMode.FixedRarityRandomStats:
            case Enums.EquipableRollMode.FixedRarityFixedStats:
                return equipable.itemRarity;

            case Enums.EquipableRollMode.RandomRarityRandomStats:
            {
                Enums.ItemRarity rolled = RarityUtility.RollRandomRarity();
                return rolled;
            }

            default:
                return equipable.itemRarity;
        }
    }

    private static List<StatModifier> ResolveEquipableModifiers(
        EquipableItemData equipable,
        LootTable.Entry entry,
        Enums.ItemRarity rarity)
    {
        if (entry != null && entry.equipableOverrideMode != Enums.LootEntryEquipableOverrideMode.None)
        {
            switch (entry.equipableOverrideMode)
            {
                case Enums.LootEntryEquipableOverrideMode.FixedRarityFixedStats:
                    return CloneModifiers(equipable.modifiers);

                case Enums.LootEntryEquipableOverrideMode.FixedRarityRandomStats:
                case Enums.LootEntryEquipableOverrideMode.RandomRarityRandomStats:
                    return RollModifiers(equipable.modifiers, rarity);
            }
        }

        switch (equipable.rollMode)
        {
            case Enums.EquipableRollMode.FixedRarityFixedStats:
                return CloneModifiers(equipable.modifiers);

            case Enums.EquipableRollMode.FixedRarityRandomStats:
            case Enums.EquipableRollMode.RandomRarityRandomStats:
                return RollModifiers(equipable.modifiers, rarity);

            default:
                return CloneModifiers(equipable.modifiers);
        }
    }

    private static List<StatModifier> CloneModifiers(List<StatModifier> sourceModifiers)
    {
        var result = new List<StatModifier>();

        if (sourceModifiers == null)
            return result;

        foreach (StatModifier mod in sourceModifiers)
        {
            if (mod == null) continue;
            result.Add(mod.Clone());
        }

        return result;
    }

    private static List<StatModifier> RollModifiers(List<StatModifier> sourceModifiers, Enums.ItemRarity rarity)
    {
        var result = new List<StatModifier>();

        if (sourceModifiers == null)
            return result;

        (float minMultiplier, float maxMultiplier) = RarityUtility.GetMultiplierRange(rarity);

        foreach (StatModifier mod in sourceModifiers)
        {
            if (mod == null)
                continue;

            float multiplier = Random.Range(minMultiplier, maxMultiplier);
            float rolledValue = NormalizeRolledValue(mod.statTypeAffected, mod.value * multiplier);

            StatModifier rolledModifier = mod.Clone();
            rolledModifier.value = rolledValue;

            result.Add(rolledModifier);
        }

        return result;
    }

    private static float NormalizeRolledValue(Enums.StatType statType, float value)
    {
        switch (statType)
        {
            case Enums.StatType.Speed:
                return Mathf.Round(value * 100f) / 100f;

            default:
                return Mathf.Round(value);
        }
    }
}
