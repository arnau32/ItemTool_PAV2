using System;
using UnityEngine;

public class PlayerCombatController : MonoBehaviour, IProjectileParryTarget
{
    #region Fields

    [SerializeField] private PlayerInteractable _interactable;
    private PlayerContext _context;
    private readonly InputBuffer _postDodgeAttackBuffer = new InputBuffer();

    private readonly ParryController _parry = new ParryController();
    private ParryData _currentParryData;
    private bool _parrySlotEnabled;

    #endregion

    #region Events

    public event Action OnWeaponSkillUsed;

    #endregion

    #region Properties

    public InputBuffer PostDodgeBuffer => _postDodgeAttackBuffer;
    public bool IsParrying => _parry.IsParrying;
    public ParryData CurrentParryData => _currentParryData;
    public float CurrentParryNormalizedTime => _parry.CurrentNormalizedTime;

    public bool CanMoveCancelParry() => _parry.IsParrying && _parry.CanMoveCancel();
    public bool CanRotateDuringParry() => _parry.IsParrying && _parry.CanRotateDuringParry();
    public bool CanDodgeCancelParry() => _parry.IsParrying && _parry.CanDodgeCancel();

    #endregion

    #region Unity Callbacks

    private void Update()
    {
        if (_context == null) return;

        _parry.Tick(_context.Movement.IsGrounded());
        UpdateParrySlotCollider();
    }

    private void OnDisable()
    {
        if (_context == null || _context.Inputs == null) return;

        var inputs = _context.Inputs;
        inputs.OnLockOn -= HandleLockOnPressed;
        inputs.OnSprint -= _context.Movement.HandleSprintInput;
        inputs.OnInteract -= _interactable.InteractPerformed;
        inputs.OnAttackInput -= HandleAttackInputPerformed;
        inputs.OnDodge -= HandleDodge;
        inputs.OnParry -= HandleParry;

        if (_context.Animation != null)
        {
            _context.Animation.OnAttacking -= HandleAttacking;
        }

        var dodgeSystem = _context.Movement.DodgeSystem;
        if (dodgeSystem != null)
        {
            dodgeSystem.OnDodgeEnded -= HandleDodgeEnded;
        }

        var weaponHandler = _context.EquipmentHandler.WeaponHandler;
        if (weaponHandler != null)
        {
            weaponHandler.OnWeaponDealtDamage -= DealDamage;
            weaponHandler.OnParryContact -= HandleParryContact;
            weaponHandler.OnProjectileParryContact -= HandleProjectileParryContact;
        }

        var eq = _context.EquipmentHandler;
        if (eq == null) return;

        eq.OnEquipWeapon -= HandleWeaponEquipped;
        eq.OnUnEquipWeapon -= HandleWeaponUnEquipped;
    }

    #endregion

    #region Public API

    public void Initialize(PlayerContext context)
    {
        if (context == null)
        {
            Debug.LogError("[PlayerCombatController] Initialize received a null PlayerContext.", this);
            return;
        }

        _context = context;

        _parry.Initialize(_context, _context.EquipmentHandler.WeaponHandler);

        SubscribeToInputEvents();

        var eq = _context.EquipmentHandler;
        if (eq != null)
        {
            eq.OnEquipWeapon += HandleWeaponEquipped;
            eq.OnUnEquipWeapon += HandleWeaponUnEquipped;
            SetParryFromWeapon(eq.GetCurrentWeapon());
        }

        CombatAnalytics.EnsureSession();
        CombatAnalytics.PlayerLockOnToggled(_context.LockOnSystem.IsLockedOn);
    }

    public void SubscribeToWeaponEvents()
    {
        if (_context == null)
        {
            Debug.LogError("[PlayerCombatController] Cannot subscribe weapon events — context is null.", this);
            return;
        }

        _context.Animation.OnAttacking += HandleAttacking;

        var weaponHandler = _context.EquipmentHandler.WeaponHandler;
        weaponHandler.OnWeaponDealtDamage += DealDamage;
        weaponHandler.OnParryContact += HandleParryContact;
        weaponHandler.OnProjectileParryContact += HandleProjectileParryContact;

        var dodgeSystem = _context.Movement.DodgeSystem;
        if (dodgeSystem != null)
        {
            dodgeSystem.OnDodgeEnded += HandleDodgeEnded;
        }
    }

