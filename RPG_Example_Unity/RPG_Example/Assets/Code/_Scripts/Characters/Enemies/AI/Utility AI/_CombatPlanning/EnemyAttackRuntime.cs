using System.Collections.Generic;
using UnityEngine;

/// Tracks the lifecycle of an active attack: synchronises with the Animator,
/// ticks all window executors, and determines when the attack is finished.
/// Extracted from EnemyCombatPlanner to give each responsibility a clear home.
public sealed class EnemyAttackRuntime
{
    #region Fields

    private AttackWindowExecutor _attackWindows;
    private VfxWindowExecutor _vfxWindows;
    private WeaponVfxWindowExecutor _weaponVfxWindows;
    private AudioWindowExecutor _audioWindows;

    private AttackData _activeAttack;
    private bool _isActive;
    private float _normalizedTime;

    private int _activeStateHash;
    private int _activeLayer = -1;
    private bool _seenInAnimator;
    private float _waitTimer;
    private bool _isComboChain;

    private readonly HashSet<int> _hitTargetsThisAttack = new HashSet<int>(4);

    #endregion

    #region Properties

    public bool IsActive => _isActive;
    public AttackData ActiveAttack => _activeAttack;
    public float NormalizedTime => _normalizedTime;

    #endregion

    #region Initialization

    public void Initialize(
        AttackWindowExecutor attackWindows,
        VfxWindowExecutor vfxWindows,
        WeaponVfxWindowExecutor weaponVfxWindows,
        AudioWindowExecutor audioWindows)
    {
        _attackWindows = attackWindows;
        _vfxWindows = vfxWindows;
        _weaponVfxWindows = weaponVfxWindows;
        _audioWindows = audioWindows;
    }

    public void Reset()
    {
        StopInternal(endCurrentAction: false);
        _hitTargetsThisAttack.Clear();
    }

    #endregion

    #region Public API

    public void StartAttack(AttackData attack, EnemyCombatContext combatCtx, bool isComboChain = false)
    {
        _activeAttack = attack;
        _isActive = true;
        _activeStateHash = attack != null ? attack.TriggerAnimationHash() : 0;
        _activeLayer = -1;
        _seenInAnimator = false;
        _waitTimer = 0f;
        _normalizedTime = 0f;
        _isComboChain = isComboChain;

        _hitTargetsThisAttack.Clear();

        combatCtx.Weapons.AttackStarted(0);
        _attackWindows.StartAttack(_activeAttack);
        _vfxWindows.StartAttack(_activeAttack);
        _weaponVfxWindows.StartAttack(_activeAttack);
        _audioWindows.StartAttack(_activeAttack);
    }

    // Returns true when the attack runtime should be stopped (animation finished).
    public bool Tick(float dt, Animator animator)
    {
        if (!_isActive) return false;

        if (_activeAttack == null || _activeStateHash == 0)
        {
            StopInternal(endCurrentAction: true);
            return true;
        }

        if (!TryGetAttackState(animator, out var stateForTime))
        {
            if (_seenInAnimator)
            {
                StopInternal(endCurrentAction: true);
                return true;
            }

            _waitTimer += dt;
            float baseCrossFade = Mathf.Max(_activeAttack.crossFade, 0.05f);
            float maxWait = _isComboChain
                ? baseCrossFade * 2f + Time.fixedDeltaTime * 6f
                : baseCrossFade + Time.fixedDeltaTime * 3f;

            bool anyTransition = IsAnyLayerInTransition(animator);

            if ((!anyTransition && _waitTimer >= maxWait) || _waitTimer >= 3f)
            {
                StopInternal(endCurrentAction: true);
                return true;
            }

            return false;
        }

        bool wasSeen = _seenInAnimator;
        _seenInAnimator = true;
        _waitTimer = 0f;

        float rawNorm = stateForTime.normalizedTime;
        float tNorm = rawNorm - Mathf.Floor(rawNorm);

        _attackWindows.Tick(tNorm);
        _vfxWindows.Tick(tNorm);
        _weaponVfxWindows.Tick(tNorm);
        _audioWindows.Tick(tNorm);
        _normalizedTime = tNorm;

        if (!(rawNorm >= 1f)) return false;

        bool layerTransition = _activeLayer >= 0
                               && _activeLayer < animator.layerCount
                               && animator.IsInTransition(_activeLayer);

        if (!layerTransition)
        {
            StopInternal(endCurrentAction: true);
            return true;
        }

        return false;
    }

