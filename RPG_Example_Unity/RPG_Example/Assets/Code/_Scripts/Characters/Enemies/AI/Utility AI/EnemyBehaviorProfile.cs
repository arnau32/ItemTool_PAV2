using UnityEngine;

[CreateAssetMenu(fileName = "BehaviorProfile", menuName = "Enemies/EnemyBehaviorProfile", order = 0)]
public class EnemyBehaviorProfile : ScriptableObject
{
    public bool startPatrolling = true;
    public bool canChase = true;
    public bool canEnterCombat = true;
    public bool reactsToSound = true;

    [Tooltip("On Disables enemy will be sparring dummy")]
    public bool usesCombatPlanner = true;

    [Header("Area Patrol")]
    [Tooltip("If true, the enemy patrols random points within patrolRadius around its patrol center " +
             "(SpawnPosition by default, or overridden at spawn time). After losing the player it returns to that area before resuming patrol.")]
    public bool useAreaPatrol = false;

    [Tooltip("Radius of the patrol area around the patrol center.")] [Min(1f)]
    public float patrolRadius = 8f;

    public float maxChaseDistance = 25f;

    [Header("Multi-Enemy Separation")] [Tooltip("Radius to detect nearby allied enemies for chase and orbit separation. Range: 1.5–4.0")] [Min(0f)]
    public float separationRadius = 2.5f;

    [Tooltip("Layer mask for allied enemy colliders. Used by chase separation and orbit direction.")]
    public LayerMask enemyLayer;

    [Tooltip(
        "Probability [0-1] that this enemy will try to intercept the player's path instead of chasing directly. 0 = always chase, 1 = always intercept. Recommended: 0.3-0.5.")]
    [Range(0f, 1f)]
    public float interceptChance = 0.35f;

    [Tooltip("Seconds of player movement to predict when intercepting.")] [Min(0.1f)]
    public float interceptPredictionTime = 0.7f;

    [Header("Idle Routines")] [Tooltip("Weighted list of behaviours the enemy picks between patrol destinations. Leave empty to keep the original continuous-patrol behaviour.")]
    public IdleRoutine[] idleRoutines;

    [Tooltip("Degrees per second the parent transform rotates during a LookAround turn animation. Adjust to match the visual animation.")]
    [Min(0f)]
    public float turnRotationSpeed = 90f;

    [Tooltip("Search radius around the agent when looking for InteractPoints nearby.")] [Min(1f)]
    public float interactPointSearchRadius = 6f;

}