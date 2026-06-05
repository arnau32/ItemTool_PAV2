using System.Globalization;
using System.Text.RegularExpressions;

namespace ItemTool.Infrastructure.Unity;

internal static class UnityYamlReader
{
    private static readonly Regex GuidRegex = new(
        @"guid:\s*(?<guid>[a-fA-F0-9]+)",
        RegexOptions.Compiled);

    public static string? ReadScalar(
        IReadOnlyList<string> lines,
        string fieldName)
    {
        string prefix = $"{fieldName}:";

        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();

            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            string value = trimmed[prefix.Length..].Trim();

            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value;
        }

        return null;
    }

    public static string? ReadObjectGuid(
        IReadOnlyList<string> lines,
        string fieldName)
    {
        string prefix = $"{fieldName}:";

        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();

            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            Match match = GuidRegex.Match(trimmed);

            if (match.Success)
                return match.Groups["guid"].Value;
        }

        return null;
    }

    public static List<List<string>> ReadYamlListBlocks(
        IReadOnlyList<string> lines,
        string listFieldName)
    {
        List<List<string>> blocks = new();

        string listPrefix = $"{listFieldName}:";
        bool insideList = false;
        List<string>? currentBlock = null;

        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();

            if (!insideList)
            {
                if (trimmed.StartsWith(listPrefix, StringComparison.Ordinal))
                    insideList = true;

                continue;
            }

            if (!line.StartsWith(" ", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(line))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                if (currentBlock != null && currentBlock.Count > 0)
                    blocks.Add(currentBlock);

                currentBlock = new List<string>
                {
                    trimmed[2..]
                };

                continue;
            }

            currentBlock?.Add(trimmed);
        }

        if (currentBlock != null && currentBlock.Count > 0)
            blocks.Add(currentBlock);

        return blocks;
    }

    public static string? ReadScalarFromBlock(
        IReadOnlyList<string> block,
        string fieldName)
    {
        string prefix = $"{fieldName}:";

        foreach (string line in block)
        {
            string trimmed = line.TrimStart();

            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            string value = trimmed[prefix.Length..].Trim();

            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value;
        }

        return null;
    }

    public static string? ReadObjectGuidFromBlock(
        IReadOnlyList<string> block,
        string fieldName)
    {
        string prefix = $"{fieldName}:";

        foreach (string line in block)
        {
            string trimmed = line.TrimStart();

            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            Match match = GuidRegex.Match(trimmed);

            if (match.Success)
                return match.Groups["guid"].Value;
        }

        return null;
    }

    public static int ReadInt(
        IReadOnlyList<string> lines,
        string fieldName,
        int defaultValue)
    {
        string? raw = ReadScalar(lines, fieldName);

        return int.TryParse(
            raw,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int value)
            ? value
            : defaultValue;
    }

    public static float ReadFloat(
        IReadOnlyList<string> lines,
        string fieldName,
        float defaultValue)
    {
        string? raw = ReadScalar(lines, fieldName);

        return float.TryParse(
            raw,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float value)
            ? value
            : defaultValue;
    }

    public static bool ReadBool(
        IReadOnlyList<string> lines,
        string fieldName,
        bool defaultValue)
    {
        string? raw = ReadScalar(lines, fieldName);

        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        if (raw == "1")
            return true;

        if (raw == "0")
            return false;

        return bool.TryParse(raw, out bool value)
            ? value
            : defaultValue;
    }

    public static int ReadIntFromBlock(
        IReadOnlyList<string> block,
        string fieldName,
        int defaultValue)
    {
        string? raw = ReadScalarFromBlock(block, fieldName);

        return int.TryParse(
            raw,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int value)
            ? value
            : defaultValue;
    }

    public static int ReadNestedInt(
        IReadOnlyList<string> lines,
        string parentFieldName,
        string childFieldName,
        int defaultValue)
    {
        string parentPrefix = $"{parentFieldName}:";
        string childPrefix = $"{childFieldName}:";

        bool insideParent = false;

        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith(parentPrefix, StringComparison.Ordinal))
            {
                insideParent = true;
                continue;
            }

            if (!insideParent)
                continue;

            if (!line.StartsWith(" ", StringComparison.Ordinal))
                break;

            if (!trimmed.StartsWith(childPrefix, StringComparison.Ordinal))
                continue;

            string value = trimmed[childPrefix.Length..].Trim();

            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsed)
                ? parsed
                : defaultValue;
        }

        return defaultValue;
    }

    public static TEnum ReadEnumInt<TEnum>(
        IReadOnlyList<string> lines,
        string fieldName,
        TEnum defaultValue)
        where TEnum : struct, Enum
    {
        int rawValue = ReadInt(
            lines,
            fieldName,
            Convert.ToInt32(defaultValue, CultureInfo.InvariantCulture));

        if (Enum.IsDefined(typeof(TEnum), rawValue))
            return (TEnum)Enum.ToObject(typeof(TEnum), rawValue);

        return defaultValue;
    }

    public static TEnum ReadEnumIntFromBlock<TEnum>(
        IReadOnlyList<string> block,
        string fieldName,
        TEnum defaultValue)
        where TEnum : struct, Enum
    {
        int rawValue = ReadIntFromBlock(
            block,
            fieldName,
            Convert.ToInt32(defaultValue, CultureInfo.InvariantCulture));

        if (Enum.IsDefined(typeof(TEnum), rawValue))
            return (TEnum)Enum.ToObject(typeof(TEnum), rawValue);

        return defaultValue;
    }
}