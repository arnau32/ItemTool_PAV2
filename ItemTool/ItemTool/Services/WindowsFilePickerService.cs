using System.IO;
using Microsoft.Win32;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

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
}