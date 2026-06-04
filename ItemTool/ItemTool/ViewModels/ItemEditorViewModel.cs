using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ItemTool.Application.DTOs;
using ItemTool.Domain.Enums;

namespace ItemTool.App.ViewModels;

public sealed partial class ItemEditorViewModel : ViewModelBase
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
                OnPropertyChanged(nameof(HeaderTitle));
                OnPropertyChanged(nameof(HeaderSubtitle));
                OnPropertyChanged(nameof(HeaderTechnicalInfo));
                OnPropertyChanged(nameof(HeaderBadgeText));
                OnPropertyChanged(nameof(HeaderPreviewText));
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
    
    public string HeaderTitle
    {
        get
        {
            if (SelectedItem == null)
                return "No item selected";

            if (!string.IsNullOrWhiteSpace(SelectedItem.DisplayName))
                return SelectedItem.DisplayName;

            if (!string.IsNullOrWhiteSpace(SelectedItem.ItemNameId))
                return SelectedItem.ItemNameId;

            return "<Unnamed Item>";
        }
    }

    public string HeaderSubtitle
    {
        get
        {
            if (SelectedItem == null)
                return "Select or create an item to start editing.";

            string id = string.IsNullOrWhiteSpace(SelectedItem.ItemNameId)
                ? "No ItemNameId"
                : SelectedItem.ItemNameId;

            return $"{SelectedItem.ItemKind} · {id}";
        }
    }

    public string HeaderTechnicalInfo
    {
        get
        {
            if (SelectedItem == null)
                return string.Empty;

            string id = string.IsNullOrWhiteSpace(SelectedItem.Id)
                ? "No Id"
                : SelectedItem.Id;

            return $"Unity Type: {SelectedItem.ItemType} · Rarity: {SelectedItem.ItemRarity} · Id: {id}";
        }
    }

    public string HeaderBadgeText => SelectedItem?.ItemKind.ToString() ?? "None";

    public string HeaderPreviewText
    {
        get
        {
            if (SelectedItem == null)
                return "?";

            return SelectedItem.ItemKind switch
            {
                ItemKind.Equipment => "E",
                ItemKind.Weapon => "W",
                ItemKind.Consumable => "C",
                ItemKind.Crafting => "M",
                ItemKind.Collectable => "Q",
                _ => "?"
            };
        }
    }

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

        if (e.PropertyName == nameof(ItemDto.DisplayName) ||
            e.PropertyName == nameof(ItemDto.ItemNameId) ||
            e.PropertyName == nameof(ItemDto.Id) ||
            e.PropertyName == nameof(ItemDto.ItemKind) ||
            e.PropertyName == nameof(ItemDto.ItemType) ||
            e.PropertyName == nameof(ItemDto.ItemRarity))
        {
            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderSubtitle));
            OnPropertyChanged(nameof(HeaderTechnicalInfo));
            OnPropertyChanged(nameof(HeaderBadgeText));
            OnPropertyChanged(nameof(HeaderPreviewText));
        }

        ItemChanged?.Invoke();
    }

    private void OnSelectedItemDimensionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        ItemChanged?.Invoke();
    }
    
    [RelayCommand]
    public void AddStatModifier()
    {
        if (SelectedItem?.Equipable == null)
            return;

        SelectedItem.Equipable.Modifiers.Add(new StatModifierDto
        {
            StatType = StatType.Attack,
            Value = 0
        });

        ItemChanged?.Invoke();
    }

    [RelayCommand]
    public void ClearStatModifiers()
    {
        if (SelectedItem?.Equipable == null)
            return;

        SelectedItem.Equipable.Modifiers.Clear();
        ItemChanged?.Invoke();
    }

    [RelayCommand]
    public void AddBuff()
    {
        if (SelectedItem?.Consumable == null)
            return;

        SelectedItem.Consumable.Buffs.Add(new BuffEffectDto
        {
            ApplicationMode = BuffApplicationMode.InstantHeal,
            StatType = StatType.Health,
            Value = 0,
            Duration = 0
        });

        ItemChanged?.Invoke();
    }

    [RelayCommand]
    public void ClearBuffs()
    {
        if (SelectedItem?.Consumable == null)
            return;

        SelectedItem.Consumable.Buffs.Clear();
        ItemChanged?.Invoke();
    }
}