using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.Input;
using ItemTool.Application.Abstractions;
using ItemTool.Application.DTOs;
using ItemTool.Application.Services;
using ItemTool.Application.Validation;
using ItemTool.Domain.Enums;
using ItemTool.Domain.Validation;
using System.Windows.Media;
using ItemTool.App.Visuals;

namespace ItemTool.App.ViewModels;

public sealed partial class ItemBrowserViewModel : ViewModelBase
{
    private readonly IContentDatabaseRepository _repository;
    private readonly ItemValidator _validator;
    private readonly ItemFactory _itemFactory;

    private ContentDatabaseDto _database = new();
    private ItemKind? _activeItemKindFilter;

    public ObservableCollection<ItemListItemViewModel> Items { get; } = new();

    public ICollectionView FilteredItems { get; }

    private ItemListItemViewModel? _selectedListItem;

    public ItemListItemViewModel? SelectedListItem
    {
        get => _selectedListItem;
        set
        {
            if (SetProperty(ref _selectedListItem, value))
            {
                Editor.SelectedItem = value?.Item;
                RefreshValidation();
            }
        }
    }

    private string _validationSummary = string.Empty;

    public string ValidationSummary
    {
        get => _validationSummary;
        set => SetProperty(ref _validationSummary, value);
    }

    public string ActiveFilterText => _activeItemKindFilter?.ToString() ?? "All";

    public bool IsAllFilterActive => _activeItemKindFilter == null;

    public bool IsEquipmentFilterActive => _activeItemKindFilter == ItemKind.Equipment;

    public bool IsWeaponFilterActive => _activeItemKindFilter == ItemKind.Weapon;

    public bool IsConsumableFilterActive => _activeItemKindFilter == ItemKind.Consumable;

    public bool IsCraftingFilterActive => _activeItemKindFilter == ItemKind.Crafting;

    public bool IsCollectableFilterActive => _activeItemKindFilter == ItemKind.Collectable;

    public Brush AllFilterBrush => ItemVisualTheme.GetItemKindBrush(null);

    public Brush EquipmentFilterBrush => ItemVisualTheme.GetItemKindBrush(ItemKind.Equipment);

    public Brush WeaponFilterBrush => ItemVisualTheme.GetItemKindBrush(ItemKind.Weapon);

    public Brush ConsumableFilterBrush => ItemVisualTheme.GetItemKindBrush(ItemKind.Consumable);

    public Brush CraftingFilterBrush => ItemVisualTheme.GetItemKindBrush(ItemKind.Crafting);

    public Brush CollectableFilterBrush => ItemVisualTheme.GetItemKindBrush(ItemKind.Collectable);

    public ItemEditorViewModel Editor { get; } = new();

    public Array ItemKinds => Enum.GetValues(typeof(ItemKind));
    public Array ItemRarities => Enum.GetValues(typeof(ItemRarity));

    public Array EquipSlots => Enum.GetValues(typeof(EquipSlot));
    public Array EquipableRollModes => Enum.GetValues(typeof(EquipableRollMode));
    public Array WeaponHandTypes => Enum.GetValues(typeof(WeaponHandType));
    public Array WeaponFamilies => Enum.GetValues(typeof(WeaponFamily));
    public Array StatTypes => Enum.GetValues(typeof(StatType));
    public Array BuffApplicationModes => Enum.GetValues(typeof(BuffApplicationMode));

    public ItemBrowserViewModel(
        IContentDatabaseRepository repository,
        ItemValidator validator,
        ItemFactory itemFactory)
    {
        _repository = repository;
        _validator = validator;
        _itemFactory = itemFactory;

        FilteredItems = CollectionViewSource.GetDefaultView(Items);
        FilteredItems.Filter = FilterItem;

        Editor.ItemChanged += OnEditorItemChanged;
    }

