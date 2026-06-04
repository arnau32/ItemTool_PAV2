using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

internal sealed class WindowReflectionRegistry
{
    #region Metadata Structures

    public sealed class WindowMetadata
    {
        public string FieldName { get; set; }
        public string DisplayName { get; set; }
        public Color Color { get; set; }
        public int TrackOrder { get; set; }
        public WindowStructureType StructureType { get; set; }

        public string EnableFieldName { get; set; }

        public FieldInfo FieldInfo { get; set; }
        public FieldInfo EnableFieldInfo { get; set; }

        public bool HasEnableField => !string.IsNullOrEmpty(EnableFieldName);

        // All list-based structure types — used to determine if GetListProperty applies.
        public bool IsList => StructureType == WindowStructureType.List
                              || StructureType == WindowStructureType.SpecialDamage
                              || StructureType == WindowStructureType.SpecialVfx
                              || StructureType == WindowStructureType.SpecialAudioTrigger
                              || StructureType == WindowStructureType.SpecialAudioWindow;
    }

    #endregion

    #region Fields

    private readonly Dictionary<string, WindowMetadata> _metadataByFieldName = new Dictionary<string, WindowMetadata>();
    private readonly List<WindowMetadata> _orderedMetadata = new List<WindowMetadata>();
    private bool _initialized = false;

    #endregion

    #region Public API

    public void Initialize(Type attackDataType)
    {
        if (_initialized) return;
        _initialized = true;

        _metadataByFieldName.Clear();
        _orderedMetadata.Clear();

        var fields = attackDataType.GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            var attr = field.GetCustomAttribute<WindowTypeAttribute>();
            if (attr == null) continue;

            var meta = new WindowMetadata
            {
                FieldName = field.Name,
                DisplayName = attr.DisplayName,
                Color = attr.Color,
                TrackOrder = attr.TrackOrder,
                StructureType = attr.StructureType,
                EnableFieldName = attr.EnableFieldName,
                FieldInfo = field
            };

            if (!string.IsNullOrEmpty(attr.EnableFieldName))
            {
                meta.EnableFieldInfo = attackDataType.GetField(
                    attr.EnableFieldName, BindingFlags.Public | BindingFlags.Instance);
            }

            _metadataByFieldName[field.Name] = meta;
            _orderedMetadata.Add(meta);
        }

        _orderedMetadata.Sort((a, b) => a.TrackOrder.CompareTo(b.TrackOrder));
    }

    public WindowMetadata GetMetadata(string fieldName)
    {
        return _metadataByFieldName.TryGetValue(fieldName, out var meta) ? meta : null;
    }

    public List<WindowMetadata> GetAllOrdered()
    {
        return _orderedMetadata;
    }

    public bool IsEnabled(SerializedObject so, WindowMetadata meta)
    {
        if (!meta.HasEnableField) return true;

        var prop = so.FindProperty(meta.EnableFieldName);
        return prop != null && prop.boolValue;
    }

    public void SetEnabled(SerializedObject so, WindowMetadata meta, bool enabled)
    {
        if (!meta.HasEnableField) return;

        var prop = so.FindProperty(meta.EnableFieldName);
        if (prop != null) prop.boolValue = enabled;
    }

    public SerializedProperty GetListProperty(SerializedObject so, WindowMetadata meta)
    {
        return so.FindProperty(meta.FieldName);
    }

    public SerializedProperty GetSingleProperty(SerializedObject so, WindowMetadata meta)
    {
        return so.FindProperty(meta.FieldName);
    }

    #endregion
}
