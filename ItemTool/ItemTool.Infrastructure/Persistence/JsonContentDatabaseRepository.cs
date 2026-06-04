using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ItemTool.Application.Abstractions;
using ItemTool.Application.DTOs;

namespace ItemTool.Infrastructure.Persistence;

public sealed class JsonContentDatabaseRepository : IContentDatabaseRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonContentDatabaseRepository(string filePath)
    {
        _filePath = filePath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public async Task<ContentDatabaseDto> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            return new ContentDatabaseDto();

        string json = await File.ReadAllTextAsync(_filePath, cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
            return new ContentDatabaseDto();

        ContentDatabaseDto database;

        if (IsLegacyItemListJson(json))
        {
            List<ItemDto>? legacyItems = JsonSerializer.Deserialize<List<ItemDto>>(json, _jsonOptions);

            database = new ContentDatabaseDto
            {
                SchemaVersion = 1,
                ProjectSettings = new ProjectSettingsDto(),
                Items = legacyItems ?? new List<ItemDto>(),
                LootTables = new List<LootTableDto>()
            };
        }
        else
        {
            database = JsonSerializer.Deserialize<ContentDatabaseDto>(json, _jsonOptions)
                       ?? new ContentDatabaseDto();
        }

        Normalize(database);
        return database;
    }

    public async Task SaveAsync(ContentDatabaseDto database, CancellationToken cancellationToken = default)
    {
        Normalize(database);

        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string json = JsonSerializer.Serialize(database, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }

    private static bool IsLegacyItemListJson(string json)
    {
        return json.TrimStart().StartsWith("[");
    }

    private static void Normalize(ContentDatabaseDto database)
    {
        database.SchemaVersion = database.SchemaVersion <= 0 ? 1 : database.SchemaVersion;

        database.ProjectSettings ??= new ProjectSettingsDto();
        database.ProjectSettings.UnityProjectRootPath ??= string.Empty;

        database.Items ??= new List<ItemDto>();
        database.LootTables ??= new List<LootTableDto>();

        foreach (ItemDto item in database.Items)
        {
            item.Id ??= string.Empty;
            item.ItemNameId ??= string.Empty;
            item.DisplayName ??= string.Empty;
            item.Description ??= string.Empty;
            item.SlotDimension ??= new DimensionsDto();

            item.EnsureDetailsForCurrentKind();

            if (string.IsNullOrWhiteSpace(item.Id))
                item.Id = item.ItemNameId;
        }

        foreach (LootTableDto lootTable in database.LootTables)
        {
            lootTable.Id ??= string.Empty;
            lootTable.Name ??= string.Empty;
            lootTable.GuaranteedEntries ??= new ObservableCollection<LootEntryDto>();
            lootTable.WeightedEntries ??= new ObservableCollection<LootEntryDto>();
        }
    }
}