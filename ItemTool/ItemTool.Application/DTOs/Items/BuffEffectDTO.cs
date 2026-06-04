using CommunityToolkit.Mvvm.ComponentModel;
using ItemTool.Domain.Enums;

namespace ItemTool.Application.DTOs;

public partial class BuffEffectDto : ObservableObject
{
    [ObservableProperty]
    private BuffApplicationMode applicationMode = BuffApplicationMode.InstantHeal;

    [ObservableProperty]
    private StatType statType = StatType.Health;

    [ObservableProperty]
    private float value;

    [ObservableProperty]
    private float duration;
}