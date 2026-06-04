using System;
using Gameplay.Enemies;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterStats), typeof(EnemyMovement), typeof(EnemyAnimation))]
[RequireComponent(typeof(NavMeshAgent), typeof(EnemyCombatPlanner), typeof(EnemyCombat))]
[RequireComponent(typeof(EquipmentHandler), typeof(EnemyWeaponHandler))]
[RequireComponent(typeof(EnemyAttackAnimatorSpeedDriver))]
public class EnemyBase : CharacterBase, ITargetable, IPoolable
{

    [Header("Behavior")] [SerializeField] private EnemyBehaviorProfile _behaviorProfile;

    [Header("Systems")] [SerializeField] private CharacterHealthSystem _healthSystem;
    [SerializeField] private EnemyPerception _perception;
    [SerializeField] private EnemyHealthBar _healthBar;
    [SerializeField] private EnemyPoiseBar _poiseBar;
    [SerializeField] private FactionComponent _factionComponent;
    [SerializeField] private EnemyFeedbacksController _feedbacksController;

    [Header("Combat")] [SerializeField] private Enums.CombatTemperament _temperament = Enums.CombatTemperament.Normal;

    private EnemyStateFactory _stateFactory;

    [Header("Definition / Data")] [SerializeField]
    private EnemyDefinition _definition;

    public EnemyDefinition Definition => _definition;
    public EnemyPerception Perception => _perception;

    public event Action<EnemyBase> OnEnemyDied;

    private EnemyContext _context;
    public EnemyContext Context => _context;

    private CharacterStats _stats;
    private EnemyMovement _movement;
    private NavMeshAgent _agent;
    private EnemyAnimation _animation;
    private EnemyCombatPlanner _combatPlanner;
    private EnemyCombat _enemyCombat;
    private EquipmentHandler _equipmentHandler;
    private EnemyWeaponHandler _weaponHandler;
    private EnemyAttackAnimatorSpeedDriver _attackSpeedDriver;
    private EnemyAudio _audio;

    // Poise system — created once in Awake, reused across pool cycles via Reset().
    private readonly EnemyPoiseSystem _poiseSystem = new EnemyPoiseSystem();

    private PlayerCombatController _cachedPlayerCombat;
    private PlayerFeedbacksController _cachedPlayerFeedbacks;

    #region ITargetable Implementation

    public Transform Transform => transform;
    public Enums.Faction Faction => _factionComponent.Faction;
    public bool IsAlive => _healthSystem.IsAlive;
    public bool IsNoisy => false;
    public float CurrentHealth01 => _healthSystem.CurrentHealth01;

    public float TargetWindUp01
    {
        get
        {
            if (!_combatPlanner.IsAttackRuntimeActive) return 0f;
            float tNorm = _combatPlanner.AttackNormalizedTime;
            var attack = _combatPlanner.ActiveAttack;
            if (attack == null || attack.damageWindows.Count == 0) return 0f;

            float firstStart = float.MaxValue;
            for (int i = 0; i < attack.damageWindows.Count; i++)
            {
                float s = attack.damageWindows[i].window.start;
                if (s < firstStart) firstStart = s;
            }

            return tNorm < firstStart ? Mathf.Clamp01(tNorm / firstStart) : 0f;
        }
    }

    public float TargetRecovery01
    {
        get
        {
            if (!_combatPlanner.IsAttackRuntimeActive) return 0f;
            float tNorm = _combatPlanner.AttackNormalizedTime;
            var attack = _combatPlanner.ActiveAttack;
            if (attack == null || attack.damageWindows.Count == 0) return 0f;

            float lastEnd = 0f;
            for (int i = 0; i < attack.damageWindows.Count; i++)
            {
                float e = attack.damageWindows[i].window.end;
                if (e > lastEnd) lastEnd = e;
            }

            return tNorm > lastEnd ? Mathf.Clamp01((tNorm - lastEnd) * 1.5f) : 0f;
        }
    }

    public Vector3 Velocity => Vector3.zero;

    #endregion

