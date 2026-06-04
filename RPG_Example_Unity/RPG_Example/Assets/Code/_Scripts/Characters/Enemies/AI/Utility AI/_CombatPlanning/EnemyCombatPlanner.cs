using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// Orchestrates enemy combat behaviour each fixed tick.
/// Decision-making is delegated to EnemyActionSelector,
/// attack lifecycle to EnemyAttackRuntime,
/// and parry handling to EnemyParryResponder.
public class EnemyCombatPlanner : MonoBehaviour
{
    #region Fields

    [SerializeField] private CombatPlannerConfig _config;
    [SerializeField] private List<EnemyAction> _actions = new();

    [SerializeField] private float _combatRange = 10f;
    public float combatRange => _combatRange;

    [Header("Positioning Priority")] [Tooltip("When enabled, enemy will prioritize positioning actions when attack is ready but out of range.")]
    public bool enablePositioningPriority = true;

    [Tooltip("Bonus multiplier for positioning actions when attack is ready.")]
    public float positioningBonus = 2.5f;

    [Tooltip("Distance tolerance — if within this range of optimal, attack anyway.")]
    public float positioningTolerance = 0.5f;

    [Tooltip("Multiplier applied to positioningTolerance when no positioning action is available.")]
    public float relaxedToleranceMultiplier = 2.5f;

    private EnemyContext _ctx;

    private readonly Dictionary<EnemyAction, float> _cooldowns = new();
    private readonly Dictionary<EnemyAction, object> _actionRuntime = new();
    private readonly List<AttackAction> _attackActionsCache = new(8);
    private readonly Dictionary<AttackAction, float> _optimalRangeCache = new();

    private EnemyAction _currentAction;
    private EnemyAction _lastActionBeforeHit;
    private float _currentActionScore;

    private float _nextEvaluationTime;
    private float _nextActionTime;

    private Enums.ActionCategory? _commitCategory = null;
    private float _commitUntil = 0f;

    private ScoreContext _lastContext;

    private EnemyCombatContext _combatCtx;
    private EnemyBase _enemyBase;

    private EnemyAttackRuntime _attackRuntime;
    private EnemyParryResponder _parryResponder;
    private EnemyActionSelector _actionSelector;

    private AttackTokenService _tokenService;
    private bool _holdingToken;
    private float _tokenStuckTimer;

    private float _lastTargetWindup;
    private PlayerCombatController _cachedPlayerCombat;

    #endregion

    public event Action<Enums.ParryQuality> OnParried;

    private sealed class NullVfxUser : IVfxUser
    {
        public void ActivateAttack(int id)
        {
        }

        public void DeactiveAttack(int id)
        {
        }
    }

    #region Result Types

    public readonly struct ParryConsumeResult
    {
        public readonly bool consumed;
        public readonly bool fresh;
        public readonly Enums.ParryQuality quality;

        public ParryConsumeResult(bool consumed, bool fresh, Enums.ParryQuality quality)
        {
            this.consumed = consumed;
            this.fresh = fresh;
            this.quality = quality;
        }

        public static ParryConsumeResult No() => new ParryConsumeResult(false, false, default);
        public static ParryConsumeResult ConsumedNoFx() => new ParryConsumeResult(true, false, default);
        public static ParryConsumeResult Fresh(Enums.ParryQuality q) => new ParryConsumeResult(true, true, q);
    }

    #endregion

    #region Properties

    public bool IsBusy => Time.time < _nextActionTime;
    public string Debug_CurrentActionName => _currentAction ? _currentAction.name : "None";

    public Enums.ActionCategory CurrentActionCategory => _currentAction == null ? Enums.ActionCategory.Movement : _currentAction.category;

    public IEnumerable<EnemyAction> Debug_Actions => _actions;
    public ScoreContext Debug_GetLastContext() => _lastContext;
    public EnemyAction CurrentAction => _currentAction;

    public bool IsAttackRuntimeActive => _attackRuntime.IsActive;
    public AttackData ActiveAttack => _attackRuntime.ActiveAttack;
    public float AttackNormalizedTime => _attackRuntime.NormalizedTime;

    #endregion

    #region Initialization

