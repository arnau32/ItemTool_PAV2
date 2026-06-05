using ItemTool.Application.DTOs;
using ItemTool.Domain.Enums;
using ItemTool.Domain.Validation;

namespace ItemTool.Application.Validation;

public sealed class LootTableValidator
{
    public IReadOnlyList<ValidationIssue> Validate(
        LootTableDto lootTable,
        IEnumerable<LootTableDto> allLootTables)
    {
        return Validate(
            lootTable,
            allLootTables,
            Enumerable.Empty<string>());
    }

    public IReadOnlyList<ValidationIssue> Validate(
        LootTableDto lootTable,
        IEnumerable<LootTableDto> allLootTables,
        IEnumerable<string> availableItemIds)
    {
        List<ValidationIssue> issues = new();

        if (lootTable == null)
        {
            issues.Add(Error("Loot table inválida o null."));
            return issues;
        }

        List<LootTableDto> allLootTablesList = allLootTables.ToList();

        HashSet<string> availableItemIdSet = availableItemIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ValidateIdentity(lootTable, allLootTablesList, issues);
        ValidatePicks(lootTable, issues);

        ValidateEntries(
            lootTable,
            allLootTablesList,
            availableItemIdSet,
            lootTable.GuaranteedEntries,
            "Guaranteed",
            requiresWeight: false,
            issues);

        ValidateEntries(
            lootTable,
            allLootTablesList,
            availableItemIdSet,
            lootTable.WeightedEntries,
            "Weighted",
            requiresWeight: true,
            issues);

        if (lootTable.MaxRandomPicks > 0 && lootTable.WeightedEntries.Count == 0)
            issues.Add(Warning("Random picks are configured, but there are no weighted entries."));

        return issues;
    }

    private static void ValidateIdentity(
        LootTableDto lootTable,
        IReadOnlyList<LootTableDto> allLootTables,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(lootTable.Id))
        {
            issues.Add(Error("Loot table Id is required."));
            return;
        }

        int duplicatedIds = allLootTables.Count(x =>
            !ReferenceEquals(x, lootTable) &&
            !string.IsNullOrWhiteSpace(x.Id) &&
            string.Equals(x.Id.Trim(), lootTable.Id.Trim(), StringComparison.OrdinalIgnoreCase));

        if (duplicatedIds > 0)
            issues.Add(Error($"Duplicated loot table Id: \"{lootTable.Id}\"."));

        if (string.IsNullOrWhiteSpace(lootTable.Name))
            issues.Add(Warning("Loot table Name is empty."));
    }

    private static void ValidatePicks(
        LootTableDto lootTable,
        List<ValidationIssue> issues)
    {
        if (lootTable.MinRandomPicks < 0)
            issues.Add(Error("Min Random Picks cannot be negative."));

        if (lootTable.MaxRandomPicks < 0)
            issues.Add(Error("Max Random Picks cannot be negative."));

        if (lootTable.MaxRandomPicks < lootTable.MinRandomPicks)
            issues.Add(Error("Max Random Picks cannot be lower than Min Random Picks."));
    }

    private static void ValidateEntries(
        LootTableDto owner,
        IReadOnlyList<LootTableDto> allLootTables,
        IReadOnlySet<string> availableItemIds,
        IEnumerable<LootEntryDto> entries,
        string groupName,
        bool requiresWeight,
        List<ValidationIssue> issues)
    {
        int index = 1;

        foreach (LootEntryDto entry in entries)
        {
            ValidateEntry(
                owner,
                allLootTables,
                availableItemIds,
                entry,
                $"{groupName} entry #{index}",
                requiresWeight,
                issues);

            index++;
        }
    }

    private static void ValidateEntry(
        LootTableDto owner,
        IReadOnlyList<LootTableDto> allLootTables,
        IReadOnlySet<string> availableItemIds,
        LootEntryDto entry,
        string prefix,
        bool requiresWeight,
        List<ValidationIssue> issues)
    {
        if (entry.MinQuantity < 1)
            issues.Add(Error($"{prefix}: Min Quantity must be at least 1."));

        if (entry.MaxQuantity < 1)
            issues.Add(Error($"{prefix}: Max Quantity must be at least 1."));

        if (entry.MaxQuantity < entry.MinQuantity)
            issues.Add(Error($"{prefix}: Max Quantity cannot be lower than Min Quantity."));

        if (requiresWeight && entry.Weight <= 0)
            issues.Add(Error($"{prefix}: Weight must be greater than 0."));

        switch (entry.EntryType)
        {
            case LootEntryType.Item:
                ValidateItemEntry(entry, availableItemIds, prefix, issues);
                break;

            case LootEntryType.LootTable:
                ValidateNestedLootTableEntry(owner, allLootTables, entry, prefix, issues);
                break;

            default:
                issues.Add(Error($"{prefix}: Unsupported entry type \"{entry.EntryType}\"."));
                break;
        }
    }

    private static void ValidateItemEntry(
        LootEntryDto entry,
        IReadOnlySet<string> availableItemIds,
        string prefix,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(entry.ItemId))
        {
            issues.Add(Error($"{prefix}: Item Id is required."));
            return;
        }

        if (availableItemIds.Count == 0)
        {
            issues.Add(Warning($"{prefix}: Item Id \"{entry.ItemId}\" cannot be checked because no item id list is available."));
            return;
        }

        if (!availableItemIds.Contains(entry.ItemId.Trim()))
            issues.Add(Error($"{prefix}: Item Id \"{entry.ItemId}\" does not exist."));
    }

    private static void ValidateNestedLootTableEntry(
        LootTableDto owner,
        IReadOnlyList<LootTableDto> allLootTables,
        LootEntryDto entry,
        string prefix,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(entry.NestedLootTableId))
        {
            issues.Add(Error($"{prefix}: Nested Loot Table Id is required."));
            return;
        }

        bool referencesSelf =
            !string.IsNullOrWhiteSpace(owner.Id) &&
            string.Equals(
                owner.Id.Trim(),
                entry.NestedLootTableId.Trim(),
                StringComparison.OrdinalIgnoreCase);

        if (referencesSelf)
            issues.Add(Error($"{prefix}: A loot table cannot reference itself."));

        bool exists = allLootTables.Any(x =>
            !string.IsNullOrWhiteSpace(x.Id) &&
            string.Equals(
                x.Id.Trim(),
                entry.NestedLootTableId.Trim(),
                StringComparison.OrdinalIgnoreCase));

        if (!exists)
            issues.Add(Error($"{prefix}: Nested Loot Table \"{entry.NestedLootTableId}\" does not exist."));
    }

    private static ValidationIssue Error(string message)
    {
        return new ValidationIssue
        {
            Severity = ValidationSeverity.Error,
            Message = message
        };
    }

    private static ValidationIssue Warning(string message)
    {
        return new ValidationIssue
        {
            Severity = ValidationSeverity.Warning,
            Message = message
        };
    }
}