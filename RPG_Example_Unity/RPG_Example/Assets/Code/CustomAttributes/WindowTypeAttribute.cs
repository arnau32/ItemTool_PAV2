using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class WindowTypeAttribute : Attribute
{
    public string DisplayName { get; }
    public Color Color { get; }
    public int TrackOrder { get; }
    public string EnableFieldName { get; set; }
    public WindowStructureType StructureType { get; set; } = WindowStructureType.List;

    public WindowTypeAttribute(string displayName, float r, float g, float b, int trackOrder = 0)
    {
        DisplayName = displayName;
        Color = new Color(r, g, b);
        TrackOrder = trackOrder;
        StructureType = WindowStructureType.List;
        EnableFieldName = string.Empty;
    }
}

public enum WindowStructureType
{
    Single,
    List,
    SpecialDamage,
    SpecialVfx,
    SpecialAudioTrigger,
    SpecialAudioWindow
}