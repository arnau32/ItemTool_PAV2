
public interface IState
{
    void OnEnter();
    void OnExit();
    void Tick(float dt);
    void FixedTick(float fixedDt);
    void HandleCommand(ICommand command);
}
