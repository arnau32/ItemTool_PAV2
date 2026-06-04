using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ItemTool.Application.DTOs;

public partial class ConsumableItemDetailsDto : ObservableObject
{
    public ObservableCollection<BuffEffectDto> Buffs { get; set; } = new();
}