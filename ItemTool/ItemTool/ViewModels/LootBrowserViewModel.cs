using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ItemTool.Application.Abstractions;
using ItemTool.Application.DTOs;

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
        if (SelectedLootTable == null)
        {
            ValidationSummary = string.Empty;
            OnPropertyChanged(nameof(ValidationSummary));
            return;
        }

        List<string> issues = new();

        if (string.IsNullOrWhiteSpace(SelectedLootTable.Id))
            issues.Add("- [Error] Loot table Id is required.");

        if (string.IsNullOrWhiteSpace(SelectedLootTable.Name))
            issues.Add("- [Warning] Loot table Name is empty.");

        if (SelectedLootTable.MinRandomPicks < 0)
            issues.Add("- [Error] Min Random Picks cannot be negative.");

        if (SelectedLootTable.MaxRandomPicks < 0)
            issues.Add("- [Error] Max Random Picks cannot be negative.");

        if (SelectedLootTable.MaxRandomPicks < SelectedLootTable.MinRandomPicks)
            issues.Add("- [Error] Max Random Picks cannot be lower than Min Random Picks.");

        ValidationSummary = issues.Count == 0
            ? "Sin errores ni warnings."
            : string.Join(Environment.NewLine, issues);

        OnPropertyChanged(nameof(ValidationSummary));
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