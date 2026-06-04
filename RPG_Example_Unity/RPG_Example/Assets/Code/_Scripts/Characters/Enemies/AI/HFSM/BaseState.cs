// Base State with generic contect to difference between HSFM for Player or Enemy ctx without duplications

public abstract class BaseState<TContext> : IState where TContext : CharacterContext
{
    protected readonly StateMachine _finiteStateMachine;
    protected readonly TContext _ctx;
    
    protected BaseState(StateMachine fsm, TContext ctx) 
    {
        this._finiteStateMachine = fsm;
        this._ctx = ctx;
    }
    public virtual void OnEnter() { }
    public virtual void OnExit()  { }
    public virtual void Tick(float dt) { }
    public virtual void FixedTick(float fixedDt) { }
    public virtual void HandleCommand(ICommand command) { }
    public virtual void CheckToConsumeActions() {}
}
