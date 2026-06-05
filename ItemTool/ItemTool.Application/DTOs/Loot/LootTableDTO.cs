using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ItemTool.Application.DTOs;

public partial class LootTableDto : ObservableObject
{
    [ObservableProperty]
    private string id = string.Empty;

    [ObservableProperty]
    private string sourceAssetPath = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private int minRandomPicks;

    [ObservableProperty]
    private int maxRandomPicks;

    public ObservableCollection<LootEntryDto> GuaranteedEntries { get; set; } = new();

    public ObservableCollection<LootEntryDto> WeightedEntries { get; set; } = new();
}