public sealed class EnemyStateFactory
{
    public IdleState Idle { get; }
    public PatrollState Patroll { get; }
    public ChasingState Chasing { get; }
    public InvestigateState Investigate { get; }
    public AlertState Alert { get; }
    public CombatWanderState CombatWander { get; }
    public HitState Hit { get; }
    public KnockbackState Knockback { get; }
    public KnockdownState Knockdown { get; }
    public StunState Stun { get; }
    public DeadState Dead { get; }
    public ReturnToSpawnState ReturnToSpawn { get; }
    public SpawnState Spawn { get; }

    public EnemyStateFactory(StateMachine fsm, EnemyContext ctx)
    {
        Idle = new IdleState(fsm, ctx);
        Patroll = new PatrollState(fsm, ctx);
        Chasing = new ChasingState(fsm, ctx);
        Investigate = new InvestigateState(fsm, ctx);
        Alert = new AlertState(fsm, ctx);
        CombatWander = new CombatWanderState(fsm, ctx);
        Hit = new HitState(fsm, ctx);
        Knockback = new KnockbackState(fsm, ctx);
        Knockdown = new KnockdownState(fsm, ctx);
        Stun = new StunState(fsm, ctx);
        Dead = new DeadState(fsm, ctx);
        ReturnToSpawn = new ReturnToSpawnState(fsm, ctx);
        Spawn = new SpawnState(fsm, ctx);
    }

    public IState GetInitialState(bool startPatrolling)
    {
        return startPatrolling ? Patroll : Idle;
    }
}