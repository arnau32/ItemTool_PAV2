using UnityEngine;
// -- Interface for Damageable Components such as PlayerHealth
public interface IDamageable
{
    bool IsInvulnerable { get; }
    void TakeDamage(float damage, Enums.HitType type);
    void TakeDamage(float damage, Vector3 hitPoint, Vector3 direction, Enums.HitType type);
    void TakeDamage(float damage, Vector3 hitPoint, Vector3 direction, Enums.HitType type, float poiseDamage);
}
