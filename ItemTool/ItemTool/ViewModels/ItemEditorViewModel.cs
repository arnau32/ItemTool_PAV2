using System.ComponentModel;
using ItemTool.Application.DTOs;
using ItemTool.Domain.Enums;

namespace ItemTool.App.ViewModels;

public sealed class ItemEditorViewModel : ViewModelBase
{
    private ItemDto? _selectedItem;

    public ItemDto? SelectedItem
    {
        get => _selectedItem;
        set
        {
            UnsubscribeFromSelectedItem();

            if (SetProperty(ref _selectedItem, value))
            {
                SubscribeToSelectedItem();

                OnPropertyChanged(nameof(HasSelectedItem));
                OnPropertyChanged(nameof(ItemKindText));
                OnPropertyChanged(nameof(ItemTypeText));
                OnPropertyChanged(nameof(ShowsEquipableSection));
                OnPropertyChanged(nameof(ShowsWeaponSection));
                OnPropertyChanged(nameof(ShowsConsumableSection));
                OnPropertyChanged(nameof(ShowsCollectableSection));

                ItemChanged?.Invoke();
            }
        }
    }

    public bool HasSelectedItem => SelectedItem != null;

    public string ItemKindText => SelectedItem?.ItemKind.ToString() ?? string.Empty;

    public string ItemTypeText => SelectedItem?.ItemType.ToString() ?? string.Empty;

    public bool ShowsEquipableSection =>
        SelectedItem?.ItemKind is ItemKind.Equipment or ItemKind.Weapon;

    public bool ShowsWeaponSection =>
        SelectedItem?.ItemKind == ItemKind.Weapon;

    public bool ShowsConsumableSection =>
        SelectedItem?.ItemKind == ItemKind.Consumable;

    public bool ShowsCollectableSection =>
        SelectedItem?.ItemKind == ItemKind.Collectable;

    public event Action? ItemChanged;

    private void SubscribeToSelectedItem()
    {
        if (_selectedItem == null)
            return;

        _selectedItem.PropertyChanged += OnSelectedItemPropertyChanged;

        if (_selectedItem.SlotDimension != null)
            _selectedItem.SlotDimension.PropertyChanged += OnSelectedItemDimensionsChanged;
    }

    private void UnsubscribeFromSelectedItem()
    {
        if (_selectedItem == null)
            return;

        _selectedItem.PropertyChanged -= OnSelectedItemPropertyChanged;

        if (_selectedItem.SlotDimension != null)
            _selectedItem.SlotDimension.PropertyChanged -= OnSelectedItemDimensionsChanged;
    }

    private void OnSelectedItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemDto.SlotDimension))
        {
            if (sender is ItemDto item)
            {
                if (item.SlotDimension != null)
                    item.SlotDimension.PropertyChanged += OnSelectedItemDimensionsChanged;
            }
        }

        if (e.PropertyName == nameof(ItemDto.ItemKind) ||
            e.PropertyName == nameof(ItemDto.ItemType))
        {
            OnPropertyChanged(nameof(ItemKindText));
            OnPropertyChanged(nameof(ItemTypeText));
            OnPropertyChanged(nameof(ShowsEquipableSection));
            OnPropertyChanged(nameof(ShowsWeaponSection));
            OnPropertyChanged(nameof(ShowsConsumableSection));
            OnPropertyChanged(nameof(ShowsCollectableSection));
        }

        ItemChanged?.Invoke();
    }

    private void OnSelectedItemDimensionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        ItemChanged?.Invoke();
    }
}