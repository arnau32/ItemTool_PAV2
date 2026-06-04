using System.IO;
using System.Windows;
using ItemTool.App.ViewModels;
using ItemTool.Application.Validation;
using ItemTool.Infrastructure.Persistence;

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

        DataContext = new ItemBrowserViewModel(
            new JsonContentDatabaseRepository(filePath),
            new ItemValidator());
    }
}