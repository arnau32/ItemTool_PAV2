using CommunityToolkit.Mvvm.ComponentModel;

namespace ItemTool.Application.DTOs;

public partial class CollectableItemDetailsDto : ObservableObject
{
    [ObservableProperty]
    private int collectionId;

    [ObservableProperty]
    private bool isAuroraDust;
}