using UnityEngine;
using UnityEngine.AI;

public sealed class EnemyContextBuilder : CharacterContextBuilder<EnemyContextBuilder>
{
    public NavMeshAgent Agent { get; private set; }
    public EnemyMovement Movement { get; private set; }
    public EnemyAnimation Animation { get; private set; }
    public ITargetable Targetable { get; private set; }
    public EnemyPerception Perception { get; private set; }
    public EnemyCombatPlanner CombatPlanner { get; private set; }
    public Enums.CombatTemperament Temperament { get; private set; }
    public EnemyBehaviorProfile BehaviorProfile { get; private set; }
    public EnemyCombat EnemyCombat { get; private set; }
    public EnemyWeaponHandler WeaponHandler { get; private set; }
    public EnemyAudio Audio { get; private set; }
    public EnemyPoiseSystem Poise { get; private set; }
    public EnemyFeedbacksController Feedbacks { get; private set; }

    public EnemyContextBuilder(GameObject owner, CharacterStats stats, CharacterHealthSystem health) : base(owner, stats, health)
    {
    }

    public EnemyContextBuilder WithAgent(NavMeshAgent agent)
    {
        Agent = agent;
        return this;
    }

    public EnemyContextBuilder WithMovement(EnemyMovement movement)
    {
        Movement = movement;
        return this;
    }

    public EnemyContextBuilder WithAnimation(EnemyAnimation animation)
    {
        Animation = animation;
        return this;
    }

    public EnemyContextBuilder WithTargetable(ITargetable targetable)
    {
        Targetable = targetable;
        return this;
    }

    public EnemyContextBuilder WithPerception(EnemyPerception perception)
    {
        Perception = perception;
        return this;
    }

    public EnemyContextBuilder WithCombatPlanner(EnemyCombatPlanner planner)
    {
        CombatPlanner = planner;
        return this;
    }

    public EnemyContextBuilder WithTemperament(Enums.CombatTemperament temperament)
    {
        Temperament = temperament;
        return this;
    }

    public EnemyContextBuilder WithBehaviorProfile(EnemyBehaviorProfile profile)
    {
        BehaviorProfile = profile;
        return this;
    }

    public EnemyContextBuilder WithCombat(EnemyCombat combat)
    {
        EnemyCombat = combat;
        return this;
    }

    public EnemyContextBuilder WithWeaponHandler(EnemyWeaponHandler handler)
    {
        WeaponHandler = handler;
        return this;
    }

    public EnemyContextBuilder WithAudio(EnemyAudio audio)
    {
        Audio = audio;
        return this;
    }

    public EnemyContextBuilder WithPoise(EnemyPoiseSystem poise)
    {
        Poise = poise;
        return this;
    }

    public EnemyContextBuilder WithFeedbacks(EnemyFeedbacksController feedbacks)
    {
        Feedbacks = feedbacks;
        return this;
    }

    public EnemyContext Build() => new(this);
}