using CommunityToolkit.Mvvm.ComponentModel;
using ItemTool.Domain.Enums;

namespace ItemTool.Application.DTOs;

public partial class StatModifierDto : ObservableObject
{
    [ObservableProperty]
    private StatType statType = StatType.Attack;

    [ObservableProperty]
    private float value;
}