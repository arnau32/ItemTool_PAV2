using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ParryController
{
    #region Fields

    private PlayerContext _context;
    private Action<string, float> _playAnim;

    private IVfxUser _vfxUser;
    private WeaponAudio _weaponAudio;
    private ParryData _currentParry;

    private float _timeNormalized;
    private float _rawTimeNormalized;
    private bool _hasValidParryState;

    private float _parryStartTime;

    private float _nextAllowedTime;
    private float _bufferedUntil;
    private bool _bufferedRequest;

    private bool _parrySucceededThisAttempt;

    private bool _dodgeCancelOpen;
    private bool _moveCancelOpen;
    private bool _attackCancelOpen;

    // Cancel-movement window state (does NOT end the parry)
    private bool _moveCancelWindowOpen;
    private bool _movementControlReleased;

    private float _lastParrySpeed = 1f;
    private static readonly int HashParrySpeedParameter = Animator.StringToHash("ParrySpeed");

    // Tracks ALL VFX that were activated.
    private readonly HashSet<int> _enabledVfx = new HashSet<int>();

    // Tracks only VFX that have disableOnEnd=true and should be force-disabled on StopParry.
    private readonly HashSet<int> _forceDisableVfx = new HashSet<int>();

    private FastWindowExecutor64<AttackVfxWindow, ParryVfxWindowHandler> _vfxWindows;

    private readonly FastWindowTrackerSimple _moveRotation = new();

    [NonSerialized] private ParryData _bufferedParryData;

    #endregion

    #region Properties

    public bool IsParrying => _currentParry != null;
    public float CurrentNormalizedTime => _timeNormalized;

    public bool CanDodgeCancel() => _currentParry != null && _dodgeCancelOpen;
    public bool CanMoveCancel() => _currentParry != null && _moveCancelOpen;
    public bool CanRotateDuringParry() => _currentParry != null && _moveRotation.IsActive;
    public bool CanAttackCancel() => _currentParry != null && _attackCancelOpen;

    public bool MoveCancelWindowOpen => _moveCancelWindowOpen;
    public bool MovementControlReleased => _movementControlReleased;

    #endregion

    #region Initialization

    public void Initialize(PlayerContext context, IVfxUser vfxUser, WeaponAudio weaponAudio = null)
    {
        _context = context;
        _playAnim = _context.Animation.PlayTargetAnimation;
        _vfxUser = vfxUser;
        _vfxWindows = new FastWindowExecutor64<AttackVfxWindow, ParryVfxWindowHandler>(new ParryVfxWindowHandler(_vfxUser, _enabledVfx, _forceDisableVfx));
    }

    #endregion

    #region Public API

    public void Tick(bool isGrounded)
    {
        if (_currentParry == null)
        {
            if (_bufferedRequest && Time.time <= _bufferedUntil && Time.time >= _nextAllowedTime)
            {
                TryStartBuffered();
            }
            else if (_bufferedRequest && Time.time > _bufferedUntil)
            {
                _bufferedRequest = false;
            }

            return;
        }

        if (!isGrounded)
        {
            StopParry();
            return;
        }

        var animator = _context != null && _context.Animation != null ? _context.Animation.Animator : null;
        if (animator == null)
        {
            StopParry();
            return;
        }

        if (TryGetParryNormalizedTime(animator, out var n, out var raw))
        {
            _hasValidParryState = true;
            _timeNormalized = n;
            _rawTimeNormalized = raw;
        }
        else
        {
            if (!_hasValidParryState)
            {
                float maxWait = Mathf.Max(_currentParry != null ? _currentParry.crossFade : 0f, 0.05f) + 0.10f;
                bool inTransition = animator.IsInTransition(PlayerAnimHashes.LayerOverride);

                if (!inTransition && (Time.time - _parryStartTime) >= maxWait)
                    StopParry();

                return;
            }

            StopParry();
            return;
        }

        _vfxWindows.Tick(_timeNormalized);

        if (_currentParry.allowMoveRotation && _currentParry.moveRotationWindows != null)
            _moveRotation.Tick(_timeNormalized);
        else
            _moveRotation.Clear();

        UpdateCancelWindows();

        _moveCancelWindowOpen = _moveCancelOpen;

        if (_moveCancelWindowOpen && _context != null && _context.Inputs != null && _context.Inputs.IsMoving())
            _movementControlReleased = true;

        ApplyParryAnimatorSpeed(animator);

        if (_rawTimeNormalized >= 1f && !animator.IsInTransition(PlayerAnimHashes.LayerOverride))
            StopParry();
    }

    public bool TryStartParry(ParryData parry)
    {
        if (_context == null || parry == null) return false;

        if (Time.time < _nextAllowedTime)
        {
            BufferParry(parry);
            return false;
        }

        if (_context.Movement != null && _context.Movement.DodgeSystem != null && _context.Movement.DodgeSystem.IsInDodge)
            return false;

        if (!ConsumeStamina(parry.staminaCost))
            return false;

        StopParry();

        if (!PlayParryAnimation(parry)) return false;

        StartForNewParry(parry);

        _nextAllowedTime = Time.time + Mathf.Max(0f, parry.cooldownSeconds);

        // Attempt audio and effects live in _feedbackParryAttempt in PlayerFeedbacksController.
        _context.FeedbacksController?.PlayParryAttempt();

        return true;
    }

    public (bool active, Enums.ParryQuality quality) GetParryState()
    {
        if (_currentParry == null) return (false, Enums.ParryQuality.None);
        return _currentParry.IsParryActive(_timeNormalized);
    }

    public void NotifyParryResult(bool success)
    {
        if (_currentParry == null) return;

        if (success)
        {
            _parrySucceededThisAttempt = true;
            // Audio is handled by PlayerFeedbacksController via PlayParryReceived.
        }

        var extra = success ? _currentParry.successLockoutSeconds : _currentParry.failLockoutSeconds;
        if (extra > 0f)
            _nextAllowedTime = Mathf.Max(_nextAllowedTime, Time.time + extra);
    }

    public void ForceStop()
    {
        StopParry();
        _bufferedRequest = false;
    }

    #endregion

    #region Internal

    private void UpdateCancelWindows()
    {
        if (_currentParry == null)
        {
            _dodgeCancelOpen = false;
            _moveCancelOpen = false;
            _attackCancelOpen = false;
            return;
        }

        bool permaDodgeCancel = _currentParry.permaDodgeCancelWindow.IsActive(_timeNormalized);
        bool permaMoveCancel = _currentParry.permaMoveCancelWindow.IsActive(_timeNormalized);

        bool successDodgeCancel = _parrySucceededThisAttempt && _currentParry.allowDodgeCancelWindow && _currentParry.dodgeCancelWindow.IsActive(_timeNormalized);
        bool successMoveCancel = _parrySucceededThisAttempt && _currentParry.allowMoveCancel && _currentParry.moveCancelWindow.IsActive(_timeNormalized);

        _dodgeCancelOpen = permaDodgeCancel || successDodgeCancel;
        _moveCancelOpen = permaMoveCancel || successMoveCancel;
        _attackCancelOpen = _currentParry.allowAttackCancel && _currentParry.attackCancelWindows.IsActive(_timeNormalized);
    }

    private void ApplyParryAnimatorSpeed(Animator animator)
    {
        if (animator == null) return;

        float speed = _currentParry != null ? _currentParry.EvaluateAnimatorSpeed(_timeNormalized) : 1f;
        speed = Mathf.Max(0.01f, speed);

        if (Mathf.Abs(_lastParrySpeed - speed) < 0.001f) return;

        _lastParrySpeed = speed;
        animator.SetFloat(HashParrySpeedParameter, speed);
    }

    private void ResetParryAnimatorSpeed(Animator animator)
    {
        if (animator == null) return;
        if (Mathf.Abs(_lastParrySpeed - 1f) < 0.001f) return;

        _lastParrySpeed = 1f;
        animator.SetFloat(HashParrySpeedParameter, 1f);
    }

    private void BufferParry(ParryData parry)
    {
        _bufferedParryData = parry;

        var buffer = Mathf.Max(0f, parry.inputBufferSeconds);
        if (buffer <= 0f) return;

        _bufferedRequest = true;
        _bufferedUntil = Time.time + buffer;
    }

    private void TryStartBuffered()
    {
        if (!_bufferedRequest) return;

        if (_bufferedParryData == null || Time.time > _bufferedUntil)
        {
            _bufferedRequest = false;
            return;
        }

        if (Time.time < _nextAllowedTime) return;

        _bufferedRequest = false;
        TryStartParry(_bufferedParryData);
    }

    private bool ConsumeStamina(float amount)
    {
        if (_context == null || _context.Stamina == null) return false;
        return _context.Stamina.TryConsumeStamina(amount);
    }

    private bool PlayParryAnimation(ParryData parry)
    {
        if (_playAnim == null)
        {
            if (_context == null || _context.Animation == null) return false;
            _context.Animation.PlayTargetAnimation(parry.triggerAnimation, parry.crossFade);
            return true;
        }

        _playAnim.Invoke(parry.triggerAnimation, parry.crossFade);
        return true;
    }

    private void StartForNewParry(ParryData parry)
    {
        _currentParry = parry;
        _timeNormalized = 0f;
        _rawTimeNormalized = 0f;
        _hasValidParryState = false;
        _parryStartTime = Time.time;
        _parrySucceededThisAttempt = false;
        _dodgeCancelOpen = false;
        _moveCancelOpen = false;
        _attackCancelOpen = false;
        _moveCancelWindowOpen = false;
        _movementControlReleased = false;
        _enabledVfx.Clear();
        _forceDisableVfx.Clear();

        if (_currentParry != null && _currentParry.vfxWindows != null && _currentParry.vfxWindows.Count > 0)
            _vfxWindows.Bind(_currentParry.vfxWindows);
        else
            _vfxWindows.Clear();

        if (_currentParry != null && _currentParry.allowMoveRotation && _currentParry.moveRotationWindows != null)
            _moveRotation.Bind(_currentParry.moveRotationWindows);
        else
            _moveRotation.Clear();

        if (_context != null && _context.Animation != null)
            ResetParryAnimatorSpeed(_context.Animation.Animator);
    }

    private void StopParry()
    {
        if (_currentParry == null) return;

        CombatAnalytics.ParryAttempted(_parrySucceededThisAttempt, _currentParry.staminaCost);

        foreach (var id in _forceDisableVfx)
            _vfxUser?.DeactiveAttack(id);

        _enabledVfx.Clear();
        _forceDisableVfx.Clear();
        _vfxWindows.Clear();
        _moveRotation.Clear();
        _parrySucceededThisAttempt = false;
        _dodgeCancelOpen = false;
        _moveCancelOpen = false;
        _attackCancelOpen = false;
        _moveCancelWindowOpen = false;
        _movementControlReleased = false;

        if (_context != null && _context.Animation != null)
            ResetParryAnimatorSpeed(_context.Animation.Animator);

        _currentParry = null;
        _timeNormalized = 0f;
        _rawTimeNormalized = 0f;
        _hasValidParryState = false;
    }

    private bool TryGetParryNormalizedTime(Animator animator, out float tNorm, out float rawNorm)
    {
        tNorm = 0f;
        rawNorm = 0f;

        if (_currentParry == null) return false;

        var targetClip = _currentParry.animationClip;
        if (targetClip == null) return false;

        return AnimatorClipTimeUtils.TryGetClipNormalizedTime(animator, PlayerAnimHashes.LayerOverride, targetClip, out tNorm, out rawNorm);
    }

    private struct ParryVfxWindowHandler : IWindowHandler<AttackVfxWindow>
    {
        private readonly IVfxUser _vfx;
        private readonly HashSet<int> _enabled;
        private readonly HashSet<int> _forceDisable;

        public ParryVfxWindowHandler(IVfxUser vfx, HashSet<int> enabled, HashSet<int> forceDisable)
        {
            _vfx = vfx;
            _enabled = enabled;
            _forceDisable = forceDisable;
        }

        public WindowEvent GetEvent(in AttackVfxWindow window) => window.window;

        public void OnEnter(in AttackVfxWindow window, int index)
        {
            _vfx?.ActivateAttack(window.vfxId);
            _enabled.Add(window.vfxId);
            if (window.disableOnEnd) _forceDisable.Add(window.vfxId);
        }

        public void OnExit(in AttackVfxWindow window, int index)
        {
            if (!window.disableOnEnd) return;
            _vfx?.DeactiveAttack(window.vfxId);
            _enabled.Remove(window.vfxId);
            _forceDisable.Remove(window.vfxId);
        }
    }

    #endregion
}