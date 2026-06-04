using System;
using System.Collections.Generic;
using UnityEngine;

public class LootPoint : Point
{
    [Header("Loot Settings")]
    public List<LootTable> LootTables = new();

    [Tooltip("Prefab de Storage que se spawneará en este punto.")]
    public Storage prefab;

    public GameObject _editor_reference;

    protected override void OnValidate()
    {
        base.OnValidate();
    }

    private void OnDrawGizmos()
    {
        DrawBaseGizmos(Color.yellow, 0.22f, 0.22f);
    }

    private void Awake()
    {
        Destroy(_editor_reference);
    }
}