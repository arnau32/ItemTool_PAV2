using System.Collections.Generic;
using UnityEngine;
using Gameplay.Enemies;

public class EnemyPoint : Point
{
    [Header("Enemy Settings")]
    [Tooltip("Si se deja vacío, se usan los enemigos por defecto de la ExpeditionDefinition.")]
    public List<EnemyDefinition> Enemies = new();

    [Tooltip("Permite que este punto pueda generar varios enemigos.")]
    public bool allowMultipleEnemies = false;

    [Min(1)]
    [Tooltip("Número máximo de enemigos que puede spawnear este punto.")]
    public int maxEnemies = 1;

    [Min(0f)]
    [Tooltip("Radio en el que se dispersan los enemigos alrededor del punto.")]
    public float spawnRadius = 0.25f;

    [Tooltip("Si está activado, este punto SIEMPRE se usa.")]
    public bool forceSpawn = false;

    [Tooltip("Si está activado y este punto se usa, spawnea SIEMPRE el máximo.")]
    public bool forceMax = false;

    protected override void OnValidate()
    {
        base.OnValidate();

        maxEnemies = Mathf.Max(1, maxEnemies);
        spawnRadius = Mathf.Max(0f, spawnRadius);

        if (!allowMultipleEnemies)
            maxEnemies = 1;
    }

    private void OnDrawGizmos()
    {
        float innerSphere = 0.22f;
        float size = spawnRadius > 0f ? spawnRadius : innerSphere;

        DrawBaseGizmos(Color.red, size, innerSphere);
    }
}