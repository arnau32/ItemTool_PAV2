using System.ComponentModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using ItemTool.App.Services;
using ItemTool.App.Visuals;
using ItemTool.Application.DTOs;
using ItemTool.Domain.Enums;

namespace ItemTool.App.ViewModels;

public sealed partial class ItemEditorViewModel : ViewModelBase
{
    private readonly IFilePickerService _filePickerService;

    private ItemDto? _selectedItem;
    private DimensionsDto? _subscribedSlotDimension;

    public ItemEditorViewModel(IFilePickerService filePickerService)
    {
        _filePickerService = filePickerService;
    }

    public ItemDto? SelectedItem
    {
        get => _selectedItem;
        set
        {
            UnsubscribeFromSelectedItem();

            if (SetProperty(ref _selectedItem, value))
            {
                SubscribeToSelectedItem();
                NotifySelectedItemChanged();

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

            string source = string.IsNullOrWhiteSpace(SelectedItem.SourceAssetPath)
                ? "No Source Asset"
                : SelectedItem.SourceAssetPath;

            return $"Unity Type: {SelectedItem.ItemType} · Rarity: {SelectedItem.ItemRarity} · Id: {id} · Source: {source}";
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

    public ImageSource? HeaderPreviewImageSource =>
        ItemIconSourceLoader.Load(SelectedItem?.IconPath);

    public Brush HeaderKindBrush => ItemVisualTheme.GetItemKindBrush(SelectedItem?.ItemKind);

    public Brush HeaderRarityBrush => ItemVisualTheme.GetRarityBrush(SelectedItem?.ItemRarity);

    public Brush HeaderRarityPreviewBrush => ItemVisualTheme.GetRarityPreviewBrush(SelectedItem?.ItemRarity);

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

    public void NotifyHeaderPreviewChanged()
    {
        OnPropertyChanged(nameof(HeaderPreviewImageSource));
    }

    private void SubscribeToSelectedItem()
    {
        if (_selectedItem == null)
            return;

        _selectedItem.PropertyChanged += OnSelectedItemPropertyChanged;
        SubscribeToSlotDimension(_selectedItem.SlotDimension);
    }

    private void UnsubscribeFromSelectedItem()
    {
        if (_selectedItem != null)
            _selectedItem.PropertyChanged -= OnSelectedItemPropertyChanged;

        UnsubscribeFromSlotDimension();
    }

    private void SubscribeToSlotDimension(DimensionsDto? slotDimension)
    {
        UnsubscribeFromSlotDimension();

        _subscribedSlotDimension = slotDimension;

        if (_subscribedSlotDimension != null)
            _subscribedSlotDimension.PropertyChanged += OnSelectedItemDimensionsChanged;
    }

    private void UnsubscribeFromSlotDimension()
    {
        if (_subscribedSlotDimension != null)
            _subscribedSlotDimension.PropertyChanged -= OnSelectedItemDimensionsChanged;

        _subscribedSlotDimension = null;
    }

    private void OnSelectedItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemDto.SlotDimension) &&
            sender is ItemDto item)
        {
            SubscribeToSlotDimension(item.SlotDimension);
        }

        if (e.PropertyName == nameof(ItemDto.DisplayName) ||
            e.PropertyName == nameof(ItemDto.ItemNameId) ||
            e.PropertyName == nameof(ItemDto.Id) ||
            e.PropertyName == nameof(ItemDto.SourceAssetPath) ||
            e.PropertyName == nameof(ItemDto.IconPath) ||
            e.PropertyName == nameof(ItemDto.ItemKind) ||
            e.PropertyName == nameof(ItemDto.ItemType) ||
            e.PropertyName == nameof(ItemDto.ItemRarity))
        {
            NotifyHeaderChanged();
        }

        if (e.PropertyName == nameof(ItemDto.ItemKind) ||
            e.PropertyName == nameof(ItemDto.ItemType))
        {
            NotifyTypeInfoChanged();
        }

        ItemChanged?.Invoke();
    }

    private void OnSelectedItemDimensionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        ItemChanged?.Invoke();
    }

    private void NotifySelectedItemChanged()
    {
        OnPropertyChanged(nameof(HasSelectedItem));
        NotifyHeaderChanged();
        NotifyTypeInfoChanged();
    }

    private void NotifyHeaderChanged()
    {
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderSubtitle));
        OnPropertyChanged(nameof(HeaderTechnicalInfo));
        OnPropertyChanged(nameof(HeaderBadgeText));
        OnPropertyChanged(nameof(HeaderPreviewText));
        OnPropertyChanged(nameof(HeaderPreviewImageSource));
        OnPropertyChanged(nameof(HeaderKindBrush));
        OnPropertyChanged(nameof(HeaderRarityBrush));
        OnPropertyChanged(nameof(HeaderRarityPreviewBrush));
    }

    private void NotifyTypeInfoChanged()
    {
        OnPropertyChanged(nameof(ItemKindText));
        OnPropertyChanged(nameof(ItemTypeText));
        OnPropertyChanged(nameof(ShowsEquipableSection));
        OnPropertyChanged(nameof(ShowsWeaponSection));
        OnPropertyChanged(nameof(ShowsConsumableSection));
        OnPropertyChanged(nameof(ShowsCollectableSection));
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
    public void RemoveStatModifier(StatModifierDto? modifier)
    {
        if (SelectedItem?.Equipable == null || modifier == null)
            return;

        SelectedItem.Equipable.Modifiers.Remove(modifier);
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
    public void RemoveBuff(BuffEffectDto? buff)
    {
        if (SelectedItem?.Consumable == null || buff == null)
            return;

        SelectedItem.Consumable.Buffs.Remove(buff);
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

    [RelayCommand]
    public void BrowseIconPath()
    {
        if (SelectedItem == null)
            return;

        string? selectedPath = _filePickerService.PickImageFile(SelectedItem.IconPath);

        if (string.IsNullOrWhiteSpace(selectedPath))
            return;

        SelectedItem.IconPath = UnityAssetPathUtility.ToUnityAssetPathIfPossible(selectedPath);
        ItemChanged?.Invoke();
    }
}