
public sealed class CombatWindowExecutor
{
    private AttackData _attack;

    private readonly FastWindowTrackerSimple _dodgeCancel = new();
    private readonly FastWindowTrackerSimple _moveCancel = new();
    private readonly FastWindowTrackerSimple _moveRotation = new();

    public bool DodgeCancelOpen => _dodgeCancel.IsActive;
    public bool MoveCancelOpen => _moveCancel.IsActive;
    public bool MoveRotationOpen => _moveRotation.IsActive;

    public void StartAttack(AttackData attack)
    {
        _attack = attack;

        if (_attack == null)
        {
            StopAttack();
            return;
        }

        if (_attack.allowDodgeCancelWindow)
        {
            _dodgeCancel.Bind(_attack.dodgeCancelWindows);
        }
        else
        {
            _dodgeCancel.Clear();
        }

        if (_attack.allowMoveCancel)
        {
            _moveCancel.Bind(_attack.moveCancelWindows);
        }
        else
        {
            _moveCancel.Clear();
        }

        if (_attack.allowMoveRotation)
        {
            _moveRotation.Bind(_attack.moveRotationWindows);
        }
        else
        {
            _moveRotation.Clear();
        }
    }

    public void StopAttack()
    {
        _attack = null;
        _dodgeCancel.Clear();
        _moveCancel.Clear();
        _moveRotation.Clear();
    }

    public void Tick(float tNorm)
    {
        if (_attack == null) return;

        _dodgeCancel.Tick(tNorm);
        _moveCancel.Tick(tNorm);
        _moveRotation.Tick(tNorm);
    }
}