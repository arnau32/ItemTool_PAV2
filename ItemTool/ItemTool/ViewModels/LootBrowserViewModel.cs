using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ItemTool.Application.Abstractions;
using ItemTool.Application.DTOs;
using ItemTool.Domain.Enums;
using ItemTool.Domain.Validation;

namespace ItemTool.App.ViewModels;

public sealed partial class LootBrowserViewModel : ViewModelBase
{
    private readonly IContentDatabaseRepository _repository;

    private LootTableListItemViewModel? _selectedListItem;

    public LootBrowserViewModel(IContentDatabaseRepository repository)
    {
        _repository = repository;
    }

    public ObservableCollection<LootTableListItemViewModel> LootTables { get; } = new();

    public ObservableCollection<string> AvailableItemIds { get; } = new();

    public ObservableCollection<string> AvailableLootTableIds { get; } = new();

    public Array EntryTypes => Enum.GetValues(typeof(LootEntryType));

    public Array EquipableOverrideModes => Enum.GetValues(typeof(LootEntryEquipableOverrideMode));

    public Array ItemRarities => Enum.GetValues(typeof(ItemRarity));

    public LootTableListItemViewModel? SelectedListItem
    {
        get => _selectedListItem;
        set
        {
            if (SetProperty(ref _selectedListItem, value))
            {
                OnPropertyChanged(nameof(SelectedLootTable));
                OnPropertyChanged(nameof(HasSelectedLootTable));
                NotifyHeaderChanged();
                RefreshValidation();
            }
        }
    }

    public LootTableDto? SelectedLootTable => SelectedListItem?.LootTable;

    public bool HasSelectedLootTable => SelectedLootTable != null;

    public string ValidationSummary { get; private set; } = string.Empty;

    public string HeaderTitle
    {
        get
        {
            if (SelectedLootTable == null)
                return "No loot table selected";

            if (!string.IsNullOrWhiteSpace(SelectedLootTable.Name))
                return SelectedLootTable.Name;

            if (!string.IsNullOrWhiteSpace(SelectedLootTable.Id))
                return SelectedLootTable.Id;

            return "<Unnamed Loot Table>";
        }
    }

    public string HeaderSubtitle
    {
        get
        {
            if (SelectedLootTable == null)
                return "Select or create a loot table to start editing.";

            return $"Loot Table · {SelectedLootTable.Id}";
        }
    }

    public string HeaderTechnicalInfo
    {
        get
        {
            if (SelectedLootTable == null)
                return string.Empty;

            int guaranteedCount = SelectedLootTable.GuaranteedEntries?.Count ?? 0;
            int weightedCount = SelectedLootTable.WeightedEntries?.Count ?? 0;

            return
                $"Guaranteed: {guaranteedCount} · Weighted: {weightedCount} · Picks: {SelectedLootTable.MinRandomPicks}-{SelectedLootTable.MaxRandomPicks}";
        }
    }

    public string HeaderBadgeText => "Loot Table";

    public string HeaderPreviewText => "L";

    [RelayCommand]
    public async Task LoadAsync()
    {
        ContentDatabaseDto database = await _repository.LoadAsync();

        AvailableItemIds.Clear();
        foreach (string itemId in database.Items
                     .Select(x => x.Id)
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x))
        {
            AvailableItemIds.Add(itemId);
        }

        LootTables.Clear();

        foreach (LootTableDto lootTable in database.LootTables)
        {
            EnsureLootTableCollections(lootTable);
            SubscribeToLootTable(lootTable);
            LootTables.Add(new LootTableListItemViewModel(lootTable));
        }

        RefreshAvailableLootTableIds();

        SelectedListItem = LootTables.FirstOrDefault();
        RefreshValidation();
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        ContentDatabaseDto database = await _repository.LoadAsync();
        database.LootTables = LootTables.Select(x => x.LootTable).ToList();

        await _repository.SaveAsync(database);

