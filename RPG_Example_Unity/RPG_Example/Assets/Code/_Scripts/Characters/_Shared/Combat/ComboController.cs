using System;
using UnityEngine;

[Serializable]
public class ComboController
{
    // [DEBUG] Set to true to log the full combo decision chain.
    private const bool DEBUG_COMBO = false;
    // [DEBUG] Set to true to log damage window open/close events and clip tracking.
    private const bool DEBUG_WINDOWS = false;
    private static string D => "[COMBO DBG]";

    #region Fields

    private ICombatContext _context;
    private readonly ComboResolver _resolver = new();
    private readonly InputBuffer _inputBuffer = new();
    
    private float _timeNormalized;
    private float _nextComboTimeNormalized;
    
    private Action<string, float> _playAnim;

    private AttackWindowExecutor _attackWindows;
    private CombatWindowExecutor _combatWindowExecutor;
    private VfxWindowExecutor _vfxWindows;
    private WeaponVfxWindowExecutor _weaponVfxWindows;
    private AudioWindowExecutor _audioWindows;
    
    private AttackData _currentAttack;
    
    private bool _hasValidAttackState;
    
    private bool _waitingForWindow;
    private bool _windowOpen;
    private bool _hasHitThisAttack;
    private bool _bufferedInsideComboWindow;
    private bool _isSkillAttack;

    private CombatAnalyticsService _combatAnalytics;
    
    private Animator _cachedAnimator;
    private AnimationClip _cachedTargetClip;

    #endregion

    #region Properties

    public AttackData CurrentAttack => _currentAttack;
    public bool IsInCombo => _currentAttack != null;
    public float CurrentNormalizedTime => _timeNormalized;
    public bool IsSkillAttack => _isSkillAttack;
    public bool IsComboWindowOpen => _windowOpen;

    #endregion

    #region Initialization

    public void Initialize(ICombatContext context, IVfxUser vfxUser, IWeaponVfxUser weaponVfxUser)
    {
        _context = context;
        _playAnim = _context.Animation.PlayTargetAnimation;
        
        var attackerId = ResolveAttackerIdFromVfxUser(vfxUser);
        _attackWindows = new AttackWindowExecutor(_context.Weapons, attackerId);
        _vfxWindows = new VfxWindowExecutor(vfxUser);
        _weaponVfxWindows = new WeaponVfxWindowExecutor(weaponVfxUser);
        _combatWindowExecutor = new CombatWindowExecutor();
        _combatAnalytics = GameServices.Get<CombatAnalyticsService>();
        
        var ownerTransform = (vfxUser is Component c) ? c.transform : null;
        _audioWindows = new AudioWindowExecutor(ownerTransform);
    }
    
    private static int ResolveAttackerIdFromVfxUser(IVfxUser vfxUser)
    {
        if (!(vfxUser is Component asComponent)) return 0;
        return asComponent.transform != null ? asComponent.transform.GetInstanceID() : 0;
    }

    #endregion

    #region Public API

    public void UpdateComboController(bool isGrounded)
    {
        if (_currentAttack == null) return;

        if (!isGrounded)
        {
            ResetCombo();
            return;
        }

        if (!TryGetAttackNormalizedTime(out var normalizedTime, out bool foundInOutgoing))
        {
            if (_hasValidAttackState)
            {
                // Clip left the Animator (animation finished before nextComboTimeNormalized).
                // If there is a buffered input, fire it now — timeToChangeAnim no longer applies.
                var prevAttack = _currentAttack;
                if (_waitingForWindow && _bufferedInsideComboWindow)
                {
                    if (DEBUG_COMBO) Debug.Log($"{D} UpdateCombo — clip expiró con input bufferizado. Intentando combo anticipado.");
                    TryStartCombo();
                }

                if (_currentAttack == prevAttack)
                    ResetCombo();
            }

            return;
        }

        if (foundInOutgoing)
        {
            // Clip is blending out to idle/recovery but is still playing.
            // Tick damage windows so active windows can fire — skip combo logic.
            // Guard: only valid if the clip was confirmed active first (_hasValidAttackState).
            // Without this, a new attack whose clip matches the previous outgoing clip would
            // tick windows with the old clip's tNorm (~1.0), firing/closing windows incorrectly.
            if (_hasValidAttackState)
            {
                _timeNormalized = normalizedTime;
                TickWindows();
            }
            return;
        }

        _hasValidAttackState = true;
        _timeNormalized = normalizedTime;

        TickWindows();
        ProcessComboLogic();
    }

