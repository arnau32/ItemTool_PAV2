using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using ItemTool.Domain.Enums;

namespace ItemTool.Application.DTOs;

public partial class ItemDto : ObservableObject
{
    [ObservableProperty]
    private string id = string.Empty;

    [ObservableProperty]
    private string sourceAssetPath = string.Empty;

    [ObservableProperty]
    private ItemKind itemKind = ItemKind.Equipment;

    [ObservableProperty]
    private ItemType itemType = ItemType.Equipable;

    [ObservableProperty]
    private string itemNameId = string.Empty;

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private string? iconPath;

    [ObservableProperty]
    private ItemRarity itemRarity = ItemRarity.Common;

    [ObservableProperty]
    private float value;

    [ObservableProperty]
    private float weight;

    [ObservableProperty]
    private int maxStack = 1;

    [ObservableProperty]
    private DimensionsDto slotDimension = new();

    [ObservableProperty]
    private EquipableItemDetailsDto? equipable;

    [ObservableProperty]
    private WeaponItemDetailsDto? weapon;

    [ObservableProperty]
    private ConsumableItemDetailsDto? consumable;

    [ObservableProperty]
    private CollectableItemDetailsDto? collectable;

    /// <summary>
    /// Alias temporal para no romper el ItemBrowserViewModel, ItemValidator y bindings antiguos.
    /// Más adelante migraremos todo a ItemNameId y eliminaremos esta propiedad.
    /// </summary>
    [JsonIgnore]
    public string ItemName
    {
        get => ItemNameId;
        set => ItemNameId = value;
    }

    partial void OnItemKindChanged(ItemKind value)
    {
        ItemType = GetItemTypeFromKind(value);
        EnsureDetailsForKind(value);
    }

    partial void OnItemNameIdChanged(string value)
    {
        OnPropertyChanged(nameof(ItemName));

        if (string.IsNullOrWhiteSpace(Id))
            Id = value;
    }

    public void EnsureDetailsForCurrentKind()
    {
        ItemType = GetItemTypeFromKind(ItemKind);
        EnsureDetailsForKind(ItemKind);
    }

    public static ItemType GetItemTypeFromKind(ItemKind kind)
    {
        return kind switch
        {
            ItemKind.Equipment => ItemType.Equipable,
            ItemKind.Weapon => ItemType.Equipable,
            ItemKind.Consumable => ItemType.Consumable,
            ItemKind.Crafting => ItemType.Crafting,
            ItemKind.Collectable => ItemType.Collectable,
            _ => ItemType.Equipable
        };
    }

    private void EnsureDetailsForKind(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.Equipment:
                Equipable ??= new EquipableItemDetailsDto();
                Weapon = null;
                Consumable = null;
                Collectable = null;
                break;

            case ItemKind.Weapon:
                Equipable ??= new EquipableItemDetailsDto
                {
                    EquipSlot = EquipSlot.Weapon
                };

                Weapon ??= new WeaponItemDetailsDto();
                Consumable = null;
                Collectable = null;
                break;

            case ItemKind.Consumable:
                Equipable = null;
                Weapon = null;
                Consumable ??= new ConsumableItemDetailsDto();
                Collectable = null;
                break;

            case ItemKind.Crafting:
                Equipable = null;
                Weapon = null;
                Consumable = null;
                Collectable = null;
                break;

            case ItemKind.Collectable:
                Equipable = null;
                Weapon = null;
                Consumable = null;
                Collectable ??= new CollectableItemDetailsDto();
                break;
        }
    }
}