    public void ForceStop()
    {
        StopInternal(endCurrentAction: false);
        _hitTargetsThisAttack.Clear();
    }

    /// Transitions to the next combo hit without destroying the runtime.
    /// Avoids the seenInAnimator=true->stop bug caused by ForceStop+StartAttack
    /// while the animator is mid-transition.
    public void SwitchToComboHit(AttackData nextHit, EnemyCombatContext combatCtx)
    {
        if (nextHit == null) return;

        _activeAttack = nextHit;
        _activeStateHash = nextHit.TriggerAnimationHash();
        _activeLayer = -1;
        _seenInAnimator = false;
        _waitTimer = 0f;
        _normalizedTime = 0f;
        _isComboChain = true;
        _isActive = true;

        _hitTargetsThisAttack.Clear();

        combatCtx.Weapons.AttackStarted(0);
        _attackWindows.StartAttack(nextHit);
        _vfxWindows.StartAttack(nextHit);
        _weaponVfxWindows.StartAttack(nextHit);
        _audioWindows.StartAttack(nextHit);
    }

    public void RegisterHitTarget(int instanceId)
    {
        _hitTargetsThisAttack.Add(instanceId);
    }

    public bool HasHitTarget(int instanceId)
    {
        return _hitTargetsThisAttack.Contains(instanceId);
    }

    public void ClearHitTargets()
    {
        _hitTargetsThisAttack.Clear();
    }

    public bool TryGetCurrentNormalizedTime(Animator animator, out float tNorm)
    {
        tNorm = _normalizedTime;
        if (animator == null || !TryGetAttackState(animator, out var state)) return false;

        float rawNorm = state.normalizedTime;
        tNorm = rawNorm - Mathf.Floor(rawNorm);
        return true;
    }

    #endregion

    #region Helpers

    private void StopInternal(bool endCurrentAction)
    {
        if (!_isActive) return;

        _isActive = false;
        _activeAttack = null;
        _activeStateHash = 0;
        _activeLayer = -1;
        _seenInAnimator = false;
        _waitTimer = 0f;
        _normalizedTime = 0f;
        _isComboChain = false;

        _attackWindows?.StopAttack();
        _vfxWindows?.StopAttack();
        _weaponVfxWindows?.StopAttack();
        _audioWindows?.StopAttack();
    }

    private bool TryGetAttackState(Animator animator, out AnimatorStateInfo stateForTime)
    {
        stateForTime = default;
        if (_activeStateHash == 0) return false;

        int layerCount = animator.layerCount;

        if (_activeLayer >= 0 && _activeLayer < layerCount)
        {
            return TryMatchLayer(animator, _activeLayer, out stateForTime);
        }

        for (int i = 0; i < layerCount; i++)
        {
            if (!TryMatchLayer(animator, i, out stateForTime)) continue;

            _activeLayer = i;
            return true;
        }

        return false;
    }

    private bool TryMatchLayer(Animator animator, int layer, out AnimatorStateInfo stateForTime)
    {
        stateForTime = default;
        bool inTransition = animator.IsInTransition(layer);
        var cur = animator.GetCurrentAnimatorStateInfo(layer);
        var state = cur;

        bool matchCur = MatchesState(cur, _activeStateHash);
        bool matchNext = false;

        if (inTransition)
        {
            var next = animator.GetNextAnimatorStateInfo(layer);
            matchNext = MatchesState(next, _activeStateHash);
            if (!matchCur && matchNext)
            {
                state = next;
            }
        }

        if (!matchCur && !matchNext) return false;

        stateForTime = state;
        return true;
    }

    private static bool MatchesState(in AnimatorStateInfo st, int hash)
    {
        return st.shortNameHash == hash || st.fullPathHash == hash;
    }

    private static bool IsAnyLayerInTransition(Animator animator)
    {
        int count = animator.layerCount;
        for (int i = 0; i < count; i++)
        {
            if (animator.IsInTransition(i)) return true;
        }

        return false;
    }

    #endregion
}