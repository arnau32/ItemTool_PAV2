using UnityEngine;

[CreateAssetMenu(fileName = "ProjectileConfig", menuName = "Combat/Projectile Config")]
public class ProjectileConfig : ScriptableObject
{
    [Header("Movement")]
    public float speed = 12f;
    public float lifetime = 4f;

    [Header("Damage")]
    public float damage = 20f;
    public float poiseDamage = 15f;
    public Enums.HitType hitType = Enums.HitType.Normal;
}