    protected override void Awake()
    {
        base.Awake();
        _stats = GetComponent<CharacterStats>();
        _movement = GetComponent<EnemyMovement>();
        _agent = GetComponent<NavMeshAgent>();
        _animation = GetComponent<EnemyAnimation>();
        _combatPlanner = GetComponent<EnemyCombatPlanner>();
        _enemyCombat = GetComponent<EnemyCombat>();
        _factionComponent = GetComponent<FactionComponent>();
        _weaponHandler = GetComponent<EnemyWeaponHandler>();
        _attackSpeedDriver = GetComponent<EnemyAttackAnimatorSpeedDriver>();
        _audio = GetComponent<EnemyAudio>();

        _poiseSystem.Initialize(
            _definition.maxPoise,
            _definition.poiseRegenDelay,
            _definition.poiseRegenRate);

        _context = new EnemyContextBuilder(gameObject, _stats, _healthSystem)
            .WithAgent(_agent)
            .WithMovement(_movement)
            .WithAnimation(_animation)
            .WithTargetable(this)
            .WithPerception(_perception)
            .WithCombatPlanner(_combatPlanner)
            .WithTemperament(_temperament)
            .WithBehaviorProfile(_behaviorProfile)
            .WithCombat(_enemyCombat)
            .WithFaction(_factionComponent)
            .WithWeaponHandler(_weaponHandler)
            .WithAudio(_audio)
            .WithPoise(_poiseSystem)
            .WithFeedbacks(_feedbacksController)
            .Build();

        _context.BindDeathCallback(HandleDeathComplete);

        _poiseSystem.OnPoiseBreak += HandlePoiseBreak;

        if (_poiseBar != null)
            _poiseBar.Initialize(_poiseSystem);

        if (_feedbacksController != null)
            _feedbacksController.Initialize(_healthSystem);
    }

    private void OnEnable()
    {
        _healthSystem.Initialize(_stats);

        _movement.Initialize(_context);
        _perception.Initialize(_context);

        _weaponHandler.Initialize(_context);

        _combatPlanner.Initialize(_context);
        _attackSpeedDriver.Initialize(_context);

        _poiseSystem.Reset();

        SubscribeToEvents();

        _stateFactory = new EnemyStateFactory(_stateMachine, _context);
        _context.BindStateFactory(_stateFactory);

        _stateMachine.Set(_stateFactory.GetInitialState(_behaviorProfile.startPatrolling));

        CombatAnalytics.EnemySpawned(this);
    }

    private void OnDisable()
    {
        UnsubscribeToEvents();
    }

    protected override void Update()
    {
        if (!_context.Health.IsAlive) return;

        base.Update();
        _poiseSystem.Tick(Time.deltaTime);
    }

    private void HandleDead()
    {
        CombatAnalytics.EnemyDied(this);
        CombatAnalytics.EncounterEnded(this, playerDied: false, transform.position);

        _context.CombatPlanner.ForceRelease();
        _context.Perception.DisablePerception();

        _stateMachine.Set(_stateFactory.Dead);
    }

    private void HandleDeathComplete()
    {
        OnEnemyDied?.Invoke(this);
    }

    private void HandlePoiseBreak()
    {
        if (!_context.Health.IsAlive) return;

        _context.Feedbacks?.PlayPoiseBreak();

        _stateFactory.Stun.Configure(_definition.stunDuration);
        _stateMachine.Set(_stateFactory.Stun);
    }

    private void HandleWeaponDealDamage(DamageContext hit)
    {
        if (hit.Target == null) return;

        var targetComponent = hit.Target as Component;

        var parryResult = _context.CombatPlanner.TryConsumeHitByParryResult(targetComponent);
        if (parryResult.consumed)
        {
            if (parryResult.fresh)
            {
                PlayParryFeedbackFromDamagePath(targetComponent, hit.HitPoint, parryResult.quality);
            }

            return;
        }

        var activeAttack = _context.CombatPlanner.ActiveAttack;
        var hitType = activeAttack != null ? activeAttack.attackHitType : Enums.HitType.Normal;

        float baseDamage = _context.Stats.GetStatValue(Enums.StatType.Attack);
        float multiplier = _weaponHandler.baseWeapon != null ? _weaponHandler.equipedWeapon.enemyDamageMultiplier : 1f;
        float damage = baseDamage * multiplier;

        CombatAnalytics.EnemyDealtDamage(this, damage, hitType);

        // Pass poiseDamage so targets with a poise system receive it.
        // Faction filtering already happened in AttackColliderHandler.
        var poiseDmg = activeAttack != null ? activeAttack.poiseDamage : 0f;

        _context.CombatPlanner.RegisterHitLanded(targetComponent);

        hit.Target.TakeDamage(damage, hit.HitPoint, (hit.HitPoint - transform.position).normalized, hitType, poiseDmg);
    }

    private void PlayParryFeedbackFromDamagePath(Component targetComponent, Vector3 hitPoint, Enums.ParryQuality quality)
    {
        if (targetComponent == null) return;

        if (_cachedPlayerCombat == null)
            _cachedPlayerCombat = targetComponent.GetComponentInParent<PlayerCombatController>();
        if (_cachedPlayerCombat == null) return;

        if (_cachedPlayerFeedbacks == null)
            _cachedPlayerFeedbacks = targetComponent.GetComponentInParent<PlayerFeedbacksController>();

        Vector3 refPoint = _cachedPlayerCombat.transform.position + Vector3.up * 1.0f;
        Vector3 dir = refPoint - hitPoint;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = _cachedPlayerCombat.transform.forward;

        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);

