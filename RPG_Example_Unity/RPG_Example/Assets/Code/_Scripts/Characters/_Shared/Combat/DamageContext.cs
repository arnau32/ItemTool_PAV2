using UnityEngine;

public readonly struct DamageContext
{
    // Ownership
    public readonly Component Attacker;
    public readonly Component Source;
    public readonly Enums.Faction AttackerFaction;

    // Target
    public readonly IDamageable Target;
    public readonly Enums.Faction TargetFaction;

    // Contact
    public readonly Vector3 HitPoint;
    public readonly Vector3 HitNormal;

    // Metadata
    public readonly Enums.WeaponHand WeaponHand;
    public readonly Enums.ColliderSlot Slot;

    public DamageContext(
        Component attacker,
        Component source,
        Enums.Faction attackerFaction,
        IDamageable target,
        Enums.Faction targetFaction,
        Vector3 hitPoint,
        Vector3 hitNormal,
        Enums.WeaponHand weaponHand,
        Enums.ColliderSlot slot)
    {
        Attacker = attacker;
        Source = source;
        AttackerFaction = attackerFaction;
        Target = target;
        TargetFaction = targetFaction;
        HitPoint = hitPoint;
        HitNormal = hitNormal;
        WeaponHand = weaponHand;
        Slot = slot;
    }
}