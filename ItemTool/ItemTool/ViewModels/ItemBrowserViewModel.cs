using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ItemTool.Application.Abstractions;
using ItemTool.Application.DTOs;
using ItemTool.Application.Validation;
using ItemTool.Domain.Enums;
using ItemTool.Domain.Validation;

namespace ItemTool.App.ViewModels;

public sealed partial class ItemBrowserViewModel : ViewModelBase
{
    private readonly IContentDatabaseRepository _repository;
    private readonly ItemValidator _validator;

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

    public Array ItemTypes => Enum.GetValues(typeof(ItemType));
    public Array ItemRarities => Enum.GetValues(typeof(ItemRarity));

    public ItemBrowserViewModel(IContentDatabaseRepository repository, ItemValidator validator)
    {
        _repository = repository;
        _validator = validator;

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
        ItemDto newItem = new()
        {
            Id = Guid.NewGuid().ToString("N"),
            ItemKind = ItemKind.Equipment,
            ItemNameId = "new_equipment_item",
            DisplayName = "New Equipment Item",
            Description = string.Empty,
            ItemRarity = ItemRarity.Common,
            MaxStack = 1,
            SlotDimension = new DimensionsDto
            {
                Width = 1,
                Height = 1
            }
        };

        newItem.EnsureDetailsForCurrentKind();

        ItemListItemViewModel vm = new(newItem);
        Items.Add(vm);
        SelectedListItem = vm;

        RefreshValidation();
        RefreshDimensionPreview();
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