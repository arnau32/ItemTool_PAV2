using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

[Serializable]
public class CharacterStat
{
    public float baseValue;
    public Enums.StatType statTypeAffected;

    private bool _reCalculate;
    private float _value;
    private float _lastBaseValue = float.MinValue;
    private readonly List<StatModifier> _statsModifiers;
    private readonly ReadOnlyCollection<StatModifier> _readStatModifiers;

    public float Value
    {
        get
        {
            if (!_reCalculate && Mathf.Approximately(baseValue, _lastBaseValue)) return _value;

            _lastBaseValue = baseValue;
            _value = CalculateFinalValue();
            _reCalculate = false;
            return _value;
        }
    }

    public CharacterStat()
    {
        _statsModifiers = new List<StatModifier>();
        _readStatModifiers = _statsModifiers.AsReadOnly();
    }

    public CharacterStat(float v, Enums.StatType type) : this()
    {
        baseValue = v;
        statTypeAffected = type;
    }

    public void Reset()
    {
        _statsModifiers.Clear();
        _reCalculate = true;
        _lastBaseValue = float.MinValue;
        _value = baseValue;
    }

    public bool RemoveModifier(StatModifier mod)
    {
        if (_statsModifiers.Remove(mod))
        {
            _reCalculate = true;
            return true;
        }
        return false;
    }

    public void AddModifier(StatModifier mod)
    {
        _reCalculate = true;

        int insertAt = _statsModifiers.Count;
        while (insertAt > 0 && _statsModifiers[insertAt - 1].order > mod.order)
            insertAt--;

        _statsModifiers.Insert(insertAt, mod);
    }

    public bool RemoveAllModifiersFromSource(object source)
    {
        var didRemove = false;
        for (var i = _statsModifiers.Count - 1; i >= 0; i--)
        {
            if (_statsModifiers[i].source != source) continue;

            _reCalculate = true;
            didRemove = true;
            _statsModifiers.RemoveAt(i);
        }
        return didRemove;
    }

    public float CalculateFinalValue()
    {
        var finalValue = baseValue;
        float sumPercentAdd = 0;

        for (var i = 0; i < _statsModifiers.Count; i++)
        {
            var mod = _statsModifiers[i];

            switch (mod.type)
            {
                case StatModifierType.Flat:
                    finalValue += mod.value;
                    break;
                case StatModifierType.PercentAdd:
                {
                    sumPercentAdd += mod.value;
                    if (i + 1 >= _statsModifiers.Count || _statsModifiers[i + 1].type != StatModifierType.PercentAdd)
                    {
                        finalValue *= 1 + sumPercentAdd;
                        sumPercentAdd = 0;
                    }

                    break;
                }
                case StatModifierType.PercentMult:
                    finalValue *= 1 + mod.value;
                    break;
                default:
                    Debug.LogWarning("This stat modifiers is not available");
                    break;
            }
        }
        return (float)Math.Round(finalValue, 4);
    }
}