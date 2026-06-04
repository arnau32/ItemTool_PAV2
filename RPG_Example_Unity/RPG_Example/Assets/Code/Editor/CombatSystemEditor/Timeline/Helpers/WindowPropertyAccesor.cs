using UnityEditor;

/// Centralized helper for accessing SerializedProperty start/end values from timeline windows.
internal static class WindowPropertyAccessor
{
    #region Public API

    /// Gets the root SerializedProperty for a given window type and index.
    public static SerializedProperty GetRoot(SerializedObject so, string fieldName, int index,
        WindowStructureType structureType)
    {
        if (so == null || string.IsNullOrEmpty(fieldName)) return null;

        var prop = so.FindProperty(fieldName);
        if (prop == null) return null;

        if (structureType == WindowStructureType.Single)
            return prop;

        if (!prop.isArray || index < 0 || index >= prop.arraySize) return null;
        return prop.GetArrayElementAtIndex(index);
    }

    /// Gets the "start" property for a given window type and index.
    public static SerializedProperty GetStart(SerializedObject so, string fieldName, int index,
        WindowStructureType structureType)
    {
        var root = GetRoot(so, fieldName, index, structureType);
        if (root == null) return null;

        switch (structureType)
        {
            case WindowStructureType.SpecialDamage:
            case WindowStructureType.SpecialVfx:
            case WindowStructureType.SpecialAudioWindow:
                // These structs have a nested WindowEvent called "window".
                var w = root.FindPropertyRelative("window");
                return w != null ? w.FindPropertyRelative("start") : null;

            case WindowStructureType.SpecialAudioTrigger:
                // AudioTrigger uses a single float "triggerAt" — mapped to "start"
                // so the timeline can render it as a zero-width marker block.
                return root.FindPropertyRelative("triggerAt");

            default:
                return root.FindPropertyRelative("start");
        }
    }

    /// Gets the "end" property for a given window type and index.
    public static SerializedProperty GetEnd(SerializedObject so, string fieldName, int index,
        WindowStructureType structureType)
    {
        var root = GetRoot(so, fieldName, index, structureType);
        if (root == null) return null;

        switch (structureType)
        {
            case WindowStructureType.SpecialDamage:
            case WindowStructureType.SpecialVfx:
            case WindowStructureType.SpecialAudioWindow:
                var w = root.FindPropertyRelative("window");
                return w != null ? w.FindPropertyRelative("end") : null;

            case WindowStructureType.SpecialAudioTrigger:
                // AudioTrigger has no "end" — return "triggerAt" for both
                // so the block renders as a zero-width vertical marker.
                return root.FindPropertyRelative("triggerAt");

            default:
                return root.FindPropertyRelative("end");
        }
    }

    /// Tries to get both start and end properties in a single call.
    public static bool TryGetRange(SerializedObject so, SelectionKey key, WindowReflectionRegistry registry,
        out SerializedProperty start, out SerializedProperty end)
    {
        start = null;
        end   = null;

        var meta = registry.GetMetadata(key.fieldName);
        if (meta == null) return false;

        start = GetStart(so, key.fieldName, key.index, meta.StructureType);
        end   = GetEnd(so,   key.fieldName, key.index, meta.StructureType);

        return start != null && end != null;
    }

    #endregion
}