    public void Initialize(EnemyContext ctx)
    {
        // Release any held token before overwriting the context (pool-reuse safety).
        ReleaseToken();

        _ctx = ctx;

        _cooldowns.Clear();
        _actionRuntime.Clear();
        _attackActionsCache.Clear();
        _optimalRangeCache.Clear();

        _currentAction = null;
        _lastActionBeforeHit = null;
        _currentActionScore = 0f;
        _commitCategory = null;
        _commitUntil = 0f;
        _nextActionTime = 0f;

        RebuildActionCaches();

        _combatCtx = new EnemyCombatContext(_ctx, _ctx.EnemyCombat);

        // EnemyWeaponHandler already in ctx.WeaponHandler — no GetComponent needed.
        IVfxUser vfxUser = _ctx.WeaponHandler != null ? (IVfxUser)_ctx.WeaponHandler : new NullVfxUser();

        var attackWindows = new AttackWindowExecutor(_combatCtx.Weapons, _ctx.Owner.transform.GetInstanceID());
        var vfxWindows = new VfxWindowExecutor(vfxUser);
        var weaponVfxWindows = new WeaponVfxWindowExecutor(_ctx.WeaponHandler);
        var audioWindows = new AudioWindowExecutor(_ctx.Owner.transform);

        _attackRuntime = new EnemyAttackRuntime();
        _attackRuntime.Initialize(attackWindows, vfxWindows, weaponVfxWindows, audioWindows);
        _attackRuntime.Reset();

        _parryResponder = new EnemyParryResponder();
        _parryResponder.Reset();
        _parryResponder.OnParried += quality => OnParried?.Invoke(quality);

        _actionSelector = new EnemyActionSelector(
            _config, _actions, _cooldowns, _optimalRangeCache,
            enablePositioningPriority, positioningBonus,
            positioningTolerance, relaxedToleranceMultiplier,
            _config.defensePriorityMultiplier);

        // EnemyBase is a sibling component — cache once in Initialize, not per-tick.
        _enemyBase = _ctx.Owner.GetComponent<EnemyBase>();

        GameServices.TryGet<AttackTokenService>(out _tokenService);
        _holdingToken = false;
        _tokenStuckTimer = 0f;

        _nextEvaluationTime = Time.time + Random.Range(0f, 0.05f);
    }

    #endregion

    #region Unity Callbacks

    private void OnDisable()
    {
        ReleaseToken();
    }

    #endregion

    #region Update

    public void TickPlan(float dt)
    {
        float now = Time.time;

        if (_holdingToken && !_attackRuntime.IsActive)
        {
            // Release token if no attack runtime is running.
            // Covers both: movement action holding token (shouldn't happen) and
            // combo stuck mid-sequence with dead runtime (token leak).
            if (_currentAction is not AttackAction)
            {
                ReleaseToken();
                _tokenStuckTimer = 0f;
            }
            else
            {
                _tokenStuckTimer += dt;
                if (_tokenStuckTimer >= 0.5f)
                {
                    ReleaseToken();
                    ClearRuntime(_currentAction);
                    _currentAction = null;
                    _currentActionScore = 0f;
                    _tokenStuckTimer = 0f;
                }
            }
        }
        else
        {
            _tokenStuckTimer = 0f;
        }

        _currentAction?.Tick(_ctx, dt);

        // Tick the attack runtime. Returns true when the animation has finished.
        if (_attackRuntime.Tick(dt, _ctx.Animation.Animator))
        {
            OnAttackRuntimeFinished();
        }

        if (_attackRuntime.IsActive) return;

        if (_commitCategory == Enums.ActionCategory.Attack)
        {
            _commitCategory = null;
            _commitUntil = 0f;
        }

        // Force immediate re-evaluation on the rising edge of player windup so the
        // enemy can react to the attack start rather than waiting for the next tick.
        float currentWindup = _ctx.Perception.CurrentTargetableTarget?.TargetWindUp01 ?? 0f;
        if (currentWindup > 0f && _lastTargetWindup <= 0f && _currentAction is not DodgeAction)
            _nextEvaluationTime = now;
        _lastTargetWindup = currentWindup;

        if (now < _nextEvaluationTime) return;
        _nextEvaluationTime = now + _config.evaluationRate;

        ScoreContext sc = BuildScoreContext(now);

        EnemyAction chosen = _actionSelector.Select(
            sc,
            now,
            attacksLocked: now < _nextActionTime,
            currentAction: _currentAction,
            currentActionScore: _currentActionScore,
            commitCategory: _commitCategory,
            commitUntil: _commitUntil);

        _lastContext = sc;

        if (chosen == null)
        {
            // When there is no current action and nothing scored, retry sooner than
            // a full evaluationRate so the enemy doesn't freeze visibly.
            if (_currentAction == null)
                _nextEvaluationTime = now + Mathf.Min(0.05f, _config.evaluationRate);
            return;
        }

        if (_currentAction != null && chosen != _currentAction)
        {
            _currentAction.OnInterrupted(_ctx);
            ClearRuntime(_currentAction);
            _attackRuntime.ForceStop();

            if (_currentAction is AttackAction)
            {
                ReleaseToken();
            }
        }

        float chosenScore = chosen.Evaluate(sc);
        ExecuteAction(chosen, now, chosenScore);
    }