    public (bool active, Enums.ParryQuality quality) GetParryState() => _parry.GetParryState();
    public void NotifyParryResult(bool success) => _parry.NotifyParryResult(success);

    #endregion

    #region Event Handlers

    private void SubscribeToInputEvents()
    {
        _context.Inputs.OnLockOn += HandleLockOnPressed;
        _context.Inputs.OnInteract += _interactable.InteractPerformed;
        _context.Inputs.OnSprint += _context.Movement.HandleSprintInput;
        _context.Inputs.OnAttackInput += HandleAttackInputPerformed;
        _context.Inputs.OnDodge += HandleDodge;
        _context.Inputs.OnParry += HandleParry;
    }

    private void HandleWeaponEquipped(WeaponData weapon)
    {
        SetParryFromWeapon(weapon);
        _parrySlotEnabled = false;
        UpdateParrySlotCollider();
    }

    private void HandleWeaponUnEquipped()
    {
        SetParryFromWeapon(null);
        _parrySlotEnabled = false;
        UpdateParrySlotCollider();
    }

    private void SetParryFromWeapon(WeaponData weapon)
    {
        _currentParryData = weapon != null ? weapon.parry : null;
    }

    private void HandleParry()
    {
        if (_currentParryData == null) return;

        if (_context.PlayableController != null && _context.PlayableController.BlocksAttack) return;

        var dodgeSystem = _context.Movement.DodgeSystem;
        if (dodgeSystem != null && dodgeSystem.IsInDodge) return;

        if (_context.Animation.IsAttacking)
        {
            if (CanParryCancelCurrentAttack())
            {
                _context.ComboController.ResetCombo();
            }
            else
            {
                return;
            }
        }

        _parry.TryStartParry(_currentParryData);
    }

    private bool CanParryCancelCurrentAttack()
    {
        if (_context?.ComboController == null) return false;

        var atk = _context.ComboController.CurrentAttack;
        if (atk == null) return false;

        var windows = atk.parryCancel;
        if (windows == null || windows.Count == 0) return false;

        float t = _context.ComboController.CurrentNormalizedTime;

        for (int i = 0; i < windows.Count; i++)
        {
            if (windows[i].IsActive(t)) return true;
        }

        return false;
    }

    private void HandleParryContact(AttackColliderHandler enemyAttackCollider)
    {
        if (enemyAttackCollider == null) return;

        var (active, _) = _parry.GetParryState();
        if (!active) return;

        var enemyPlanner = enemyAttackCollider.GetComponentInParent<EnemyCombatPlanner>();
        if (enemyPlanner == null) return;

        var res = enemyPlanner.TryConsumeHitByParryResult(this);
        if (!res.consumed || !res.fresh) return;

        var enemyCol = enemyAttackCollider.Collider;
        Vector3 refPoint = transform.position + Vector3.up * 1.0f;
        Vector3 hitPoint = enemyCol != null ? enemyCol.ClosestPoint(refPoint) : enemyAttackCollider.transform.position;

        Vector3 dir = refPoint - hitPoint;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);

