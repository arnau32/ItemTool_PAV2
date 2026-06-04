using System;
using System.Collections.Generic;

public static class DamageWindowRegistry
{
    public readonly struct DamageWindowToken
    {
        public readonly int attackInstanceId;
        public readonly int windowIndex;
        public readonly bool allowMultiHit;
        public readonly float perTargetCooldown;
        public readonly int maxHitsPerTarget;

        public DamageWindowToken(int attackInstanceId, int windowIndex, bool allowMultiHit, float perTargetCooldown, int maxHitsPerTarget)
        {
            this.attackInstanceId = attackInstanceId;
            this.windowIndex = windowIndex;
            this.allowMultiHit = allowMultiHit;
            this.perTargetCooldown = perTargetCooldown;
            this.maxHitsPerTarget = maxHitsPerTarget;
        }

        public bool IsValid => attackInstanceId != 0;
    }

    private static readonly Dictionary<(int attackerId, Enums.WeaponHand hand, Enums.ColliderSlot slot), DamageWindowToken> _active =
        new Dictionary<(int attackerId, Enums.WeaponHand hand, Enums.ColliderSlot slot), DamageWindowToken>(32);

    public static void SetActive(int attackerId, Enums.WeaponHand hand, Enums.ColliderSlot slot, DamageWindowToken token)
    {
        if (attackerId == 0) return;

        var key = (attackerId, hand, slot);

        if (!token.IsValid)
        {
            _active.Remove(key);
            return;
        }

        _active[key] = token;
    }

    public static bool TryGet(int attackerId, Enums.WeaponHand hand, Enums.ColliderSlot slot, out DamageWindowToken token)
    {
        if (attackerId == 0)
        {
            token = default;
            return false;
        }

        return _active.TryGetValue((attackerId, hand, slot), out token);
    }

    public static void ClearSlot(int attackerId, Enums.WeaponHand hand, Enums.ColliderSlot slot)
    {
        if (attackerId == 0) return;
        _active.Remove((attackerId, hand, slot));
    }

    public static void ClearIfMatches(int attackerId, Enums.WeaponHand hand, Enums.ColliderSlot slot, int attackInstanceId, int windowIndex)
    {
        if (attackerId == 0) return;

        var key = (attackerId, hand, slot);

        if (!_active.TryGetValue(key, out var t)) return;

        if (t.attackInstanceId == attackInstanceId && t.windowIndex == windowIndex)
        {
            _active.Remove(key);
        }
    }

    public static void ClearAll()
    {
        _active.Clear();
    }
}