    public bool IsHitBoxOpen()
    {
        if (_currentAttack == null) return false;
        var windows = _currentAttack.damageWindows;
        
        for (int i = 0; i < windows.Count; i++)
        {
            if (windows[i].window.IsActive(_timeNormalized)) return true;
        }
        return false;
    }

    public float GetWindUp()
    {
        if (_currentAttack == null || !_context.Animation.IsAttacking) return 0f;

        float firstStart = GetFirstHitWindowStart();
        return _timeNormalized < firstStart ? Mathf.Clamp01(_timeNormalized / firstStart) : 0f;
    }

    public float GetTargetRecovery()
    {
        if (_currentAttack == null) return 0f;

        float lastEnd = GetLastHitWindowEnd();
        return _timeNormalized > lastEnd 
            ? Mathf.Clamp01((_timeNormalized - lastEnd) * 1.5f) 
            : 0f;
    }

    public void SetWeapon(WeaponData weapon) => _resolver.currentWeapon = weapon;

    public void RegisterHit()
    {
        _hasHitThisAttack = true;
    }

    public void AddInputToSequence(Enums.AttackInputs input)
    {
        // If no active attack, start a new combo immediately
        if (_currentAttack == null)
        {
            if (DEBUG_COMBO) Debug.Log($"{D} INPUT '{input}' — sin ataque activo, iniciando combo.");
            _inputBuffer.Register(input);
            TryStartCombo();
            return;
        }

        bool usesCombo      = _currentAttack.usesCombo;
        bool windowActive   = _currentAttack.comboWindow.IsActive(_timeNormalized);
        bool comboWindowOpen = usesCombo && windowActive;

        if (DEBUG_COMBO) Debug.Log(
            $"{D} INPUT '{input}' | ataque='{_currentAttack.name}' | tNorm={_timeNormalized:F3} " +
            $"| usesCombo={usesCombo} | ventana=[{_currentAttack.comboWindow.start:F2}-{_currentAttack.comboWindow.end:F2}] " +
            $"| ventanaActiva={windowActive} | buffered={comboWindowOpen}");

        if (comboWindowOpen)
        {
            _inputBuffer.Register(input);
            _bufferedInsideComboWindow = true;
        }

        _waitingForWindow = true;
    }

    public void OnAnimationAttackingChanged(bool isAttacking)
    {
        if (!isAttacking && _currentAttack == null)
            ResetCombo();
    }

    public void ResetCombo()
    {
        if (_currentAttack != null)
            _combatAnalytics.PlayerAttackResolved(_currentAttack, _hasHitThisAttack);

        _attackWindows.StopAttack();
        _combatWindowExecutor.StopAttack();
        _vfxWindows.StopAttack();
        _weaponVfxWindows.StopAttack();
        _audioWindows.StopAttack();
        
        _resolver.ResetCombo();
        _inputBuffer.Clear();

        _currentAttack = null;
        _timeNormalized = 0f;
        _nextComboTimeNormalized = 0f;
        _hasValidAttackState = false;
        _windowOpen = false;
        _waitingForWindow = false;
        _hasHitThisAttack = false;
        _bufferedInsideComboWindow = false;
        _isSkillAttack = false;
        
        _cachedAnimator = null;
        _cachedTargetClip = null;
    }

    public bool CanDodgeCancel() => _currentAttack != null && _combatWindowExecutor.DodgeCancelOpen;

