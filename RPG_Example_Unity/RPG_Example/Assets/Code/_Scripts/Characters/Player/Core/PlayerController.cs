using UnityEngine;

[RequireComponent(typeof(PlayerInputs))]
[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(StaminaSystem))]
[RequireComponent(typeof(PlayerAnimation))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(FactionComponent))]
[RequireComponent(typeof(WeaponHandler))]
[RequireComponent(typeof(EquipmentHandler))]
[RequireComponent(typeof(PlayerCombat))]
[RequireComponent(typeof(PlayerLockOnSystem))]
[RequireComponent(typeof(PlayerCombatController))]
[RequireComponent(typeof(PlayerConsumableController))]
[RequireComponent(typeof(PlayerLifeController))]
[RequireComponent(typeof(PlayerPlayableController))]
[RequireComponent(typeof(PlayerUpgradeService))]
public class PlayerController : MonoBehaviour
{
    #region Fields

    private PlayerContext _context;
    private PlayerCombatContext _combatContext;

    [SerializeField] private PlayerHUD _hud;
    [SerializeField] private PlayerLocomotion _locomotion;
    [SerializeField] private PlayerInteractable _interactable;
    [SerializeField] private CharacterHealthSystem _healthSystem;

    private PlayerLifeController _lifeController;
    private PlayerCombatController _combatController;
    private PlayerConsumableController _consumableController;
    private PlayerPlayableController _playableController;
    private PlayerFeedbacksController _feedbacksController;
    private PlayerAudio _playerAudio;
    private PlayerMovement _movement;
    private PlayerLockOnSystem _lockOnSystem;
    private AttackAnimatorSpeedDriver _attackSpeedDriver;
    private PlayerSaveHandler _saveHandler;
    private WeaponHandler _weaponHandler;
    private FactionComponent _faction;
    private PlayerUpgradeService _upgradeService;

    private readonly ComboController _comboController = new();
    private AbilityScoreSystem _abilityScoreSystem = new();

    private bool _isInitialized;

    #endregion

    #region Properties

    public PlayerContext Context => _context;
    public bool IsInitialized => _isInitialized;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        GatherComponents();
        BuildContext();
        InitializeSystems();
    }

    private void OnDisable()
    {
        _abilityScoreSystem?.Shutdown();
    }

    private void Start()
    {
        ExecuteDeferredInitializations();
    }

    private void Update()
    {
        if (!_isInitialized || !_healthSystem.IsAlive) return;

        UpdateLocomotionSystems(Time.deltaTime);
        UpdateCombatSystems();
    }

    private void FixedUpdate()
    {
        if (!_isInitialized || !_healthSystem.IsAlive) return;

        _movement.HandleAllMovement();
    }

    #endregion

    #region Initialization

    private void GatherComponents()
    {
        _weaponHandler = GetComponent<WeaponHandler>();
        _movement = GetComponent<PlayerMovement>();
        _lifeController = GetComponent<PlayerLifeController>();
        _combatController = GetComponent<PlayerCombatController>();
        _consumableController = GetComponent<PlayerConsumableController>();
        _playableController = GetComponent<PlayerPlayableController>();
        _attackSpeedDriver = GetComponent<AttackAnimatorSpeedDriver>();
        _feedbacksController = GetComponent<PlayerFeedbacksController>();
        _playerAudio = GetComponent<PlayerAudio>();
        _faction = GetComponent<FactionComponent>();
        _lockOnSystem = GetComponent<PlayerLockOnSystem>();
        _saveHandler = GetComponent<PlayerSaveHandler>();
        _upgradeService = GetComponent<PlayerUpgradeService>();
    }

    private void BuildContext()
    {
        var stats = GetComponent<CharacterStats>();
        var inputs = GetComponent<PlayerInputs>();
        var stamina = GetComponent<StaminaSystem>();
        var playerAnim = GetComponent<PlayerAnimation>();
        var equipHandler = GetComponent<EquipmentHandler>();
        var combat = GetComponent<PlayerCombat>();

        _hud.Init(this);

        _context = new PlayerContextBuilder(gameObject, stats, _healthSystem)
            .WithInputs(inputs)
            .WithStamina(stamina)
            .WithAnimation(playerAnim)
            .WithComboController(_comboController)
            .WithMovement(_movement)
            .WithEquipmentHandler(equipHandler)
            .WithLockOn(_lockOnSystem)
            .WithLifeController(_lifeController)
            .WithCombatController(_combatController)
            .WithHUD(_hud)
            .WithCombat(combat)
            .WithAbilityScore(_abilityScoreSystem)
            .WithFeedbacks(_feedbacksController)
            .WithFaction(_faction)
            .WithAudio(_playerAudio)
            .WithPlayableController(_playableController)
            .Build();

        _combatContext = new PlayerCombatContext(_context);
    }

    private void InitializeSystems()
    {
        _context.Stamina.Initialize();
        _attackSpeedDriver.Initialize(_context);
        _abilityScoreSystem.Initialize(_context);

        _movement.Initialize(_context);
        _locomotion.Initialize(_context);
        _lockOnSystem.Initialize(_context);

        _comboController.Initialize(_combatContext, _weaponHandler, _weaponHandler);
        _combatController.Initialize(_context);
        _consumableController.Initialize(_context);
        _playableController.Initialize(_context);

        _abilityScoreSystem.UseAbility();
        GameServices.Get<InputService>()?.OnUIClose();

        _isInitialized = true;
    }

    // Requires WeaponHandler._ctx to be ready (weapon spawn, combat event wiring, health init).
    private void ExecuteDeferredInitializations()
    {
        if (!_isInitialized)
        {
            Debug.LogError("[PlayerController] Deferred init called before main initialization.", this);
            return;
        }

        _weaponHandler.Initialize(_context);
        _combatController.SubscribeToWeaponEvents();
        _healthSystem.Initialize(_context.Stats);

        _upgradeService?.SetHealthReference(_healthSystem);
        _upgradeService?.ApplyUpgrades();

        _lifeController.Initialize(_context);
        _playerAudio.Initialize(_context);

        // Wire FeedbackStaminaDebt now that StaminaSystem is initialized and
        // the context is fully ready — must happen after _context.Stamina.Initialize().
        if (_feedbacksController != null && _context.Stamina is StaminaSystem stamina)
            _feedbacksController.Initialize(stamina);

        if (_interactable != null)
            _interactable.Initialize(_playableController, _movement);

        if (_saveHandler != null && GameServices.TryGet<SaveService>(out var save))
            _saveHandler.ApplyVitalsAfterInit(save.CurrentSave);
    }

    #endregion

    #region Internal Logic

    private void UpdateLocomotionSystems(float dt)
    {
        _locomotion.UpdateLocomotion(dt);
        _lockOnSystem.Tick(dt);
    }

    private void UpdateCombatSystems()
    {
        _comboController.UpdateComboController(_movement.IsGrounded());

        HandleCancelMovement();

        _lockOnSystem.HandleDirectionalRightStickLockOn();
        _movement.TickSprint();
    }

    private void HandleCancelMovement()
    {
        bool wantsMove = _context?.Inputs != null && _context.Inputs.IsMoving();
        bool cancelMove = _movement.IsCancellingMovement(wantsMove);

        _context.Animation.SetCancelMovement(cancelMove);
    }

    #endregion
}