    private void OnEditorItemChanged()
    {
        RefreshValidation();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        _database = await _repository.LoadAsync();

        Items.Clear();

        foreach (ItemDto item in _database.Items)
        {
            item.EnsureDetailsForCurrentKind();
            Items.Add(new ItemListItemViewModel(item));
        }

        FilteredItems.Refresh();

        SelectedListItem = FilteredItems
            .Cast<ItemListItemViewModel>()
            .FirstOrDefault();

        if (SelectedListItem == null)
        {
            RefreshValidation();
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        _database.Items = Items.Select(x => x.Item).ToList();

        await _repository.SaveAsync(_database);

        foreach (ItemListItemViewModel itemVm in Items)
            itemVm.MarkAsSaved();
    }

    [RelayCommand]
    public async Task SaveSelectedItemAsync()
    {
        if (SelectedListItem == null)
            return;

        RefreshValidation();

        List<ItemDto> allItems = Items.Select(x => x.Item).ToList();
        IReadOnlyList<ValidationIssue> selectedIssues = _validator.Validate(SelectedListItem.Item, allItems);

        if (selectedIssues.Any(x => x.Severity == ValidationSeverity.Error))
            return;

        ContentDatabaseDto database = await _repository.LoadAsync();

        UpsertItem(
            database.Items,
            SelectedListItem.SourceId,
            SelectedListItem.Item);

        await _repository.SaveAsync(database);

        SelectedListItem.MarkAsSaved();
        RefreshValidation();
    }

    [RelayCommand]
    public void NewItem()
    {
        CreateItem(_activeItemKindFilter ?? ItemKind.Equipment);
    }

    [RelayCommand]
    public void NewEquipmentItem()
    {
        CreateItem(ItemKind.Equipment);
    }

    [RelayCommand]
    public void NewWeaponItem()
    {
        CreateItem(ItemKind.Weapon);
    }

    [RelayCommand]
    public void NewConsumableItem()
    {
        CreateItem(ItemKind.Consumable);
    }

    [RelayCommand]
    public void NewCraftingItem()
    {
        CreateItem(ItemKind.Crafting);
    }

    [RelayCommand]
    public void NewCollectableItem()
    {
        CreateItem(ItemKind.Collectable);
    }

    [RelayCommand]
    public void ShowAllItems()
    {
        SetItemKindFilter(null);
    }

    [RelayCommand]
    public void ShowEquipmentItems()
    {
        SetItemKindFilter(ItemKind.Equipment);
    }

    [RelayCommand]
    public void ShowWeaponItems()
    {
        SetItemKindFilter(ItemKind.Weapon);
    }

    [RelayCommand]
    public void ShowConsumableItems()
    {
        SetItemKindFilter(ItemKind.Consumable);
    }

    [RelayCommand]
    public void ShowCraftingItems()
    {
        SetItemKindFilter(ItemKind.Crafting);
    }

    [RelayCommand]
    public void ShowCollectableItems()
    {
        SetItemKindFilter(ItemKind.Collectable);
    }

    [RelayCommand]
    public void RefreshValidation()
    {
        List<ItemDto> allItems = Items.Select(x => x.Item).ToList();

        foreach (ItemListItemViewModel itemVm in Items)
        {
            IReadOnlyList<ValidationIssue> issues = _validator.Validate(itemVm.Item, allItems);
            itemVm.Severity = issues.Count == 0
                ? ValidationSeverity.None
                : issues.MaxBy(x => x.Severity)?.Severity ?? ValidationSeverity.None;
        }

        if (SelectedListItem == null)
        {
            ValidationSummary = string.Empty;
            return;
        }

        IReadOnlyList<ValidationIssue> selectedIssues = _validator.Validate(SelectedListItem.Item, allItems);
        ValidationSummary = selectedIssues.Count == 0
            ? "Sin errores ni warnings."
            : string.Join(Environment.NewLine, selectedIssues.Select(x => $"- [{x.Severity}] {x.Message}"));
    }
    
    public bool SelectItemById(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        ItemListItemViewModel? itemVm = Items.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(x.Item.Id) &&
            string.Equals(x.Item.Id.Trim(), itemId.Trim(), StringComparison.OrdinalIgnoreCase));

        if (itemVm == null)
            return false;

        if (_activeItemKindFilter != itemVm.Item.ItemKind)
            SetItemKindFilter(itemVm.Item.ItemKind);
        else
            FilteredItems.Refresh();

        SelectedListItem = itemVm;
        return true;
    }

    private void CreateItem(ItemKind kind)
    {
        ItemDto newItem = _itemFactory.Create(kind);

        MakeItemIdUnique(newItem);

        ItemListItemViewModel vm = new(newItem);
        Items.Add(vm);

        if (_activeItemKindFilter != kind)
            SetItemKindFilter(kind);
        else
            FilteredItems.Refresh();

        SelectedListItem = vm;

        RefreshValidation();
    }

    private void NotifyFilterStateChanged()
    {
        OnPropertyChanged(nameof(ActiveFilterText));

        OnPropertyChanged(nameof(IsAllFilterActive));
        OnPropertyChanged(nameof(IsEquipmentFilterActive));
        OnPropertyChanged(nameof(IsWeaponFilterActive));
        OnPropertyChanged(nameof(IsConsumableFilterActive));
        OnPropertyChanged(nameof(IsCraftingFilterActive));
        OnPropertyChanged(nameof(IsCollectableFilterActive));
    }

    private void SetItemKindFilter(ItemKind? kind)
    {
        _activeItemKindFilter = kind;

        NotifyFilterStateChanged();

        FilteredItems.Refresh();
        EnsureSelectedItemIsVisible();
    }

    private bool FilterItem(object item)
    {
        if (item is not ItemListItemViewModel itemVm)
            return false;

        return _activeItemKindFilter == null ||
               itemVm.Item.ItemKind == _activeItemKindFilter.Value;
    }

    private void EnsureSelectedItemIsVisible()
    {
        if (SelectedListItem != null && FilterItem(SelectedListItem))
            return;

        SelectedListItem = FilteredItems
            .Cast<ItemListItemViewModel>()
            .FirstOrDefault();

        if (SelectedListItem == null)
        {
            RefreshValidation();
        }
    }

    private void MakeItemIdUnique(ItemDto item)
    {
        string baseId = string.IsNullOrWhiteSpace(item.Id)
            ? "new_item"
            : item.Id.Trim();

        string candidate = baseId;
        int index = 1;

        HashSet<string> existingIds = Items
            .Select(x => x.Item.Id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        while (existingIds.Contains(candidate))
        {
            candidate = $"{baseId}_{index}";
            index++;
        }

        item.Id = candidate;
        item.ItemNameId = candidate;
    }
    
    private static void UpsertItem(
        List<ItemDto> items,
        string? sourceId,
        ItemDto item)
    {
        int index = FindItemIndexById(items, sourceId);

        if (index < 0)
            index = FindItemIndexById(items, item.Id);

        if (index >= 0)
            items[index] = item;
        else
            items.Add(item);
    }

    private static int FindItemIndexById(
        IReadOnlyList<ItemDto> items,
        string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return -1;

        string cleanId = id.Trim();

        for (int i = 0; i < items.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(items[i].Id) &&
                string.Equals(items[i].Id.Trim(), cleanId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}