    // NOTE: If you want "move cancel only after hit", add: && _hasHitThisAttack
    public bool CanMoveDuringAttack() => _currentAttack != null && _combatWindowExecutor.MoveCancelOpen;

    public bool CanRotateDuringAttack() => _currentAttack != null && _combatWindowExecutor.MoveRotationOpen;

    public void ForceStartAttack(AttackData attack)
    {
        ResetCombo();
        attack.Execute(_context.Animation.PlayTargetAnimation);
        ResetForNewAttack(attack, Enums.AttackInputs.Skill);
        _isSkillAttack = true;
    }

    #endregion

    #region Internal Logic

    private void TickWindows()
    {
        bool usesCombo = _currentAttack.usesCombo;
        _windowOpen = usesCombo && _currentAttack.comboWindow.IsActive(_timeNormalized);
        
        _vfxWindows.Tick(_timeNormalized);
        _attackWindows.Tick(_timeNormalized);
        _combatWindowExecutor.Tick(_timeNormalized);
        _weaponVfxWindows.Tick(_timeNormalized);
        _audioWindows.Tick(_timeNormalized);

        if (_windowOpen && _waitingForWindow)
        {
            _bufferedInsideComboWindow = true;
        }
    }

    private void ProcessComboLogic()
    {
        bool timeOk    = _timeNormalized >= _nextComboTimeNormalized;
        bool canTakeNext = timeOk && _bufferedInsideComboWindow && _waitingForWindow;

        if (DEBUG_COMBO && (_waitingForWindow || _bufferedInsideComboWindow)) Debug.Log(
            $"{D} PROCESS | tNorm={_timeNormalized:F3} nextComboT={_nextComboTimeNormalized:F3} " +
            $"| timeOk={timeOk} buffered={_bufferedInsideComboWindow} waiting={_waitingForWindow} " +
            $"→ canTakeNext={canTakeNext}");

        if (canTakeNext)
        {
            TryStartCombo();
            return;
        }

        if (_timeNormalized >= 1f)
        {
            if (DEBUG_COMBO) Debug.Log($"{D} RESET — animación completada (tNorm>=1). ataque='{_currentAttack?.name}'");
            ResetCombo();
        }
    }

    private void TryStartCombo()
    {
        if (!_inputBuffer.TryConsume(out var input))
        {
            if (DEBUG_COMBO) Debug.Log($"{D} TryStartCombo — buffer vacío, nada que ejecutar.");
            return;
        }

        if (DEBUG_COMBO) Debug.Log($"{D} TryStartCombo — consumiendo input '{input}' del buffer.");

        var next = _resolver.ResolveNextAttack(input);
        if (next == null)
        {
            if (DEBUG_COMBO) Debug.Log($"{D} TryStartCombo — resolver no encontró siguiente ataque para '{input}'. Combo cortado.");
            _waitingForWindow = false;
            _bufferedInsideComboWindow = false;
            return;
        }

        if (DEBUG_COMBO) Debug.Log($"{D} TryStartCombo — siguiente ataque: '{next.name}' | stamina cost={next.staminaCost}");

        if (!_context.Stamina.TryConsumeStamina(next.staminaCost))
        {
            if (DEBUG_COMBO) Debug.Log($"{D} TryStartCombo — STAMINA INSUFICIENTE para '{next.name}'. ResetCombo.");
            ResetCombo();
            return;
        }

        bool animOk = next.Execute(_playAnim);
        if (DEBUG_COMBO) Debug.Log($"{D} TryStartCombo — Execute('{next.name}') = {animOk}");

        if (!animOk) return;

        ResetForNewAttack(next, input);
    }

