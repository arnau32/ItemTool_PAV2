using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ItemTool.App.Services;
using ItemTool.App.Visuals;
using ItemTool.Application.Abstractions;
using ItemTool.Application.DTOs;
using ItemTool.Application.Services;
using ItemTool.Application.Validation;

namespace ItemTool.App.ViewModels;

public sealed partial class MainToolViewModel : ViewModelBase
{
    private readonly IContentDatabaseRepository _repository;
    private readonly IUnityContentImporter _unityContentImporter;
    private readonly IUnityContentExporter _unityContentExporter;
    private readonly IFilePickerService _filePickerService;

    private ToolWorkspace _activeWorkspace = ToolWorkspace.Items;
    private string _unityProjectRootPath = string.Empty;
    private string _projectSettingsStatusText = "No Unity project selected.";
    private string _unityContentOperationStatusText = "Import/export not run yet.";

    public MainToolViewModel(
        IContentDatabaseRepository repository,
        IUnityContentImporter unityContentImporter,
        IUnityContentExporter unityContentExporter,
        ItemValidator itemValidator,
        LootTableValidator lootTableValidator,
        ItemFactory itemFactory,
        IFilePickerService filePickerService)
    {
        _repository = repository;
        _unityContentImporter = unityContentImporter;
        _unityContentExporter = unityContentExporter;
        _filePickerService = filePickerService;

        Items = new ItemBrowserViewModel(
            repository,
            itemValidator,
            itemFactory,
            filePickerService);

        LootTables = new LootBrowserViewModel(
            repository,
            lootTableValidator);

        Items.SelectedItemChanged += RefreshSelectedItemUsages;
    }

    public ItemBrowserViewModel Items { get; }

    public LootBrowserViewModel LootTables { get; }

    public ObservableCollection<ItemLootUsageViewModel> SelectedItemLootUsages { get; } = new();

    public string UnityProjectRootPath
    {
        get => _unityProjectRootPath;
        set
        {
            if (SetProperty(ref _unityProjectRootPath, value))
            {
                ItemIconSourceLoader.UnityProjectRootPath = value;
                ProjectSettingsStatusText = CreateProjectSettingsStatusText(value);

                Items.Editor.NotifyHeaderPreviewChanged();
                RefreshAllItemIconSources();
            }
        }
    }

    public string ProjectSettingsStatusText
    {
        get => _projectSettingsStatusText;
        private set => SetProperty(ref _projectSettingsStatusText, value);
    }

    public string UnityContentOperationStatusText
    {
        get => _unityContentOperationStatusText;
        private set => SetProperty(ref _unityContentOperationStatusText, value);
    }

    public string SelectedItemUsageSummary
    {
        get
        {
            if (Items.SelectedListItem == null)
                return "No item selected.";

            if (LootTables.LootTables.Count == 0)
                return "Load loot tables or press Refresh to search references.";

            if (SelectedItemLootUsages.Count == 0)
                return "No direct loot table reference(s) found.";

            return $"{SelectedItemLootUsages.Count} direct loot table reference(s) found.";
        }
    }

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
    public async Task LoadProjectSettingsAsync()
    {
        ContentDatabaseDto database = await _repository.LoadAsync();

        UnityProjectRootPath = database.ProjectSettings.UnityProjectRootPath;
    }

    [RelayCommand]
    public async Task BrowseUnityProjectRootAsync()
    {
        string? selectedFolder = _filePickerService.PickFolder(
            UnityProjectRootPath,
            "Select Unity project root folder");

        if (string.IsNullOrWhiteSpace(selectedFolder))
            return;

        UnityProjectRootPath = selectedFolder;

        await SaveProjectSettingsAsync();
    }

    [RelayCommand]
    public async Task SaveProjectSettingsAsync()
    {
        ContentDatabaseDto database = await _repository.LoadAsync();

        database.ProjectSettings.UnityProjectRootPath = UnityProjectRootPath;

        await _repository.SaveAsync(database);

        ProjectSettingsStatusText = CreateProjectSettingsStatusText(UnityProjectRootPath);
    }

    [RelayCommand]
    public async Task ImportUnityContentAsync()
    {
        UnityContentOperationStatusText = "Importing Unity content...";

        await SaveProjectSettingsAsync();

        UnityContentOperationResultDto result = await _unityContentImporter.ImportAsync(
            UnityProjectRootPath);

        UnityContentOperationStatusText = FormatOperationResult(result);

        if (!result.Succeeded || result.ImportedDatabase == null)
            return;

        ContentDatabaseDto mergedDatabase = await MergeImportedDatabaseAsync(
            result.ImportedDatabase);

        await _repository.SaveAsync(mergedDatabase);

        await Items.LoadAsync();
        await LootTables.LoadAsync();

        RefreshSelectedItemUsages();
    }