        foreach (LootTableListItemViewModel lootTableVm in LootTables)
            lootTableVm.MarkAsSaved();
    }

    [RelayCommand]
    public async Task SaveSelectedLootTableAsync()
    {
        if (SelectedListItem == null || SelectedLootTable == null)
            return;

        RefreshValidation();

        List<LootTableDto> allLootTables = LootTables
            .Select(x => x.LootTable)
            .ToList();

        IReadOnlyList<ValidationIssue> selectedIssues = ValidateLootTable(
            SelectedLootTable,
            allLootTables);

        if (selectedIssues.Any(x => x.Severity == ValidationSeverity.Error))
            return;

        ContentDatabaseDto database = await _repository.LoadAsync();

        UpsertLootTable(
            database.LootTables,
            SelectedListItem.SourceId,
            SelectedLootTable);

        await _repository.SaveAsync(database);

        SelectedListItem.MarkAsSaved();
        RefreshAvailableLootTableIds();
        RefreshValidation();
    }

    public bool SelectLootTableById(string? lootTableId)
    {
        if (string.IsNullOrWhiteSpace(lootTableId))
            return false;

        LootTableListItemViewModel? lootTableVm = LootTables.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(x.LootTable.Id) &&
            string.Equals(x.LootTable.Id.Trim(), lootTableId.Trim(), StringComparison.OrdinalIgnoreCase));

        if (lootTableVm == null)
            return false;

        SelectedListItem = lootTableVm;
        return true;
    }

    public IReadOnlyList<ItemLootUsageViewModel> FindUsagesOfItem(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return Array.Empty<ItemLootUsageViewModel>();

        List<ItemLootUsageViewModel> usages = new();

        foreach (LootTableListItemViewModel lootTableVm in LootTables)
        {
            AddItemUsagesFromEntries(
                usages,
                lootTableVm.LootTable.Id,
                lootTableVm.LootTable.Name,
                "Guaranteed",
                lootTableVm.LootTable.GuaranteedEntries,
                itemId);

            AddItemUsagesFromEntries(
                usages,
                lootTableVm.LootTable.Id,
                lootTableVm.LootTable.Name,
                "Weighted",
                lootTableVm.LootTable.WeightedEntries,
                itemId);
        }

        return usages;
    }

    private static void AddItemUsagesFromEntries(
        List<ItemLootUsageViewModel> usages,
        string lootTableId,
        string lootTableName,
        string entryGroup,
        IEnumerable<LootEntryDto> entries,
        string itemId)
    {
        int index = 1;

        foreach (LootEntryDto entry in entries)
        {
            bool isMatchingItemEntry =
                entry.EntryType == LootEntryType.Item &&
                !string.IsNullOrWhiteSpace(entry.ItemId) &&
                string.Equals(entry.ItemId.Trim(), itemId.Trim(), StringComparison.OrdinalIgnoreCase);

            if (isMatchingItemEntry)
            {
                usages.Add(new ItemLootUsageViewModel(
                    lootTableId,
                    lootTableName,
                    entryGroup,
                    index));
            }

            index++;
        }
    }

    [RelayCommand]
    public void NewLootTable()
    {
        LootTableDto lootTable = new()
        {
            Id = GenerateUniqueId("new_loot_table"),
            Name = "New Loot Table",
            MinRandomPicks = 1,
            MaxRandomPicks = 1
        };

        EnsureLootTableCollections(lootTable);
        SubscribeToLootTable(lootTable);

        LootTableListItemViewModel vm = new(lootTable);
        LootTables.Add(vm);

        RefreshAvailableLootTableIds();

        SelectedListItem = vm;
        RefreshValidation();
    }

    [RelayCommand]
    public void AddGuaranteedItemEntry()
    {
        if (SelectedLootTable == null)
            return;

        SelectedLootTable.GuaranteedEntries.Add(CreateItemEntry());
        NotifyLootEntriesChanged();
    }

    [RelayCommand]
    public void AddGuaranteedLootTableEntry()
    {
        if (SelectedLootTable == null)
            return;

        SelectedLootTable.GuaranteedEntries.Add(CreateNestedLootTableEntry());
        NotifyLootEntriesChanged();
    }

    [RelayCommand]
    public void AddWeightedItemEntry()
    {
        if (SelectedLootTable == null)
            return;

        SelectedLootTable.WeightedEntries.Add(CreateItemEntry());
        NotifyLootEntriesChanged();
    }

    [RelayCommand]
    public void AddWeightedLootTableEntry()
    {
        if (SelectedLootTable == null)
            return;

        SelectedLootTable.WeightedEntries.Add(CreateNestedLootTableEntry());
        NotifyLootEntriesChanged();
    }

    [RelayCommand]
    public void RemoveGuaranteedEntry(LootEntryDto? entry)
    {
        if (SelectedLootTable == null || entry == null)
            return;

        SelectedLootTable.GuaranteedEntries.Remove(entry);
        NotifyLootEntriesChanged();
    }

    [RelayCommand]
    public void RemoveWeightedEntry(LootEntryDto? entry)
    {
        if (SelectedLootTable == null || entry == null)
            return;

        SelectedLootTable.WeightedEntries.Remove(entry);
        NotifyLootEntriesChanged();
    }

    [RelayCommand]
    public void ClearGuaranteedEntries()
    {
        if (SelectedLootTable == null)
            return;

        SelectedLootTable.GuaranteedEntries.Clear();
        NotifyLootEntriesChanged();
    }

    [RelayCommand]
    public void ClearWeightedEntries()
    {
        if (SelectedLootTable == null)
            return;

        SelectedLootTable.WeightedEntries.Clear();
        NotifyLootEntriesChanged();
    }

    [RelayCommand]
    public void RefreshValidation()
    {
        List<LootTableDto> allLootTables = LootTables
            .Select(x => x.LootTable)
            .ToList();

        foreach (LootTableListItemViewModel lootTableVm in LootTables)
        {
            IReadOnlyList<ValidationIssue> issues = ValidateLootTable(lootTableVm.LootTable, allLootTables);

            lootTableVm.Severity = issues.Count == 0
                ? ValidationSeverity.None
                : issues.MaxBy(x => x.Severity)?.Severity ?? ValidationSeverity.None;
        }

        if (SelectedLootTable == null)
        {
            ValidationSummary = string.Empty;
            OnPropertyChanged(nameof(ValidationSummary));
            return;
        }

        IReadOnlyList<ValidationIssue> selectedIssues = ValidateLootTable(SelectedLootTable, allLootTables);

        ValidationSummary = selectedIssues.Count == 0
            ? "Sin errores ni warnings."
            : string.Join(Environment.NewLine, selectedIssues.Select(x => $"- [{x.Severity}] {x.Message}"));

        OnPropertyChanged(nameof(ValidationSummary));
    }

    private LootEntryDto CreateItemEntry()
    {
        return new LootEntryDto
        {
            EntryType = LootEntryType.Item,
            ItemId = AvailableItemIds.FirstOrDefault(),
            MinQuantity = 1,
            MaxQuantity = 1,
            Weight = 1,
            EquipableOverrideMode = LootEntryEquipableOverrideMode.None,
            OverrideFixedRarity = ItemRarity.Common
        };
    }

    private LootEntryDto CreateNestedLootTableEntry()
    {
        return new LootEntryDto
        {
            EntryType = LootEntryType.LootTable,
            NestedLootTableId = AvailableLootTableIds
                .FirstOrDefault(x => !string.Equals(x, SelectedLootTable?.Id, StringComparison.OrdinalIgnoreCase)),
            MinQuantity = 1,
            MaxQuantity = 1,
            Weight = 1,
            EquipableOverrideMode = LootEntryEquipableOverrideMode.None,
            OverrideFixedRarity = ItemRarity.Common
        };
    }

    private void NotifyLootEntriesChanged()
    {
        NotifyHeaderChanged();
        RefreshValidation();
    }

    private static IReadOnlyList<ValidationIssue> ValidateLootTable(
        LootTableDto lootTable,
        IReadOnlyList<LootTableDto> allLootTables)
    {
        List<ValidationIssue> issues = new();

        if (string.IsNullOrWhiteSpace(lootTable.Id))
        {
            issues.Add(Error("Loot table Id is required."));
        }
        else
        {
            int duplicatedIds = allLootTables.Count(x =>
                !ReferenceEquals(x, lootTable) &&
                !string.IsNullOrWhiteSpace(x.Id) &&
                string.Equals(x.Id.Trim(), lootTable.Id.Trim(), StringComparison.OrdinalIgnoreCase));

            if (duplicatedIds > 0)
                issues.Add(Error($"Duplicated loot table Id: \"{lootTable.Id}\"."));
        }

        if (string.IsNullOrWhiteSpace(lootTable.Name))
            issues.Add(Warning("Loot table Name is empty."));

        if (lootTable.MinRandomPicks < 0)
            issues.Add(Error("Min Random Picks cannot be negative."));

        if (lootTable.MaxRandomPicks < 0)
            issues.Add(Error("Max Random Picks cannot be negative."));

        if (lootTable.MaxRandomPicks < lootTable.MinRandomPicks)
            issues.Add(Error("Max Random Picks cannot be lower than Min Random Picks."));

        ValidateEntries(
            lootTable,
            allLootTables,
            lootTable.GuaranteedEntries,
            "Guaranteed",
            requiresWeight: false,
            issues);

        ValidateEntries(
            lootTable,
            allLootTables,
            lootTable.WeightedEntries,
            "Weighted",
            requiresWeight: true,
            issues);

        if (lootTable.MaxRandomPicks > 0 && lootTable.WeightedEntries.Count == 0)
            issues.Add(Warning("Random picks are configured, but there are no weighted entries."));

        return issues;
    }

    private static void ValidateEntries(
        LootTableDto owner,
        IReadOnlyList<LootTableDto> allLootTables,
        IEnumerable<LootEntryDto> entries,
        string groupName,
        bool requiresWeight,
        List<ValidationIssue> issues)
    {
        int index = 1;

        foreach (LootEntryDto entry in entries)
        {
            string prefix = $"{groupName} entry #{index}";

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
                    if (string.IsNullOrWhiteSpace(entry.ItemId))
                    {
                        issues.Add(Error($"{prefix}: Item Id is required."));
                    }

                    break;

                case LootEntryType.LootTable:
                    if (string.IsNullOrWhiteSpace(entry.NestedLootTableId))
                    {
                        issues.Add(Error($"{prefix}: Nested Loot Table Id is required."));
                    }
                    else
                    {
                        bool referencesSelf = !string.IsNullOrWhiteSpace(owner.Id) &&
                                              string.Equals(owner.Id.Trim(), entry.NestedLootTableId.Trim(),
                                                  StringComparison.OrdinalIgnoreCase);

                        if (referencesSelf)
                        {
                            issues.Add(Error($"{prefix}: A loot table cannot reference itself."));
                        }

                        bool exists = allLootTables.Any(x =>
                            !string.IsNullOrWhiteSpace(x.Id) &&
                            string.Equals(x.Id.Trim(), entry.NestedLootTableId.Trim(),
                                StringComparison.OrdinalIgnoreCase));

                        if (!exists)
                        {
                            issues.Add(Error(
                                $"{prefix}: Nested Loot Table \"{entry.NestedLootTableId}\" does not exist."));
                        }
                    }

                    break;
            }

            index++;
        }
    }

    private string GenerateUniqueId(string baseId)
    {
        string candidate = baseId;
        int index = 1;

        HashSet<string> existingIds = LootTables
            .Select(x => x.LootTable.Id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        while (existingIds.Contains(candidate))
        {
            candidate = $"{baseId}_{index}";
            index++;
        }

        return candidate;
    }

    private void EnsureLootTableCollections(LootTableDto lootTable)
    {
        lootTable.GuaranteedEntries ??= new ObservableCollection<LootEntryDto>();
        lootTable.WeightedEntries ??= new ObservableCollection<LootEntryDto>();
    }

    private void SubscribeToLootTable(LootTableDto lootTable)
    {
        lootTable.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LootTableDto.Id))
                RefreshAvailableLootTableIds();

            if (SelectedLootTable != lootTable)
                return;

            if (e.PropertyName == nameof(LootTableDto.Name) ||
                e.PropertyName == nameof(LootTableDto.Id) ||
                e.PropertyName == nameof(LootTableDto.MinRandomPicks) ||
                e.PropertyName == nameof(LootTableDto.MaxRandomPicks))
            {
                NotifyHeaderChanged();
                RefreshValidation();
            }
        };
    }

    private void RefreshAvailableLootTableIds()
    {
        AvailableLootTableIds.Clear();

        foreach (string id in LootTables
                     .Select(x => x.LootTable.Id)
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x))
        {
            AvailableLootTableIds.Add(id);
        }
    }

    private void NotifyHeaderChanged()
    {
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderSubtitle));
        OnPropertyChanged(nameof(HeaderTechnicalInfo));
        OnPropertyChanged(nameof(HeaderBadgeText));
        OnPropertyChanged(nameof(HeaderPreviewText));
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

    private static void UpsertLootTable(
        List<LootTableDto> lootTables,
        string? sourceId,
        LootTableDto lootTable)
    {
        int index = FindLootTableIndexById(lootTables, sourceId);

        if (index < 0)
            index = FindLootTableIndexById(lootTables, lootTable.Id);

        if (index >= 0)
            lootTables[index] = lootTable;
        else
            lootTables.Add(lootTable);
    }

    private static int FindLootTableIndexById(
        IReadOnlyList<LootTableDto> lootTables,
        string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return -1;

        string cleanId = id.Trim();

        for (int i = 0; i < lootTables.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(lootTables[i].Id) &&
                string.Equals(lootTables[i].Id.Trim(), cleanId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}