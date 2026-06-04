namespace ItemTool.App.Services;

public interface IFilePickerService
{
    string? PickImageFile(string? initialPath = null);

    string? PickFolder(string? initialPath = null, string title = "Select folder");
}