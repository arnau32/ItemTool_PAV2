using System;
using UnityEngine;
using UnityEngine.AI;

public sealed class EnemyContext : CharacterContext
{
    public EnemyMovement Movement { get; }
    public NavMeshAgent Agent { get; }
    public EnemyAnimation Animation { get; }
    public ITargetable Targetable { get; }
    public EnemyPerception Perception { get; }
    public EnemyCombatPlanner CombatPlanner { get; }
    public Enums.CombatTemperament Temperament { get; }
    public EnemyBehaviorProfile BehaviorProfile { get; }
    public EnemyCombat EnemyCombat { get; }
    public EnemyStateFactory StateFactory { get; private set; }
    public FactionComponent FactionComponent { get; }
    public EnemyWeaponHandler WeaponHandler { get; }
    public EnemyAudio Audio { get; }
    public EnemyPoiseSystem Poise { get; }
    public EnemyFeedbacksController Feedbacks { get; }
    public Vector3 SpawnPosition { get; private set; }
    public Vector3 PatrolCenter { get; private set; }

    private Action _onDeathComplete;

    internal EnemyContext(EnemyContextBuilder b) : base(b.Owner, b.Stats, b.Health)
    {
        Agent = b.Agent;
        Movement = b.Movement;
        Animation = b.Animation;
        Targetable = b.Targetable;
        Perception = b.Perception;
        CombatPlanner = b.CombatPlanner;
        Temperament = b.Temperament;
        BehaviorProfile = b.BehaviorProfile;
        EnemyCombat = b.EnemyCombat;
        FactionComponent = b.FactionComponent;
        WeaponHandler = b.WeaponHandler;
        Audio = b.Audio;
        Poise = b.Poise;
        Feedbacks = b.Feedbacks;
        SpawnPosition = b.Owner.transform.position;
        PatrolCenter = SpawnPosition;
    }

    public void BindStateFactory(EnemyStateFactory factory) => StateFactory = factory;

    public void ResetSpawnPosition(Vector3 pos)
    {
        SpawnPosition = pos;
        PatrolCenter = pos;
    }

    public void SetPatrolCenter(Vector3 center) => PatrolCenter = center;

    public void BindDeathCallback(Action onDeathComplete) => _onDeathComplete = onDeathComplete;

    public void NotifyDeathComplete() => _onDeathComplete?.Invoke();
}