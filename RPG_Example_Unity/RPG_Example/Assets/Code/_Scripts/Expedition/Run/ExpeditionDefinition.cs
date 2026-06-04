using System.Collections.Generic;
using UnityEngine;
using Gameplay.Enemies;

[CreateAssetMenu(fileName = "ExpeditionDefinition", menuName = "Expedition/Expedition Definition")]
public class ExpeditionDefinition : ScriptableObject
{
    [Header("Información")]
    public string expeditionId;
    public string displayName;

    [Header("Enemigos iniciales")]
    [Tooltip("Número mínimo de enemigos que se intentarán spawnear al inicio.")]
    public int minEnemyCount = 10;

    [Tooltip("Número máximo de enemigos que se intentarán spawnear al inicio.")]
    public int maxEnemyCount = 20;

    [Tooltip("Tipos de enemigo que se pueden usar por defecto si un punto no define los suyos.")]
    public List<EnemyDefinition> defaultAllowedEnemies;

    [Tooltip("Tipos de enemigo que se pueden usar por defecto si un punto no define los suyos.")]
    public List<LootTable> defaultLootTables;
}