        _context.FeedbacksController.PlayParryReceived(hitPoint, rot, res.quality);
        NotifyParryResult(true);
    }

    private void HandleProjectileParryContact(EnemyProjectile projectile)
    {
        // Driven by the event chain — the actual intercept logic lives in TryInterceptProjectile
        // so both the event path and the direct IProjectileParryTarget path share the same code.
        TryInterceptProjectile(projectile);
    }

    // IProjectileParryTarget — called directly by EnemyProjectile.OnHitboxContact before damage.
    public bool TryInterceptProjectile(EnemyProjectile projectile)
    {
        if (projectile == null || !projectile.MarkInterceptCheckDone()) return false;

        var (active, quality) = _parry.GetParryState();
        if (!active || quality == Enums.ParryQuality.None) return false;

        Vector3 hitPoint = projectile.transform.position;
        Vector3 dir = transform.position + Vector3.up - hitPoint;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);

        _context.FeedbacksController?.PlayParryReceived(hitPoint, rot, quality);
        NotifyParryResult(true);

        var currentWeapon = _context.EquipmentHandler.GetCurrentWeapon();
        bool canReflect = currentWeapon != null && currentWeapon.familyType == Enums.WeaponFamily.Warrior;

        if (canReflect)
        {
            var faction = _context.FactionComponent != null
                ? _context.FactionComponent.Faction
                : Enums.Faction.Player;
            projectile.Reflect(transform, faction);
        }
        else
        {
            projectile.DeactivateWithEffect();
        }

        return true;
    }

    private void HandleLockOnPressed()
    {
        _context.LockOnSystem.ToggleLock();
        CombatAnalytics.PlayerLockOnToggled(_context.LockOnSystem.IsLockedOn);
    }

    private void HandleAttacking(bool on) => _context.ComboController.OnAnimationAttackingChanged(on);

    private void HandleDodge()
    {
        // Block dodge during knockdown — recovery has its own path via PlayerLifeController.
        // Knockback allows dodge so the player can escape the hit reaction early.
        if (_context.PlayerLifeController.IsInKnockdown) return;

        var dodgeSystem = _context.Movement.DodgeSystem;

        if (!dodgeSystem.CanDodge()) return;

        if (_context.ComboController.CanDodgeCancel())
        {
            _context.ComboController.ResetCombo();
        }
        else if (_context.Animation.IsAttacking)
        {
            return;
        }

        if (_parry.IsParrying && !_parry.CanDodgeCancel()) return;
        if (_parry.IsParrying) _parry.ForceStop();

        // Allow playable actions that support dodge-interrupt to be cancelled.
        // If the action does NOT allow dodge-interrupt it returns false and the dodge is blocked.
        var playable = _context.PlayableController;
        if (playable != null && playable.IsInAction)
        {
            if (!playable.TryInterrupt(PlayableInterruptFlags.Dodge)) return;
        }

        _postDodgeAttackBuffer.Clear();
        _context.PlayerLifeController.CancelKnockback();
        dodgeSystem.TryExecuteDodge(_context.LockOnSystem.IsLockedOn, _context.Inputs.GetDirectionNormalized());
    }

    private void HandleDodgeEnded()
    {
        if (_postDodgeAttackBuffer.TryConsume(out var bufferedAtk))
        {
            ExecuteAttackInput(bufferedAtk);
        }
    }

    private void ExecuteAttackInput(Enums.AttackInputs atkInput)
    {
        if (_parry.IsParrying && !_parry.CanAttackCancel()) return;

        if (!_context.LockOnSystem.IsLockedOn)
        {
            Vector3 facing = _context.Inputs.GetDirectionNormalized();

            if (facing.sqrMagnitude < 0.01f)
            {
                facing = transform.forward;
            }
            else
            {
                facing.y = 0f;
            }

            if (_context.LockOnSystem.TryGetAutoAimDirection(transform.position, facing, out var autoDir))
            {
                _context.Movement.SnapFacing(autoDir);
            }
        }

        if (atkInput == Enums.AttackInputs.LightInput && _context.Movement.IsSprinting && !_context.ComboController.IsInCombo)
        {
            atkInput = Enums.AttackInputs.Run_Attack;
            _context.Movement.SuspendSprintingKeepRequest();
        }

        _context.ComboController.AddInputToSequence(atkInput);
    }

    private void HandleAttackInputPerformed(Enums.AttackInputs atkInput)
    {
        if (_parry.IsParrying && !_parry.CanAttackCancel()) return;

        if (_context.PlayableController != null && _context.PlayableController.BlocksAttack) return;

        if (_context.PlayerLifeController.IsInHitReaction) return;

        if (atkInput == Enums.AttackInputs.HeavyInput)
        {
            bool skillAllowed = _context.ComboController.CanDodgeCancel()
                             || !_context.ComboController.IsInCombo
                             || !_context.ComboController.IsComboWindowOpen;

            if (skillAllowed)
            {
                TryExecuteWeaponSkill();
                return;
            }
        }

        var dodgeSystem = _context.Movement.DodgeSystem;

        if (dodgeSystem.IsInDodge)
        {
            if (dodgeSystem.CanBufferAttack)
            {
                _postDodgeAttackBuffer.Register(atkInput);
            }

            return;
        }

        ExecuteAttackInput(atkInput);
    }

    private void TryExecuteWeaponSkill()
    {
        if (_parry.IsParrying && !_parry.CanAttackCancel()) return;

        var weapon = _context.EquipmentHandler.GetCurrentWeapon();
        if (weapon == null || weapon.weaponSkill == null) return;

        var skill = weapon.weaponSkill;

        if (!_context.AbilityScoreSystem.CanUseAbility) return;
        if (!_context.Stamina.TryConsumeStamina(skill.staminaCost)) return;

        _context.AbilityScoreSystem.UseAbility();

        var dodgeSystem = _context.Movement.DodgeSystem;
        if (dodgeSystem.IsInDodge)
        {
            _postDodgeAttackBuffer.Clear();
            dodgeSystem.ForceEndDodge();
        }

        _context.ComboController.ResetCombo();
        _context.ComboController.ForceStartAttack(skill);
        OnWeaponSkillUsed?.Invoke();
    }

    private void DealDamage(DamageContext hit)
    {
        if (hit.Target == null) return;
        if (!DamageRules.CanDamage(_context.FactionComponent.Faction, hit.TargetFaction)) return;

        var currentAttack = _context.ComboController.CurrentAttack;
        if (currentAttack == null) return;

        var damage = _context.Stats.GetStatValue(Enums.StatType.Attack) + currentAttack.damage;

        var attackerPos = hit.Attacker != null ? hit.Attacker.transform.position : transform.position;
        Vector3 dir = hit.HitPoint - attackerPos;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

        // Knockdown always breaks poise fully — float.MaxValue is clamped to maxPoise in EnemyPoiseSystem.
        float poiseDmg = currentAttack.attackHitType == Enums.HitType.Knockdown
            ? float.MaxValue
            : currentAttack.poiseDamage;

        hit.Target.TakeDamage(damage, hit.HitPoint, dir.normalized, currentAttack.attackHitType, poiseDmg);

        var asComponent = hit.Target as Component;
        CombatAnalytics.PlayerDealtDamage(damage, asComponent);

        if (!hit.Target.IsInvulnerable)
            _context.FeedbacksController.PlayHitConfirm();

        if (!_context.ComboController.IsSkillAttack)
            _context.AbilityScoreSystem.AddScore(currentAttack.abilityScoreOnHit);

        _context.ComboController.RegisterHit();
    }

    #endregion

    #region Parry Collider

    private void UpdateParrySlotCollider()
    {
        if (_context == null) return;

        var weaponHandler = _context.EquipmentHandler?.WeaponHandler;
        if (weaponHandler == null) return;

        var (active, _) = _parry.GetParryState();

        if (active)
        {
            if (_parrySlotEnabled) return;

            var weapons = weaponHandler.ActiveWeapons;
            for (int i = 0; i < weapons.Count; i++)
            {
                weapons[i].EnableParryCollider();
            }

            _parrySlotEnabled = true;
            return;
        }

        if (!_parrySlotEnabled) return;

        var weaponList = weaponHandler.ActiveWeapons;
        for (int i = 0; i < weaponList.Count; i++)
        {
            weaponList[i].DisableParryCollider();
        }

        _parrySlotEnabled = false;
    }

    #endregion
}