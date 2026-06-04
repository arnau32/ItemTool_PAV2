using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ItemTool.Application.Abstractions;
using ItemTool.Application.DTOs;
using ItemTool.Application.Services;
using ItemTool.Application.Validation;
using ItemTool.Domain.Enums;
using ItemTool.Domain.Validation;

namespace ItemTool.App.ViewModels;

public sealed partial class ItemBrowserViewModel : ViewModelBase
{
    private readonly IContentDatabaseRepository _repository;
    private readonly ItemValidator _validator;
    private readonly ItemFactory _itemFactory;

    private ContentDatabaseDto _database = new();

    public ObservableCollection<ItemListItemViewModel> Items { get; } = new();

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
                RefreshDimensionPreview();
            }
        }
    }

    private string _validationSummary = string.Empty;
    public string ValidationSummary
    {
        get => _validationSummary;
        set => SetProperty(ref _validationSummary, value);
    }

    private string _dimensionPreviewText = "No item selected";
    public string DimensionPreviewText
    {
        get => _dimensionPreviewText;
        set => SetProperty(ref _dimensionPreviewText, value);
    }

    public ItemEditorViewModel Editor { get; } = new();

    public Array ItemKinds => Enum.GetValues(typeof(ItemKind));
    public Array ItemTypes => Enum.GetValues(typeof(ItemType));
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

        Editor.ItemChanged += OnEditorItemChanged;
    }

    private void OnEditorItemChanged()
    {
        RefreshValidation();
        RefreshDimensionPreview();
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

        if (Items.Count > 0)
            SelectedListItem = Items[0];
        else
        {
            SelectedListItem = null;
            RefreshValidation();
            RefreshDimensionPreview();
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        _database.Items = Items.Select(x => x.Item).ToList();

        await _repository.SaveAsync(_database);
    }

    [RelayCommand]
    public void NewItem()
    {
        CreateItem(ItemKind.Equipment);
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

    private void CreateItem(ItemKind kind)
    {
        ItemDto newItem = _itemFactory.Create(kind);

        MakeItemIdUnique(newItem);

        ItemListItemViewModel vm = new(newItem);
        Items.Add(vm);
        SelectedListItem = vm;

        RefreshValidation();
        RefreshDimensionPreview();
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

    private void RefreshDimensionPreview()
    {
        if (SelectedListItem?.Item?.SlotDimension == null)
        {
            DimensionPreviewText = "No item selected";
            return;
        }

        int width = Math.Max(1, SelectedListItem.Item.SlotDimension.Width);
        int height = Math.Max(1, SelectedListItem.Item.SlotDimension.Height);

        List<string> rows = new();

        for (int y = 0; y < height; y++)
        {
            rows.Add(string.Join(" ", Enumerable.Repeat("■", width)));
        }

        DimensionPreviewText = $"{width}x{height}{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, rows)}";
    }
}