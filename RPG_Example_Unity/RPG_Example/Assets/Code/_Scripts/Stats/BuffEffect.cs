using System;

[Serializable]
public class BuffEffect
{
    public Enums.BuffApplicationMode applicationMode;
    public Enums.StatType statType;
    public StatModifierType modifierType;
    public float baseValue;
    public float duration;

    [NonSerialized] public StatModifier runtimeModifier;
    [NonSerialized] public float runtimeRegenDelta;
}
