namespace ItemTool.Application.DTOs;

public sealed class UnityContentOperationResultDto
{
    public bool Succeeded { get; set; }

    public string Message { get; set; } = string.Empty;

    public string OutputPath { get; set; } = string.Empty;

    public ContentDatabaseDto? ImportedDatabase { get; set; }

    public List<string> Warnings { get; set; } = new();

    public static UnityContentOperationResultDto Success(
        string message,
        string outputPath = "",
        ContentDatabaseDto? importedDatabase = null)
    {
        return new UnityContentOperationResultDto
        {
            Succeeded = true,
            Message = message,
            OutputPath = outputPath,
            ImportedDatabase = importedDatabase
        };
    }

    public static UnityContentOperationResultDto Failure(string message)
    {
        return new UnityContentOperationResultDto
        {
            Succeeded = false,
            Message = message
        };
    }
}