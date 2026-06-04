namespace ItemTool.Application.DTOs;

public sealed class ContentDatabaseDto
{
    public int SchemaVersion { get; set; } = 1;

    public ProjectSettingsDto ProjectSettings { get; set; } = new();

    public List<ItemDto> Items { get; set; } = new();

    public List<LootTableDto> LootTables { get; set; } = new();
}