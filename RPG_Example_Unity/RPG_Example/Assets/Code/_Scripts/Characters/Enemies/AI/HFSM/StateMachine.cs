
// it's sealed because the subclasses can have their own properties and behaviors

public sealed class StateMachine
{
    public IState CurrentState { get; private set; }

    public void Set(IState nextState)
    {
        if (CurrentState == nextState) return;
        
        CurrentState?.OnExit();
        CurrentState = nextState;
        CurrentState?.OnEnter();
    }

    public void Tick(float dt)
    {
        CurrentState?.Tick(dt);
    }

    public void FixedTick(float fDt)
    {
        CurrentState?.FixedTick(fDt);
    }

    public void HandleCommand(ICommand cmd)
    {
        CurrentState?.HandleCommand(cmd);
    }
}