    private void OnAttackRuntimeFinished()
    {
        _ctx.Animation.SetIsAttacking(false);

        if (_currentAction is not AttackAction currentAttack) return;

        if (_commitCategory == Enums.ActionCategory.Attack)
        {
            _commitCategory = null;
            _commitUntil = 0f;
        }

        bool chained = currentAttack.OnHitFinished(_ctx);
        if (chained)
        {
            return;
        }

        ReleaseToken();

        ClearRuntime(_currentAction);
        _currentAction = null;
        _currentActionScore = 0f;
        _nextEvaluationTime = Time.time + Random.Range(0f, Mathf.Min(0.06f, _config.evaluationRate));
    }

    #endregion

    #region Public API

    public T GetOrCreateRuntime<T>(EnemyAction action) where T : class, new()
    {
        if (action == null) return null;

        if (_actionRuntime.TryGetValue(action, out var boxed) && boxed is T typed)
            return typed;

        var fresh = new T();
        _actionRuntime[action] = fresh;
        return fresh;
    }

    public void ClearRuntime(EnemyAction action)
    {
        if (action == null) return;
        _actionRuntime.Remove(action);
    }

    public void ScheduleRelease(float duration)
    {
        float releaseTime = Time.time + duration;
        if (releaseTime > _nextEvaluationTime)
        {
            _nextEvaluationTime = releaseTime;
        }
    }

    public bool TryReengageAfterHit()
    {
        if (_lastActionBeforeHit == null) return false;

        // Only re-engage attack actions. Movement actions have distance/position
        // conditions that may have changed after the hit reaction — let the planner
        // evaluate fresh instead of blindly re-executing a stale action.
        if (_lastActionBeforeHit is not AttackAction)
        {
            _lastActionBeforeHit = null;
            _nextEvaluationTime = 0f;
            return false;
        }

        if (_lastActionBeforeHit.canBeInterrupted)
        {
            if (_config.useAttackTokens && !_holdingToken)
            {
                Enums.Faction faction = _ctx.FactionComponent.Faction;
                if (_tokenService != null && !_tokenService.TryAcquire(faction))
                {
                    _lastActionBeforeHit = null;
                    return false;
                }

                _holdingToken = _tokenService != null;
            }

            _currentAction = _lastActionBeforeHit;
            _lastActionBeforeHit = null;

            _currentActionScore = _currentAction.Evaluate(_lastContext);
            _currentAction.Execute(_ctx);

            if (_currentAction is AttackAction atk && atk.attackData != null)
            {
                float now = Time.time;
                if (atk.maxCooldown > 0f) _cooldowns[atk] = now + Random.Range(atk.minCooldown, atk.maxCooldown);
                StartAttackRuntime(atk.attackData);
            }

            return true;
        }

        _lastActionBeforeHit = null;
        return false;
    }

    public void MemorizeCurrentAction() => _lastActionBeforeHit = _currentAction;
    public bool CanActionBeInterrupted() => _currentAction == null || _currentAction.canBeInterrupted;

