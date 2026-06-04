using UnityEngine;

public static class DamageRules
{
    public static bool CanDamage(Enums.Faction attacker, Enums.Faction target, bool allowFriendlyFire = false)
    {
        if (allowFriendlyFire) return true;

        return attacker != target;
    }
    
    public static int GetWeaponLayer(Enums.Faction faction)
    {
        return faction == Enums.Faction.Player
            ? LayerMask.NameToLayer("DealDamage")
            : LayerMask.NameToLayer("DealDamage");
    }
}