        if (_cachedPlayerFeedbacks != null)
        {
            _cachedPlayerFeedbacks.PlayParryReceived(hitPoint, rot, quality);
        }

        // Apply success lockout so cancel windows open correctly after a damage-path parry.
        // Without this, successLockoutSeconds only fired from HandleParryContact (collider path).
        _cachedPlayerCombat.NotifyParryResult(true);
    }

    private void HandleParried(Enums.ParryQuality quality)
    {
        float poiseDmg = quality == Enums.ParryQuality.Perfect
            ? _definition.perfectParryPoiseDamage
            : _definition.parryPoiseDamage;

        _poiseSystem.TakePoiseDamage(poiseDmg);

        if (_poiseSystem.IsBroken) return; // Stun already set by HandlePoiseBreak, don't override

        _context.CombatPlanner.ForceRelease();
        _stateFactory.Knockback.Configure(fromParry: true);
        _stateMachine.Set(_stateFactory.Knockback);
    }

    private void Damaged(float v, Enums.HitType hitType, float poiseDamage)
    {
        if (!_context.Health.IsAlive) return;

        CombatAnalytics.EnemyTookDamage(this, v, hitType);

        // Poise damage arrives from the attacker's TakeDamage overload —
        // no cast or component search needed.
        if (poiseDamage > 0f)
        {
            _poiseSystem.TakePoiseDamage(poiseDamage);
        }

        if (_poiseSystem.IsBroken && hitType != Enums.HitType.Knockdown) return;

        if (!_context.CombatPlanner.CanBeInterruptedBy(hitType)) return;

        _context.CombatPlanner.MemorizeCurrentAction();

        switch (hitType)
        {
            case Enums.HitType.Normal:
                _context.Animation.PlayHitAnimation(Random.Range(1, 3));
                break;
            case Enums.HitType.Knockback:
                _context.CombatPlanner.ForceRelease();
                _stateMachine.Set(_stateFactory.Knockback);
                break;
            case Enums.HitType.Knockdown:
                _context.CombatPlanner.ForceRelease();
                _stateMachine.Set(_stateFactory.Knockdown);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(hitType), hitType, null);
        }
    }

    private void SubscribeToEvents()
    {
        UnsubscribeToEvents();

        _enemyCombat.OnWeaponDealtDamage += HandleWeaponDealDamage;
        _combatPlanner.OnParried += HandleParried;
        _healthSystem.OnDeath += HandleDead;
        _healthSystem.OnDamageTaken += Damaged;
    }

    private void UnsubscribeToEvents()
    {
        if (_enemyCombat != null)
            _enemyCombat.OnWeaponDealtDamage -= HandleWeaponDealDamage;

        if (_combatPlanner != null)
            _combatPlanner.OnParried -= HandleParried;

        if (_healthSystem == null) return;

        _healthSystem.OnDeath -= HandleDead;
        _healthSystem.OnDamageTaken -= Damaged;
    }

    public void BeginSpawnSequence()
    {
        _stateMachine.Set(_stateFactory.Spawn);
    }

    // Plays the spawn animation then enters Chasing with the given target instead of Patrol/Idle.
    public void BeginSpawnSequenceTargeting(ITargetable target)
    {
        _stateFactory.Spawn.SetPostSpawnTarget(target);
        _stateMachine.Set(_stateFactory.Spawn);
    }

    public void ForceEnterCombat(ITargetable target)
    {
        _perception.EnablePerception();
        _perception.ForceTarget(target);
        _stateMachine.Set(_stateFactory.Chasing);
    }

    // Called by animation event on the Spawn animation clip
    public void OnSpawnVulnerable()
    {
        _context.Health.SetInvulnerable(false);
    }

    public void OnSpawnFromPool()
    {
        _context.ResetSpawnPosition(transform.position);
    }

    public void OnReturnToPool()
    {
        _combatPlanner.ForceRelease();
        _perception.DisablePerception();

        _poiseSystem.Reset();

        _stateMachine.Set(_stateFactory.GetInitialState(_context.BehaviorProfile.startPatrolling));
    }

    public void ReturnToPool()
    {
        if (GameServices.TryGet<EnemyManager>(out var enemyManager))
        {
            enemyManager.DespawnEnemy(this);
        }
    }
}
