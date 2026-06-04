using System;
using UnityEngine;

public class ParryColliderHandler : MonoBehaviour
{
    [SerializeField] private Collider _collider;
    [SerializeField] private LayerMask _enemyWeaponLayer;

    public event Action<AttackColliderHandler> OnParryContact;
    public event Action<EnemyProjectile> OnProjectileParryContact;

    private bool _isActive;

    private void Awake()
    {
        if (_collider == null)
        {
            _collider = GetComponent<Collider>();
        }

        DeactivateCollider();
    }

    private void OnDisable()
    {
        DeactivateCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isActive) return;
        TryParryContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!_isActive) return;
        TryParryContact(other);
    }

    private void TryParryContact(Collider other)
    {
        if (other == null) return;

        if (_enemyWeaponLayer.value != 0)
        {
            int otherLayerMask = 1 << other.gameObject.layer;
            if ((_enemyWeaponLayer.value & otherLayerMask) == 0) return;
        }

        var enemyAttackCollider = other.GetComponent<AttackColliderHandler>();
        if (enemyAttackCollider == null)
            enemyAttackCollider = other.GetComponentInParent<AttackColliderHandler>();

        if (enemyAttackCollider != null)
        {
            int enemyAttackerId = enemyAttackCollider.GetAttackerId();
            if (!DamageWindowRegistry.TryGet(enemyAttackerId, enemyAttackCollider.WeaponHand, enemyAttackCollider.Slot, out var token) || !token.IsValid) return;
            OnParryContact?.Invoke(enemyAttackCollider);
            return;
        }

        // Projectile path: detect the hitbox child of EnemyProjectile.
        var hitbox = other.GetComponent<EnemyProjectileHitbox>();
        if (hitbox == null)
            hitbox = other.GetComponentInParent<EnemyProjectileHitbox>();

        if (hitbox == null) return;

        var projectile = hitbox.GetComponentInParent<EnemyProjectile>();
        if (projectile == null) return;

        OnProjectileParryContact?.Invoke(projectile);
    }

    public void ActivateCollider()
    {
        if (_collider == null) return;
        _collider.enabled = true;
        _isActive = true;
    }

    public void DeactivateCollider()
    {
        if (_collider == null) return;
        _collider.enabled = false;
        _isActive = false;
    }

    public void ConfigureOwner(Component attacker, Enums.Faction faction) { }

    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (_collider == null)
        {
            _collider = GetComponent<Collider>();
        }
    }
    #endif
}