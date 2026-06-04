using CommunityToolkit.Mvvm.ComponentModel;

namespace ItemTool.Application.DTOs;

public partial class DimensionsDto : ObservableObject
{
    [ObservableProperty]
    private int width = 1;

    [ObservableProperty]
    private int height = 1;
}