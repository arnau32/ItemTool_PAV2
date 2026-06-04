using CommunityToolkit.Mvvm.ComponentModel;
using ItemTool.Domain.Enums;

namespace ItemTool.Application.DTOs;

public partial class LootSimulationRowDto : ObservableObject
{
    [ObservableProperty]
    private string itemId = string.Empty;

    [ObservableProperty]
    private string itemName = string.Empty;

    [ObservableProperty]
    private int appearsInRolls;

    [ObservableProperty]
    private int totalQuantity;

    [ObservableProperty]
    private float appearancePercent;

    [ObservableProperty]
    private float averageQuantityPerRoll;

    public Dictionary<ItemRarity, int> RarityCounts { get; set; } = new();
}