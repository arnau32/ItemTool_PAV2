using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ItemTool.Application.Abstractions;
using ItemTool.Application.Services;
using ItemTool.Application.Validation;

namespace ItemTool.App.ViewModels;

public sealed partial class MainToolViewModel : ViewModelBase
{
    private ToolWorkspace _activeWorkspace = ToolWorkspace.Items;

    public MainToolViewModel(
        IContentDatabaseRepository repository,
        ItemValidator itemValidator,
        ItemFactory itemFactory)
    {
        Items = new ItemBrowserViewModel(
            repository,
            itemValidator,
            itemFactory);

        LootTables = new LootBrowserViewModel(repository);

        Items.SelectedItemChanged += RefreshSelectedItemUsages;
    }

    public ItemBrowserViewModel Items { get; }

    public LootBrowserViewModel LootTables { get; }

    public ObservableCollection<ItemLootUsageViewModel> SelectedItemLootUsages { get; } = new();

    public string SelectedItemUsageSummary
    {
        get
        {
            if (Items.SelectedListItem == null)
                return "No item selected.";

            if (LootTables.LootTables.Count == 0)
                return "Load loot tables or press Refresh to search references.";

            if (SelectedItemLootUsages.Count == 0)
                return "No direct loot table references found.";

            return $"{SelectedItemLootUsages.Count} direct loot table reference(s) found.";
        }
    }

    public ToolWorkspace ActiveWorkspace
    {
        get => _activeWorkspace;
        private set
        {
            if (SetProperty(ref _activeWorkspace, value))
            {
                OnPropertyChanged(nameof(IsItemsWorkspaceActive));
                OnPropertyChanged(nameof(IsLootTablesWorkspaceActive));
            }
        }
    }

    public bool IsItemsWorkspaceActive => ActiveWorkspace == ToolWorkspace.Items;

    public bool IsLootTablesWorkspaceActive => ActiveWorkspace == ToolWorkspace.LootTables;

    [RelayCommand]
    public void ShowItemsWorkspace()
    {
        ActiveWorkspace = ToolWorkspace.Items;
    }

    [RelayCommand]
    public void ShowLootTablesWorkspace()
    {
        ActiveWorkspace = ToolWorkspace.LootTables;
    }

    [RelayCommand]
    public async Task NavigateToItemAsync(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        if (Items.Items.Count == 0)
            await Items.LoadAsync();

        bool selected = Items.SelectItemById(itemId);

        if (selected)
            ActiveWorkspace = ToolWorkspace.Items;
    }

    [RelayCommand]
    public async Task NavigateToLootTableAsync(string? lootTableId)
    {
        if (string.IsNullOrWhiteSpace(lootTableId))
            return;

        if (LootTables.LootTables.Count == 0)
            await LootTables.LoadAsync();

        bool selected = LootTables.SelectLootTableById(lootTableId);

        if (selected)
            ActiveWorkspace = ToolWorkspace.LootTables;
    }

    [RelayCommand]
    public async Task RefreshSelectedItemUsagesAsync()
    {
        if (Items.SelectedListItem == null)
        {
            SelectedItemLootUsages.Clear();
            OnPropertyChanged(nameof(SelectedItemUsageSummary));
            return;
        }

        if (LootTables.LootTables.Count == 0)
            await LootTables.LoadAsync();

        RefreshSelectedItemUsages();
    }

    private void RefreshSelectedItemUsages()
    {
        SelectedItemLootUsages.Clear();

        string? itemId = Items.SelectedItemId;

        if (!string.IsNullOrWhiteSpace(itemId) &&
            LootTables.LootTables.Count > 0)
        {
            foreach (ItemLootUsageViewModel usage in LootTables.FindUsagesOfItem(itemId))
                SelectedItemLootUsages.Add(usage);
        }

        OnPropertyChanged(nameof(SelectedItemUsageSummary));
    }
}