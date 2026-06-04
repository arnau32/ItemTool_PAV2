namespace ItemTool.App.Services;

public interface IFilePickerService
{
    string? PickImageFile(string? initialPath = null);
}