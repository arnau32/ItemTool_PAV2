using System.IO;
using System.Windows;
using ItemTool.App.Services;
using ItemTool.App.ViewModels;
using ItemTool.Application.Services;
using ItemTool.Application.Validation;
using ItemTool.Infrastructure.Persistence;
using ItemTool.Infrastructure.Unity;

namespace ItemTool.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        string filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ItemTool",
            "itemtool_project.json");

        DataContext = new MainToolViewModel(
            new JsonContentDatabaseRepository(filePath),
            new UnityContentImporter(),
            new UnityContentExporter(),
            new ItemValidator(),
            new LootTableValidator(),
            new ItemFactory(),
            new WindowsFilePickerService());
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainToolViewModel viewModel)
            await viewModel.LoadProjectSettingsAsync();
    }
}