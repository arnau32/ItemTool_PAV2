using System;
using UnityEngine;

public enum StatModifierType { Flat = 100, PercentAdd = 200, PercentMult = 300 }

[Serializable]
public class StatModifier
{
    public Enums.StatType statTypeAffected;
    public StatModifierType type;
    [SerializeField] public float value;
    public int order;
    [NonSerialized] public object source;
    
    public StatModifier()
    {
        order = (int)StatModifierType.Flat; // safe default = 100
    }
    public StatModifier(float val, StatModifierType _type, int _order, object _source)
    {
        value = val;
        type = _type;
        order = _order;
        source = _source;
    }
    
    public StatModifier(float val, StatModifierType _type, int _order, object _source, Enums.StatType _statTypeAffected)
    {
        value = val;
        type = _type;
        order = _order;
        source = _source;
        statTypeAffected = _statTypeAffected;
    }

    public StatModifier(float val, StatModifierType _type) : this(val, _type, (int)_type, null) { }
    public StatModifier(float val, StatModifierType _type, int _order) : this(val, _type, _order, null) { }
    public StatModifier(float val, StatModifierType _type, object _source) : this(val, _type, (int)_type, _source) { }
    
    public StatModifier Clone()
    {
        return new StatModifier
        {
            statTypeAffected = statTypeAffected,
            type = type,
            value = value,
            order = order,
            source = source
        };
    }

}
