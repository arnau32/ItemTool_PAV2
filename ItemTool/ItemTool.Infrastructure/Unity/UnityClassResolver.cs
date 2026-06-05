using ItemTool.Domain.Enums;

namespace ItemTool.Infrastructure.Unity;

internal static class UnityClassResolver
{
    public static string ResolveUnityClassName(
        IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, string> guidToAssetPath)
    {
        string editorClassIdentifier = ReadEditorClassIdentifier(lines);

        if (!string.IsNullOrWhiteSpace(editorClassIdentifier))
            return editorClassIdentifier;

        string? scriptGuid = UnityYamlReader.ReadObjectGuid(lines, "m_Script");

        if (string.IsNullOrWhiteSpace(scriptGuid))
            return string.Empty;

        if (!guidToAssetPath.TryGetValue(scriptGuid, out string scriptUnityPath))
            return string.Empty;

        string scriptFileName = Path.GetFileNameWithoutExtension(scriptUnityPath);

        return scriptFileName?.Trim() ?? string.Empty;
    }

    public static bool IsSupportedItemClass(string className)
    {
        return className is
            "ItemData" or
            "EquipableItemData" or
            "WeaponData" or
            "ConsumableItemData" or
            "CraftingItemData" or
            "CollectableItemData";
    }

    public static bool IsLootTableClass(string className)
    {
        return string.Equals(className, "LootTable", StringComparison.Ordinal);
    }

    public static ItemKind GetItemKindFromClassName(
        string className,
        int unityItemType)
    {
        return className switch
        {
            "WeaponData" => ItemKind.Weapon,
            "EquipableItemData" => ItemKind.Equipment,
            "ConsumableItemData" => ItemKind.Consumable,
            "CraftingItemData" => ItemKind.Crafting,
            "CollectableItemData" => ItemKind.Collectable,
            _ => GetItemKindFromUnityItemType(unityItemType)
        };
    }

    private static string ReadEditorClassIdentifier(IReadOnlyList<string> lines)
    {
        string rawIdentifier = UnityYamlReader.ReadScalar(lines, "m_EditorClassIdentifier") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(rawIdentifier))
            return string.Empty;

        int separatorIndex = rawIdentifier.LastIndexOf("::", StringComparison.Ordinal);

        if (separatorIndex >= 0 && separatorIndex + 2 < rawIdentifier.Length)
            return rawIdentifier[(separatorIndex + 2)..].Trim();

        return rawIdentifier.Trim();
    }

    private static ItemKind GetItemKindFromUnityItemType(int unityItemType)
    {
        return unityItemType switch
        {
            0 => ItemKind.Equipment,
            1 => ItemKind.Consumable,
            2 => ItemKind.Crafting,
            3 => ItemKind.Collectable,
            _ => ItemKind.Equipment
        };
    }
}