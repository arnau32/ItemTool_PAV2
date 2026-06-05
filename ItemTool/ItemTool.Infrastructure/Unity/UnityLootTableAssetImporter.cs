using ItemTool.Application.DTOs;
using ItemTool.Domain.Enums;

namespace ItemTool.Infrastructure.Unity;

internal static class UnityLootTableAssetImporter
{
    public static LootTableDto? TryImport(
        string unityProjectRootPath,
        string assetPath,
        IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, string> guidToAssetPath,
        UnityReferenceIdMaps referenceIdMaps)
    {
        string assetName = UnityYamlReader.ReadScalar(lines, "m_Name") ?? Path.GetFileNameWithoutExtension(assetPath);
        string lootTableId = UnityYamlReader.ReadScalar(lines, "lootTableID") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(lootTableId))
            lootTableId = assetName;

        LootTableDto lootTable = new()
        {
            Id = lootTableId,
            Name = assetName,
            MinRandomPicks = UnityYamlReader.ReadInt(lines, "minRandomPicks", defaultValue: 0),
            MaxRandomPicks = UnityYamlReader.ReadInt(lines, "maxRandomPicks", defaultValue: 0)
        };

        foreach (LootEntryDto entry in ReadLootEntries(
                     unityProjectRootPath,
                     lines,
                     "guaranteedEntries",
                     requiresWeight: false,
                     guidToAssetPath,
                     referenceIdMaps))
        {
            lootTable.GuaranteedEntries.Add(entry);
        }

        foreach (LootEntryDto entry in ReadLootEntries(
                     unityProjectRootPath,
                     lines,
                     "weightedEntries",
                     requiresWeight: true,
                     guidToAssetPath,
                     referenceIdMaps))
        {
            lootTable.WeightedEntries.Add(entry);
        }

        return lootTable;
    }

    private static IEnumerable<LootEntryDto> ReadLootEntries(
        string unityProjectRootPath,
        IReadOnlyList<string> lines,
        string listFieldName,
        bool requiresWeight,
        IReadOnlyDictionary<string, string> guidToAssetPath,
        UnityReferenceIdMaps referenceIdMaps)
    {
        List<List<string>> entryBlocks = UnityYamlReader.ReadYamlListBlocks(lines, listFieldName);

        foreach (List<string> entryBlock in entryBlocks)
        {
            LootEntryType entryType = UnityYamlReader.ReadEnumIntFromBlock(
                entryBlock,
                "entryType",
                LootEntryType.Item);

            LootEntryDto entry = new()
            {
                EntryType = entryType,
                MinQuantity = UnityYamlReader.ReadIntFromBlock(entryBlock, "minQuantity", defaultValue: 1),
                MaxQuantity = UnityYamlReader.ReadIntFromBlock(entryBlock, "maxQuantity", defaultValue: 1),
                Weight = UnityYamlReader.ReadIntFromBlock(entryBlock, "weight", defaultValue: requiresWeight ? 1 : 0),
                EquipableOverrideMode = UnityYamlReader.ReadEnumIntFromBlock(
                    entryBlock,
                    "equipableOverrideMode",
                    LootEntryEquipableOverrideMode.None),
                OverrideFixedRarity = UnityYamlReader.ReadEnumIntFromBlock(
                    entryBlock,
                    "overrideFixedRarity",
                    ItemRarity.Common)
            };

            if (entry.EntryType == LootEntryType.Item)
            {
                string? itemGuid = UnityYamlReader.ReadObjectGuidFromBlock(entryBlock, "item");

                if (!string.IsNullOrWhiteSpace(itemGuid) &&
                    TryResolveReferencedId(
                        unityProjectRootPath,
                        itemGuid,
                        guidToAssetPath,
                        referenceIdMaps.ItemIdByUnityPath,
                        out string itemId))
                {
                    entry.ItemId = itemId;
                }
            }
            else if (entry.EntryType == LootEntryType.LootTable)
            {
                string? nestedTableGuid = UnityYamlReader.ReadObjectGuidFromBlock(entryBlock, "nestedTable");

                if (!string.IsNullOrWhiteSpace(nestedTableGuid) &&
                    TryResolveReferencedId(
                        unityProjectRootPath,
                        nestedTableGuid,
                        guidToAssetPath,
                        referenceIdMaps.LootTableIdByUnityPath,
                        out string lootTableId))
                {
                    entry.NestedLootTableId = lootTableId;
                }
            }

            yield return entry;
        }
    }

    private static bool TryResolveReferencedId(
        string unityProjectRootPath,
        string guid,
        IReadOnlyDictionary<string, string> guidToAssetPath,
        IReadOnlyDictionary<string, string> idByUnityPath,
        out string id)
    {
        id = string.Empty;

        if (!guidToAssetPath.TryGetValue(guid, out string unityAssetPath))
            return false;

        if (idByUnityPath.TryGetValue(unityAssetPath, out string resolvedId))
        {
            id = resolvedId;
            return true;
        }

        string normalizedUnityPath = unityAssetPath.Replace('\\', '/');

        if (idByUnityPath.TryGetValue(normalizedUnityPath, out resolvedId))
        {
            id = resolvedId;
            return true;
        }

        string absoluteCandidate = Path.Combine(
            unityProjectRootPath,
            unityAssetPath.Replace('/', Path.DirectorySeparatorChar));

        string candidateUnityPath = UnityAssetScanner.ToUnityAssetPath(
            unityProjectRootPath,
            absoluteCandidate);

        if (idByUnityPath.TryGetValue(candidateUnityPath, out resolvedId))
        {
            id = resolvedId;
            return true;
        }

        return false;
    }
}