    public bool CanBeInterruptedBy(Enums.HitType hitType)
    {
        if (_currentAction == null) return true;
        if (!_currentAction.canBeInterrupted) return false;
        if (hitType == Enums.HitType.Normal) return true;

        if (_currentAction is AttackAction atk)
        {
            return hitType switch
            {
                Enums.HitType.Knockback => atk.canBeInterruptedByKnockback,
                Enums.HitType.Knockdown => atk.canBeInterruptedByKnockdown,
                _ => true
            };
        }

        return true;
    }

    public void BeginAction(Enums.ActionCategory category, float commitSeconds, float cadenceDelay)
    {
        float now = Time.time;

        if (commitSeconds > 0f)
        {
            _commitCategory = category;
            _commitUntil = now + commitSeconds;
        }

        if (cadenceDelay > 0f && category == Enums.ActionCategory.Attack)
        {
            _nextActionTime = now + cadenceDelay;
        }
    }

    public void ForceRelease()
    {
        _commitCategory = null;
        _commitUntil = 0f;

        if (_currentAction != null)
        {
            // Must call OnInterrupted before ClearRuntime so actions that own
            // movement state (DodgeAction: IsFacingLocked, SetRootMotion) can
            // restore it. Skipping this was the cause of IsFacingLocked getting
            // stuck when a hit reaction interrupted a dodge mid-flight.
            _currentAction.OnInterrupted(_ctx);
            ClearRuntime(_currentAction);
        }

        _currentAction = null;
        _currentActionScore = 0f;

        _attackRuntime.ForceStop();
        ReleaseToken();

        _nextEvaluationTime = Time.time;
    }

    public bool CanBeInterrupted()
    {
        if (_commitCategory == null) return true;
        return Time.time >= _commitUntil;
    }

    public void RegisterHitLanded(Component target)
    {
        if (!_attackRuntime.IsActive || _attackRuntime.ActiveAttack == null) return;
        if (target == null) return;

        if (_cachedPlayerCombat == null)
            _cachedPlayerCombat = target.GetComponentInParent<PlayerCombatController>();
        if (_cachedPlayerCombat == null) return;

        _attackRuntime.RegisterHitTarget(_cachedPlayerCombat.GetInstanceID());
    }

    public ParryConsumeResult TryConsumeHitByParryResult(Component target)
    {
        Animator animator = _ctx?.Animation?.Animator;
        return _parryResponder.TryConsume(target, _attackRuntime, animator);
    }

    #endregion

    #region Helpers

    private void ExecuteAction(EnemyAction action, float now, float evaluatedScore)
    {
        if (action is AttackAction && _config.useAttackTokens && !_holdingToken)
        {
            // Lazy re-acquisition: if service was not registered yet during Initialize(), retry now.
            if (_tokenService == null)
                GameServices.TryGet<AttackTokenService>(out _tokenService);

            Enums.Faction faction = _ctx.FactionComponent.Faction;
            if (_tokenService != null && !_tokenService.TryAcquire(faction))
            {
                _nextEvaluationTime = now + _config.evaluationRate + Random.Range(0.05f, 0.15f);
                return;
            }

            _holdingToken = _tokenService != null;
        }

        if (_enemyBase != null && action != null)
        {
            bool switched = _currentAction != null && _currentAction != action;
            CombatAnalytics.AIActionChosen(_enemyBase, action.name, action.category, _currentActionScore, switched);
        }

        _currentAction = action;
        _currentActionScore = evaluatedScore;

        if (_currentAction is AttackAction atk && atk.maxCooldown > 0f)
        {
            _cooldowns[atk] = now + Random.Range(atk.minCooldown, atk.maxCooldown);
        }

        _currentAction.Execute(_ctx);

        if (_currentAction is AttackAction atkWithData && atkWithData.attackData != null)
        {
            StartAttackRuntime(atkWithData.attackData);
        }
    }

    private void StartAttackRuntime(AttackData attack)
    {
        _ctx.Animation.SetIsAttacking(true);
        _parryResponder.OnAttackStarted();
        _attackRuntime.StartAttack(attack, _combatCtx);
    }

