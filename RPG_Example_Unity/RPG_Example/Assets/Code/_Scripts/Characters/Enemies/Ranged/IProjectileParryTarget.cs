// Implemented by any character that can intercept a flying EnemyProjectile via a parry action.
// Returning true means the projectile was successfully caught — the caller must NOT apply damage.
public interface IProjectileParryTarget
{
    bool TryInterceptProjectile(EnemyProjectile projectile);
}
