using UnityEngine;

// Child of EnemyProjectile. Collider must be on the EnemyDealDamage layer
// so Unity's physics matrix routes it only against the player hitbox.
// All damage logic lives in the parent — this component is a thin delegate.
[RequireComponent(typeof(Collider))]
public sealed class EnemyProjectileHitbox : MonoBehaviour
{
    private EnemyProjectile _owner;

    private void Awake()
    {
        _owner = GetComponentInParent<EnemyProjectile>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_owner == null)
            Debug.LogError("[EnemyProjectileHitbox] No EnemyProjectile found in parent.", this);
#endif
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_owner == null) return;
        _owner.OnHitboxContact(other);
    }
}