    private ScoreContext BuildScoreContext(float now)
    {
        Transform target = _ctx.Perception.CurrentTarget;
        var targetable = _ctx.Perception.CurrentTargetableTarget;
        Transform ownerTransform = _ctx.Owner.transform;

        float distance = 0f;
        float sqrDistance = 0f;
        float angle = 0f;
        bool hasLOS = false;

        if (target != null)
        {
            Vector3 toTarget = target.position - ownerTransform.position;
            sqrDistance = toTarget.sqrMagnitude;
            distance = sqrDistance > 0.000001f ? Mathf.Sqrt(sqrDistance) : 0f;
            angle = Vector3.SignedAngle(ownerTransform.forward, toTarget, Vector3.up);
            hasLOS = _ctx.Perception.HasLineOfSightTo(ownerTransform.position, target.position);
        }

        TemperamentWeights style = CombatTemperamentDB.Get(_ctx.Temperament);
        float selfHP = _ctx.Health.CurrentHealth01;
        float targetHP = targetable?.CurrentHealth01 ?? 1f;
        float windup = targetable?.TargetWindUp01 ?? 0f;
        float recovery = targetable?.TargetRecovery01 ?? 0f;

        bool tokenAvailable = true;
        if (_config.useAttackTokens && _tokenService != null && !_holdingToken)
        {
            Enums.Faction faction = _ctx.FactionComponent.Faction;
            tokenAvailable = _tokenService.ActiveTokens(faction) < _tokenService.MaxTokens(faction);
        }

        return new ScoreContext
        {
            enemyContext = _ctx,
            distance = distance,
            sqrDistance = sqrDistance,
            angleDegree = angle,
            selfHP = selfHP,
            targetHP = targetHP,
            selfStamine = 1f,
            hasLOS = hasLOS,
            spaceFree = _ctx.Movement.HasFreeSpace(),
            targetWindup = windup,
            targetRecovery = recovery,
            advantage = ComputeAdvantage01(_ctx),
            style = style,
            timeNow = now,
            attackIntent01 = ComputeAttackIntent(selfHP, targetHP, windup, recovery, distance, style),
            hasReadyAttack = false,
            attackTokenAvailable = tokenAvailable
        };
    }

    private static float ComputeAttackIntent(float selfHP, float targetHP, float windup, float recovery, float distance, TemperamentWeights style)
    {
        float advantage01 = Mathf.Clamp01((selfHP - targetHP + 1f) * 0.5f);
        float opportunity01 = Mathf.Clamp01(windup * 0.5f + recovery * 0.8f);
        float distFactor = Mathf.Clamp01(1f - Mathf.InverseLerp(3f, 8f, distance));
        float temperamentAggro = Mathf.Clamp01(style.attack * 0.7f + style.pressure * 0.3f);
        float situational = opportunity01 * 0.5f + advantage01 * 0.3f + distFactor * 0.2f;

        return Mathf.Clamp01(temperamentAggro * 0.7f + situational * 0.3f);
    }

    private static float ComputeAdvantage01(EnemyContext ctx)
    {
        float targetHP = ctx.Perception.CurrentTargetableTarget?.CurrentHealth01 ?? 1f;
        return Mathf.Clamp01((ctx.Health.CurrentHealth01 - targetHP + 1f) * 0.5f);
    }

    private void ReleaseToken()
    {
        if (!_holdingToken) return;
        _tokenService?.Release(_ctx.FactionComponent.Faction);
        _holdingToken = false;
    }

    public void RegisterCooldown(EnemyAction action, float duration)
    {
        if (action == null || duration <= 0f) return;
        _cooldowns[action] = Time.time + duration;
    }

    public void ContinueComboHit(AttackData nextHit)
    {
        if (nextHit == null) return;
        _parryResponder.OnAttackStarted();
        _attackRuntime.SwitchToComboHit(nextHit, _combatCtx);
    }

    private void RebuildActionCaches()
    {
        _attackActionsCache.Clear();

        for (int i = 0; i < _actions.Count; i++)
        {
            if (_actions[i] is AttackAction atk)
            {
                _attackActionsCache.Add(atk);
            }
        }
    }

    #endregion
}