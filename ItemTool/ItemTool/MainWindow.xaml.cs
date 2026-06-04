using ItemTool.App.ViewModels;
using ItemTool.Application.Validation;
using ItemTool.Infrastructure.Persistence;
using System.IO;
using System.Windows;

namespace ItemTool.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        string filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ItemTool",
            "items.json");

        DataContext = new ItemBrowserViewModel(
            new JsonItemRepository(filePath),
            new ItemValidator());
    }
}