    [RelayCommand]
    public async Task ExportUnityContentAsync()
    {
        UnityContentOperationStatusText = "Exporting Unity content...";

        await SaveProjectSettingsAsync();

        ContentDatabaseDto database = await CreateDatabaseSnapshotAsync();

        UnityContentOperationResultDto result = await _unityContentExporter.ExportAsync(
            database,
            UnityProjectRootPath);

        if (result.Succeeded)
        {
            await _repository.SaveAsync(database);

            Items.Editor.NotifyHeaderPreviewChanged();
            RefreshAllItemIconSources();
            RefreshSelectedItemUsages();
        }

        UnityContentOperationStatusText = FormatOperationResult(result);
    }

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

    [RelayCommand]
    public async Task NavigateToItemAsync(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        if (Items.Items.Count == 0)
            await Items.LoadAsync();

        bool selected = Items.SelectItemById(itemId);

        if (selected)
            ActiveWorkspace = ToolWorkspace.Items;
    }

    [RelayCommand]
    public async Task NavigateToLootTableAsync(string? lootTableId)
    {
        if (string.IsNullOrWhiteSpace(lootTableId))
            return;

        if (LootTables.LootTables.Count == 0)
            await LootTables.LoadAsync();

        bool selected = LootTables.SelectLootTableById(lootTableId);

        if (selected)
            ActiveWorkspace = ToolWorkspace.LootTables;
    }

    [RelayCommand]
    public async Task RefreshSelectedItemUsagesAsync()
    {
        if (Items.SelectedListItem == null)
        {
            SelectedItemLootUsages.Clear();
            OnPropertyChanged(nameof(SelectedItemUsageSummary));
            return;
        }

        if (LootTables.LootTables.Count == 0)
            await LootTables.LoadAsync();

        RefreshSelectedItemUsages();
    }

    private async Task<ContentDatabaseDto> CreateDatabaseSnapshotAsync()
    {
        ContentDatabaseDto database = await _repository.LoadAsync();

        database.ProjectSettings.UnityProjectRootPath = UnityProjectRootPath;

        if (Items.Items.Count > 0)
        {
            database.Items = Items.Items
                .Select(x => x.Item)
                .ToList();
        }

        if (LootTables.LootTables.Count > 0)
        {
            database.LootTables = LootTables.LootTables
                .Select(x => x.LootTable)
                .ToList();
        }

        return database;
    }

    private async Task<ContentDatabaseDto> MergeImportedDatabaseAsync(
        ContentDatabaseDto importedDatabase)
    {
        ContentDatabaseDto currentDatabase = await _repository.LoadAsync();

        currentDatabase.ProjectSettings.UnityProjectRootPath = UnityProjectRootPath;

        if (importedDatabase.Items.Count > 0)
            currentDatabase.Items = importedDatabase.Items;

        if (importedDatabase.LootTables.Count > 0)
            currentDatabase.LootTables = importedDatabase.LootTables;

        return currentDatabase;
    }

    private void RefreshSelectedItemUsages()
    {
        SelectedItemLootUsages.Clear();

        string? itemId = Items.SelectedItemId;

        if (!string.IsNullOrWhiteSpace(itemId) &&
            LootTables.LootTables.Count > 0)
        {
            foreach (ItemLootUsageViewModel usage in LootTables.FindUsagesOfItem(itemId))
                SelectedItemLootUsages.Add(usage);
        }

        OnPropertyChanged(nameof(SelectedItemUsageSummary));
    }

    private void RefreshAllItemIconSources()
    {
        foreach (ItemListItemViewModel item in Items.Items)
            item.NotifyIconSourceChanged();
    }

    private static string CreateProjectSettingsStatusText(string unityProjectRootPath)
    {
        if (string.IsNullOrWhiteSpace(unityProjectRootPath))
            return "No Unity project selected.";

        if (!Directory.Exists(unityProjectRootPath))
            return "Selected Unity project folder does not exist.";

        bool hasAssetsFolder = Directory.Exists(Path.Combine(unityProjectRootPath, "Assets"));
        bool hasProjectSettingsFolder = Directory.Exists(Path.Combine(unityProjectRootPath, "ProjectSettings"));

        if (hasAssetsFolder && hasProjectSettingsFolder)
            return "Unity project detected.";

        if (hasAssetsFolder)
            return "Assets folder found, but ProjectSettings is missing.";

        return "Selected folder does not look like a Unity project.";
    }

    private static string FormatOperationResult(UnityContentOperationResultDto result)
    {
        string status = result.Succeeded
            ? "Success"
            : "Failed";

        List<string> lines = new()
        {
            $"[{status}] {result.Message}"
        };

        if (!string.IsNullOrWhiteSpace(result.OutputPath))
            lines.Add($"Output: {result.OutputPath}");

        if (result.Warnings.Count > 0)
        {
            lines.Add("Observations:");

            foreach (string warning in result.Warnings)
                lines.Add($"- {warning}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}