using UnityEngine;

[CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Expedition/Enemy Definition")]
public class EnemyDefinition : ScriptableObject
{
    [Header("Identidad")] public string id;
    public string displayName;

    [Header("Prefab")] [Tooltip("Prefab que contiene EnemyBase y todos sus componentes asociados.")]
    public GameObject prefab;

    [Header("Stats")] public CharacterBaseStatsSO baseStats;

    [Header("Poise")]
    [Tooltip("Maximum poise. When depleted the enemy enters StunState.")] [Min(1f)]
    public float maxPoise = 100f;

    [Tooltip("Seconds without taking poise damage before regen starts.")] [Min(0f)]
    public float poiseRegenDelay = 3f;

    [Tooltip("Poise units recovered per second after regen delay.")] [Min(0f)]
    public float poiseRegenRate = 20f;

    [Tooltip("Seconds the enemy stays stunned after poise break.")] [Min(0.1f)]
    public float stunDuration = 2f;

    [Tooltip("Poise damage received when the player lands a normal parry on this enemy.")] [Min(0f)]
    public float parryPoiseDamage = 30f;

    [Tooltip("Poise damage received when the player lands a perfect parry on this enemy.")] [Min(0f)]
    public float perfectParryPoiseDamage = 55f;

    [Header("Balance")]
    [Tooltip("Zone this enemy belongs to. Used by the auto balance tool.")]
    [Range(1, 10)] public int zone = 1;
    public Enums.EnemyCategory category = Enums.EnemyCategory.Normal;

    public LootTable lootTable;
}
