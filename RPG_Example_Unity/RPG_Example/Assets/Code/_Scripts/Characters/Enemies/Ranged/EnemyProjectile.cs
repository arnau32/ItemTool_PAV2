using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class EnemyProjectile : MonoBehaviour
{
    #region Fields

    [SerializeField] private ProjectileConfig _config;

    [Tooltip("Layers that destroy the projectile on contact (walls, ground, environment). " +
             "Keep separate from EnemyDealDamage — that is handled by the child EnemyProjectileHitbox.")]
    [SerializeField] private LayerMask _destructionLayers;

    [Tooltip("Vertical offset applied to the target position when computing direction. " +
             "Use to aim at body centre instead of feet.")]
    [SerializeField] private float _targetHeightOffset = 0.8f;

    [Header("Hit Effect")]
    [SerializeField] private GameObject _hitEffectPrefab;

    private Rigidbody _rb;
    private EnemyProjectileHitbox _hitbox;

    private Component _attacker;
    private Enums.Faction _attackerFaction;
    private Transform _target;

    private Vector3 _direction;
    private float _lifetimeTimer;

    // Prevents double-processing when both trigger paths fire in the same physics step.
    private bool _interceptCheckDone;
    // True after Reflect() — skips deactivation when the reflected projectile grazes the reflector.
    private bool _isReflected;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _hitbox = GetComponentInChildren<EnemyProjectileHitbox>();
    }

    private void OnEnable()
    {
        if (_config == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[EnemyProjectile] No ProjectileConfig assigned.", this);
#endif
            gameObject.SetActive(false);
            return;
        }

        // Sync RB to current transform — kinematic RBs lag behind when moved via parent.
        _rb.position = transform.position;
        _rb.rotation = transform.rotation;

        if (_target != null)
        {
            Vector3 targetPos = _target.position + Vector3.up * _targetHeightOffset;
            Vector3 toTarget = targetPos - transform.position;
            _direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
        }
        else
        {
            _direction = transform.forward;
        }

        transform.rotation = Quaternion.LookRotation(_direction);
        transform.SetParent(null);

        _lifetimeTimer = _config.lifetime;
        _interceptCheckDone = false;
        _isReflected = false;

        // Restore hitbox to the attacker's layer in case it was changed by a previous Reflect().
        if (_hitbox != null)
            _hitbox.gameObject.layer = DamageRules.GetWeaponLayer(_attackerFaction);
    }

    private void FixedUpdate()
    {
        _lifetimeTimer -= Time.fixedDeltaTime;
        if (_lifetimeTimer <= 0f)
        {
            Deactivate();
            return;
        }

        _rb.MovePosition(_rb.position + _direction * (_config.speed * Time.fixedDeltaTime));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!gameObject.activeSelf) return;

        int otherMask = 1 << other.gameObject.layer;
        if ((_destructionLayers.value & otherMask) == 0) return;

        Deactivate(true);
    }

    #endregion

    #region Public API

    // Returns true the first time called — ensures a single parry interception even when
    // both EnemyProjectileHitbox and ParryColliderHandler trigger paths fire in the same physics step.
    public bool MarkInterceptCheckDone()
    {
        if (_interceptCheckDone) return false;
        _interceptCheckDone = true;
        return true;
    }

    public void Configure(Component attacker, Enums.Faction attackerFaction, Transform target)
    {
        _attacker = attacker;
        _attackerFaction = attackerFaction;
        _target = target;
    }

    public void OnHitboxContact(Collider other)
    {
        if (!gameObject.activeSelf) return;

        // Parry interception: check before applying damage.
        // Returns true only on a successful parry — failed parry falls through to normal damage.
        var parryTarget = other.GetComponentInParent<IProjectileParryTarget>();
        if (parryTarget != null && parryTarget.TryInterceptProjectile(this))
            return;

        bool damaged = TryDealDamage(other);

        // After a reflect, the hitbox may still overlap the reflector's collider in the same physics
        // step. Skip deactivation if no damage was dealt — the projectile is now flying away.
        if (damaged || !_isReflected)
            Deactivate(true);
    }

    // Reverses the projectile's direction and re-attributes it to the reflector.
    // The hitbox layer is switched so the reflected projectile can damage the original attacker's faction.
    public void Reflect(Component newAttacker, Enums.Faction newFaction)
    {
        _attacker = newAttacker;
        _attackerFaction = newFaction;
        _direction = -_direction;
        _lifetimeTimer = _config != null ? _config.lifetime : 4f;
        _isReflected = true;
        transform.rotation = Quaternion.LookRotation(_direction);

        if (_hitbox != null)
            _hitbox.gameObject.layer = DamageRules.GetWeaponLayer(newFaction);
    }

    #endregion

    #region Helpers

    private bool TryDealDamage(Collider other)
    {
        if (_config == null) return false;

        var damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null) return false;

        var factionComp = other.GetComponentInParent<FactionComponent>();
        Enums.Faction targetFaction = factionComp != null
            ? factionComp.Faction
            : (_attackerFaction == Enums.Faction.Player ? Enums.Faction.Skeleton : Enums.Faction.Player);

        if (!DamageRules.CanDamage(_attackerFaction, targetFaction)) return false;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 hitNormal = hitPoint - transform.position;
        if (hitNormal.sqrMagnitude < 0.0001f) hitNormal = _direction;
        else hitNormal.Normalize();

        damageable.TakeDamage(_config.damage, hitPoint, hitNormal, _config.hitType, _config.poiseDamage);
        return true;
    }

    public void DeactivateWithEffect() => Deactivate(true);

    // Pool re-positions the GO before calling SetActive(true).
    // No parent management needed here.
    private void Deactivate(bool spawnEffect = false)
    {
        if (spawnEffect && _hitEffectPrefab != null)
            Instantiate(_hitEffectPrefab, transform.position, Quaternion.identity);

        gameObject.SetActive(false);
    }

    #endregion
}
