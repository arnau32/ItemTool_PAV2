using System;

// Manages sprint state machine with combat-conditional stamina drain.
public sealed class SprintHandler
{
    private enum SprintState : byte
    {
        Idle,       // Not sprinting, no request
        Requested,  // Button pressed, waiting to start
        Active,     // Currently sprinting
        Exhausted   // Out of stamina while still holding the button
    }

    private readonly float _minStaminaToStart;
    private readonly float _minStaminaToKeep;

    private SprintState _state = SprintState.Idle;
    private bool _isPressed;

    // Cached reference to avoid lambda/delegate allocation on state transitions
    private Func<bool> _cachedCombatCheck;

    public bool IsSprinting => _state == SprintState.Active;
    public bool HasRequest => _state != SprintState.Idle;
    public bool IsPressed => _isPressed;

    public SprintHandler(float minStaminaToStart, float minStaminaToKeep)
    {
        _minStaminaToStart = minStaminaToStart;
        _minStaminaToKeep = minStaminaToKeep;
    }

    public void HandleInput(bool pressed, PlayerContext ctx, bool canSprintNow)
    {
        _isPressed = pressed;

        if (!pressed)
        {
            TransitionTo(SprintState.Idle, ctx);
            return;
        }

        // Button just pressed - evaluate if we can start
        switch (_state)
        {
            case SprintState.Exhausted:
                // Can only restart if we have enough stamina again
                if (ctx.Stamina.CanStartSprint(_minStaminaToStart))
                {
                    TransitionTo(canSprintNow ? SprintState.Active : SprintState.Requested, ctx);
                }
                break;

            default:
                // Try to activate or queue request
                if (canSprintNow && ctx.Stamina.CanStartSprint(_minStaminaToStart))
                {
                    TransitionTo(SprintState.Active, ctx);
                }
                else
                {
                    TransitionTo(SprintState.Requested, ctx);
                }
                break;
        }
    }

    public void Tick(PlayerContext ctx, bool canSprintNow, Func<bool> isInCombatFunc)
    {
        // Cache the delegate reference for use in state transitions (no allocation)
        _cachedCombatCheck = isInCombatFunc;

        if (_state == SprintState.Idle) return;

        // Button released - cancel everything
        if (!_isPressed)
        {
            TransitionTo(SprintState.Idle, ctx);
            return;
        }

        switch (_state)
        {
            case SprintState.Exhausted:
                HandleExhaustedState(ctx, canSprintNow);
                break;

            case SprintState.Requested:
                HandleRequestedState(ctx, canSprintNow);
                break;

            case SprintState.Active:
                HandleActiveState(ctx, canSprintNow, isInCombatFunc);
                break;
        }
    }

    public void CancelRequest(PlayerContext ctx)
    {
        _isPressed = false;
        TransitionTo(SprintState.Idle, ctx);
    }

    public void SuspendSprintingKeepRequest(PlayerContext ctx)
    {
        if (_state == SprintState.Idle) return;
        TransitionTo(_isPressed ? SprintState.Requested : SprintState.Idle, ctx);
    }

    #region State Handlers

    private void HandleExhaustedState(PlayerContext ctx, bool canSprintNow)
    {
        // Check if stamina recovered enough to restart
        if (ctx.Stamina.CanStartSprint(_minStaminaToStart) && canSprintNow)
        {
            TransitionTo(SprintState.Active, ctx);
        }
    }

    private void HandleRequestedState(PlayerContext ctx, bool canSprintNow)
    {
        // Try to activate sprint if conditions are met
        if (canSprintNow && ctx.Stamina.CanStartSprint(_minStaminaToStart))
        {
            TransitionTo(SprintState.Active, ctx);
        }
    }

    private void HandleActiveState(PlayerContext ctx, bool canSprintNow, Func<bool> isInCombatFunc)
    {
        // Check if we can still sprint (grounded, not attacking, etc)
        if (!canSprintNow)
        {
            TransitionTo(SprintState.Requested, ctx);
            return;
        }

        // Check if stamina drain stopped (hit 0 or external stop)
        // The stamina system auto-stops drain at 0, or when combat ends if onlyInCombat=true
        if (ctx.Stamina.IsDraining) return;
        
        // Check if we're out of stamina or just not draining due to combat state
        if (ctx.Stamina.CurrentStamina <= _minStaminaToKeep)
        {
            TransitionTo(SprintState.Exhausted, ctx);
        }
        
        // If stamina is fine but drain stopped, it means we left combat
        // Restart drain (it will check combat state internally)
        else if (isInCombatFunc != null)
        {
            ctx.Stamina.StartDrain(onlyInCombat: true, combatCheck: isInCombatFunc);
        }
    }

    #endregion

    private void TransitionTo(SprintState newState, PlayerContext ctx)
    {
        if (_state == newState) return;

        if (_state == SprintState.Active)
        {
            ctx.Stamina.StopDrain();
            ctx.FeedbacksController.PlaySprintStop();
        }

        _state = newState;

        if (_state != SprintState.Active) return;

        ctx.Stamina.StartDrain(onlyInCombat: true, combatCheck: _cachedCombatCheck);
        ctx.FeedbacksController.PlaySprintStart();
    }

}