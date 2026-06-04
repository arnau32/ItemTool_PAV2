using CommunityToolkit.Mvvm.ComponentModel;
using ItemTool.Domain.Enums;

namespace ItemTool.Application.DTOs;

public partial class ItemDto : ObservableObject
{
    [ObservableProperty]
    private string id = string.Empty;

    [ObservableProperty]
    private ItemType itemType;

    [ObservableProperty]
    private string itemName = string.Empty;

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
}