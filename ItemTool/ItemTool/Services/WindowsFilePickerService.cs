using System.IO;
using Microsoft.Win32;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using OpenFolderDialog = Microsoft.Win32.OpenFolderDialog;

namespace ItemTool.App.Services;

public sealed class WindowsFilePickerService : IFilePickerService
{
    public string? PickImageFile(string? initialPath = null)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Select item icon",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            string? directory = Path.GetDirectoryName(initialPath);

            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                dialog.InitialDirectory = directory;
        }

        bool? result = dialog.ShowDialog();

        return result == true
            ? dialog.FileName
            : null;
    }

    public string? PickFolder(string? initialPath = null, string title = "Select folder")
    {
        OpenFolderDialog dialog = new()
        {
            Title = title,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
            dialog.InitialDirectory = initialPath;

        bool? result = dialog.ShowDialog();

        return result == true
            ? dialog.FolderName
            : null;
    }
}