using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ItemTool.Application.Abstractions;
using ItemTool.Application.DTOs;
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

            return $"Guaranteed: {guaranteedCount} · Weighted: {weightedCount} · Picks: {SelectedLootTable.MinRandomPicks}-{SelectedLootTable.MaxRandomPicks}";
        }
    }

    public string HeaderBadgeText => "Loot Table";

    public string HeaderPreviewText => "L";

    [RelayCommand]
    public async Task LoadAsync()
    {
        ContentDatabaseDto database = await _repository.LoadAsync();

        LootTables.Clear();

        foreach (LootTableDto lootTable in database.LootTables)
        {
            EnsureLootTableCollections(lootTable);
            SubscribeToLootTable(lootTable);
            LootTables.Add(new LootTableListItemViewModel(lootTable));
        }

        SelectedListItem = LootTables.FirstOrDefault();
        RefreshValidation();
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        ContentDatabaseDto database = await _repository.LoadAsync();
        database.LootTables = LootTables.Select(x => x.LootTable).ToList();

        await _repository.SaveAsync(database);
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
        SelectedListItem = vm;

        RefreshValidation();
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
        {
            issues.Add(Warning("Loot table Name is empty."));
        }

        if (lootTable.MinRandomPicks < 0)
        {
            issues.Add(Error("Min Random Picks cannot be negative."));
        }

        if (lootTable.MaxRandomPicks < 0)
        {
            issues.Add(Error("Max Random Picks cannot be negative."));
        }

        if (lootTable.MaxRandomPicks < lootTable.MinRandomPicks)
        {
            issues.Add(Error("Max Random Picks cannot be lower than Min Random Picks."));
        }

        return issues;
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

    private void NotifyHeaderChanged()
    {
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderSubtitle));
        OnPropertyChanged(nameof(HeaderTechnicalInfo));
        OnPropertyChanged(nameof(HeaderBadgeText));
        OnPropertyChanged(nameof(HeaderPreviewText));
    }
}