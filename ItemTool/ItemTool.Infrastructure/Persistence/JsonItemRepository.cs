using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ItemTool.Application.Abstractions;
using ItemTool.Application.DTOs;

namespace ItemTool.Infrastructure.Persistence;

public sealed class JsonItemRepository : IItemRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonItemRepository(string filePath)
    {
        _filePath = filePath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<IReadOnlyList<ItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            return Array.Empty<ItemDto>();

        await using FileStream stream = File.OpenRead(_filePath);
        List<ItemDto>? items = await JsonSerializer.DeserializeAsync<List<ItemDto>>(stream, _jsonOptions, cancellationToken);
        
        if (items == null)
            return Array.Empty<ItemDto>();

        return items;
    }

    public async Task SaveAllAsync(IReadOnlyList<ItemDto> items, CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using FileStream stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, items, _jsonOptions, cancellationToken);
    }
}