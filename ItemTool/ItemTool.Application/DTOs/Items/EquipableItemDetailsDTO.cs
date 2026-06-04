using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ItemTool.Domain.Enums;

namespace ItemTool.Application.DTOs;

public partial class EquipableItemDetailsDto : ObservableObject
{
    [ObservableProperty]
    private EquipSlot equipSlot = EquipSlot.Backpack;

    [ObservableProperty]
    private int tier;

    [ObservableProperty]
    private EquipableRollMode rollMode = EquipableRollMode.RandomRarityRandomStats;

    [ObservableProperty]
    private string? prefabPath;

    public ObservableCollection<StatModifierDto> Modifiers { get; set; } = new();
}