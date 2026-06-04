using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ItemTool.Application.DTOs;

public partial class LootSimulationResultDto : ObservableObject
{
    [ObservableProperty]
    private int iterations;

    [ObservableProperty]
    private int totalGeneratedStacks;

    [ObservableProperty]
    private int totalGeneratedQuantity;

    public ObservableCollection<LootSimulationRowDto> Rows { get; set; } = new();

    public ObservableCollection<SimulatedLootStackDto> LastRoll { get; set; } = new();
}