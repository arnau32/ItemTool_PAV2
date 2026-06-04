using CommunityToolkit.Mvvm.ComponentModel;
using ItemTool.Domain.Enums;

namespace ItemTool.Application.DTOs;

public partial class LootEntryDto : ObservableObject
{
    [ObservableProperty]
    private string id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private LootEntryType entryType = LootEntryType.Item;

    [ObservableProperty]
    private string? itemId;

    [ObservableProperty]
    private string? nestedLootTableId;

    [ObservableProperty]
    private int minQuantity = 1;

    [ObservableProperty]
    private int maxQuantity = 1;

    [ObservableProperty]
    private int weight = 1;

    [ObservableProperty]
    private LootEntryEquipableOverrideMode equipableOverrideMode =
        LootEntryEquipableOverrideMode.None;

    [ObservableProperty]
    private ItemRarity overrideFixedRarity = ItemRarity.Common;

    partial void OnEntryTypeChanged(LootEntryType value)
    {
        switch (value)
        {
            case LootEntryType.Item:
                NestedLootTableId = null;
                break;

            case LootEntryType.LootTable:
                ItemId = null;
                EquipableOverrideMode = LootEntryEquipableOverrideMode.None;
                OverrideFixedRarity = ItemRarity.Common;
                break;
        }
    }
}