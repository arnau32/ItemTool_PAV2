using CommunityToolkit.Mvvm.Input;
using ItemTool.Application.Abstractions;
using ItemTool.Application.Services;
using ItemTool.Application.Validation;

namespace ItemTool.App.ViewModels;

public sealed partial class MainToolViewModel : ViewModelBase
{
    private ToolWorkspace _activeWorkspace = ToolWorkspace.Items;

    public MainToolViewModel(
        IContentDatabaseRepository repository,
        ItemValidator itemValidator,
        ItemFactory itemFactory)
    {
        Items = new ItemBrowserViewModel(
            repository,
            itemValidator,
            itemFactory);

        LootTables = new LootBrowserViewModel();
    }

    public ItemBrowserViewModel Items { get; }

    public LootBrowserViewModel LootTables { get; }

    public ToolWorkspace ActiveWorkspace
    {
        get => _activeWorkspace;
        private set
        {
            if (SetProperty(ref _activeWorkspace, value))
            {
                OnPropertyChanged(nameof(IsItemsWorkspaceActive));
                OnPropertyChanged(nameof(IsLootTablesWorkspaceActive));
            }
        }
    }

    public bool IsItemsWorkspaceActive => ActiveWorkspace == ToolWorkspace.Items;

    public bool IsLootTablesWorkspaceActive => ActiveWorkspace == ToolWorkspace.LootTables;

    [RelayCommand]
    public void ShowItemsWorkspace()
    {
        ActiveWorkspace = ToolWorkspace.Items;
    }

    [RelayCommand]
    public void ShowLootTablesWorkspace()
    {
        ActiveWorkspace = ToolWorkspace.LootTables;
    }
}