    private void ResetForNewAttack(AttackData attack, Enums.AttackInputs? inputType = null)
    {
        _combatAnalytics.PlayerAttackStarted(attack, inputType);

        _combatWindowExecutor.StartAttack(attack);
        _attackWindows.StartAttack(attack);
        _vfxWindows.StartAttack(attack);
        _weaponVfxWindows.StartAttack(attack);
        _audioWindows.StartAttack(attack);

        _currentAttack = attack;
        _nextComboTimeNormalized = Mathf.Clamp01(attack.timeToChangeAnim);
        _hasValidAttackState = false;
        _waitingForWindow = false;
        _windowOpen = false;
        _hasHitThisAttack = false;
        _bufferedInsideComboWindow = false;

        _cachedAnimator = _context.Animation.Animator;
        _cachedTargetClip = attack.animationClip;

        if (DEBUG_WINDOWS)
        {
            int windowCount = attack.damageWindows?.Count ?? 0;
            string clipName  = _cachedTargetClip != null ? _cachedTargetClip.name : "NULL (¡FALTA CLIP!)";
            Debug.Log($"[WINDOW] ResetForNewAttack → '{attack.name}' | clip='{clipName}' | damageWindows={windowCount}");
            for (int i = 0; i < windowCount; i++)
            {
                var dw = attack.damageWindows[i];
                Debug.Log($"[WINDOW]   DamageWindow[{i}] hand={dw.weaponHand} slot={dw.slot} range=[{dw.window.start:F3}-{dw.window.end:F3}]");
            }
        }
    }

    private int _clipNotFoundFrames;

    private bool TryGetAttackNormalizedTime(out float tNorm, out bool foundInOutgoing)
    {
        tNorm = 0f;
        foundInOutgoing = false;

        if (_cachedAnimator != null && _cachedTargetClip != null)
        {
            bool ok = AnimatorClipTimeUtils.TryGetClipNormalizedTime(_cachedAnimator, PlayerAnimHashes.LayerOverride, _cachedTargetClip, out tNorm,
                out _, out foundInOutgoing);

            if (!ok)
            {
                _clipNotFoundFrames++;
                if (DEBUG_WINDOWS && _clipNotFoundFrames <= 5)
                    Debug.LogWarning($"[WINDOW] clip '{_cachedTargetClip.name}' NO encontrado en Animator (frame {_clipNotFoundFrames}). " +
                                     $"ataque='{_currentAttack?.name}' hasValidState={_hasValidAttackState}");
            }
            else
            {
                if (_clipNotFoundFrames > 0 && DEBUG_WINDOWS)
                    Debug.Log($"[WINDOW] clip '{_cachedTargetClip.name}' encontrado tras {_clipNotFoundFrames} frame(s) sin verlo. " +
                              $"tNorm={tNorm:F3} outgoing={foundInOutgoing}");
                _clipNotFoundFrames = 0;
            }

            return ok;
        }

        if (_currentAttack == null) return false;

        _cachedAnimator = _context.Animation.Animator;
        if (_cachedAnimator == null) return false;

        _cachedTargetClip = _currentAttack.animationClip;
        if (_cachedTargetClip == null)
        {
            Debug.LogError($"[WINDOW] '{_currentAttack.name}' no tiene animationClip asignado — ventanas de daño no funcionarán.");
            return false;
        }

        _clipNotFoundFrames = 0;
        return AnimatorClipTimeUtils.TryGetClipNormalizedTime(
            _cachedAnimator,
            PlayerAnimHashes.LayerOverride,
            _cachedTargetClip,
            out tNorm,
            out _,
            out foundInOutgoing);
    }

    private float GetLastHitWindowEnd()
    {
        var damageWindows = _currentAttack.damageWindows;
        float maxEnd = 0f;
        
        foreach (var damageWindow in damageWindows)
        {
            var end = damageWindow.window.end;
            if (end > maxEnd)
            {
                maxEnd = end;
            }
        }
        
        return maxEnd;
    }

    private float GetFirstHitWindowStart()
    {
        var windows = _currentAttack.damageWindows;
        
        float minStart = 1f;
        
        for (int i = 0; i < windows.Count; i++)
        {
            float s = windows[i].window.start;
            if (s < minStart) minStart = s;
        }
        return minStart;
    }

    #endregion
}