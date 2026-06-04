using CommunityToolkit.Mvvm.ComponentModel;
using ItemTool.Domain.Enums;

namespace ItemTool.Application.DTOs;

public partial class SimulatedLootStackDto : ObservableObject
{
    [ObservableProperty]
    private string itemId = string.Empty;

    [ObservableProperty]
    private string itemName = string.Empty;

    [ObservableProperty]
    private int quantity;

    [ObservableProperty]
    private ItemRarity rolledRarity = ItemRarity.Common;
}