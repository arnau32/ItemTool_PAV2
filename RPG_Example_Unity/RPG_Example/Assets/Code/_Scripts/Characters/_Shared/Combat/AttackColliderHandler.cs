using System;
using System.Collections.Generic;
using UnityEngine;

public class AttackColliderHandler : MonoBehaviour
{
    [SerializeField] private Collider _collider;
    public event Action<DamageContext> OnDealDamage;

    [SerializeField] private LayerMask _layerToDealDamage;

    [SerializeField] private Enums.WeaponHand _weaponHand;
    [SerializeField] private Enums.ColliderSlot _slot;

    public Enums.WeaponHand WeaponHand => _weaponHand;
    public Enums.ColliderSlot Slot => _slot;
    public Collider Collider => _collider;

    private Component _attacker;
    private Component _source;
    private Enums.Faction _attackerFaction;
    private bool _allowFriendlyFire;

    private int _activeAttackInstanceId;
    private int _activeWindowIndex = -1;

    private struct HitState
    {
        public int count;
        public float lastTime;
    }

    private readonly Dictionary<int, HitState> _hitByTarget = new Dictionary<int, HitState>(16);

    private struct CachedHitTarget
    {
        public IDamageable Damageable;
        public FactionComponent Faction;
    }

    private static readonly Dictionary<int, CachedHitTarget> _hitTargetCache = new(32);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearStaticCaches()
    {
        _hitTargetCache.Clear();
    }


    private void Reset()
    {
        if (_collider == null)
            _collider = GetComponent<Collider>();
    }

    private void Awake()
    {
        if (_collider == null)
            _collider = GetComponent<Collider>();

        DeactivateCollider();
    }

    private void OnDisable()
    {
        DeactivateCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDealDamage(other, false);
    }

    private void OnTriggerStay(Collider other)
    {
        TryDealDamage(other, true);
    }

    private void TryDealDamage(Collider other, bool fromStay)
    {
        if (other == null) return;

        if (_layerToDealDamage.value != 0)
        {
            int otherLayerMask = 1 << other.gameObject.layer;
            if ((_layerToDealDamage.value & otherLayerMask) == 0)
            {
                return;
            }
        }

        if (!TryResolveHitTarget(other, out var damageable, out var targetFaction)) return;

        if (!DamageRules.CanDamage(_attackerFaction, targetFaction, _allowFriendlyFire))
        {
            return;
        }

        int attackerIdForRegistry = GetAttackerId();

        if (!DamageWindowRegistry.TryGet(attackerIdForRegistry, _weaponHand, _slot, out var windowToken) || !windowToken.IsValid)
        {
            return;
        }

        // Detect a new attack/window and reset per-target hit tracking.
        if (_activeAttackInstanceId != windowToken.attackInstanceId || _activeWindowIndex != windowToken.windowIndex)
        {
            _activeAttackInstanceId = windowToken.attackInstanceId;
            _activeWindowIndex      = windowToken.windowIndex;
            _hitByTarget.Clear();
        }

        var asComponent = damageable as Component;
        int targetId = asComponent != null ? asComponent.GetInstanceID() : other.GetInstanceID();
        var now = Time.time;

        _hitByTarget.TryGetValue(targetId, out var hitState);

        if (!windowToken.allowMultiHit)
        {
            if (hitState.count > 0)
            {
                return;
            }
        }
        else
        {
            if (windowToken.maxHitsPerTarget > 0 && hitState.count >= windowToken.maxHitsPerTarget) return;
            if (windowToken.perTargetCooldown > 0f && (now - hitState.lastTime) < windowToken.perTargetCooldown) return;
        }

        hitState.count    += 1;
        hitState.lastTime  = now;
        _hitByTarget[targetId] = hitState;

        OnDealDamage?.Invoke(BuildContext(damageable, targetFaction, other));
    }

    // Unified lookup: resolves both IDamageable and Faction from a single cache entry.
    // On cache hit: 1 dictionary lookup, 0 GetComponent calls.
    // On cache miss: 2 GetComponentInParent calls (same as before), then cached for future hits.
    private bool TryResolveHitTarget(Collider other, out IDamageable damageable, out Enums.Faction targetFaction)
    {
        damageable    = null;
        targetFaction = default;

        int colliderId = other.GetInstanceID();

        if (_hitTargetCache.TryGetValue(colliderId, out var cached))
        {
            // Validate cached references — destroyed objects return null
            if (cached.Damageable is Component comp && comp != null)
            {
                damageable    = cached.Damageable;
                targetFaction = cached.Faction != null
                    ? cached.Faction.Faction
                    : FallbackFaction();
                return true;
            }

            _hitTargetCache.Remove(colliderId);
        }

        damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null) return false;

        FactionComponent factionComp = null;

        var dmgComponent = damageable as Component;
        if (dmgComponent != null)
        {
            factionComp = dmgComponent.GetComponentInParent<FactionComponent>();
        }

        // Fallback: try from the collider's hierarchy (child colliders on different branch)
        if (factionComp == null)
        {
            factionComp = other.GetComponentInParent<FactionComponent>();
        }

        // Cache the result for future hits on this collider
        _hitTargetCache[colliderId] = new CachedHitTarget
        {
            Damageable = damageable,
            Faction    = factionComp
        };

        targetFaction = factionComp != null ? factionComp.Faction : FallbackFaction();
        return true;
    }

    // Fallback faction when no FactionComponent is found.
    private Enums.Faction FallbackFaction() => _attackerFaction == Enums.Faction.Player ? Enums.Faction.Skeleton : Enums.Faction.Player;

    private DamageContext BuildContext(IDamageable target, Enums.Faction targetFaction, Collider other)
    {
        var hitPoint = other != null ? other.ClosestPoint(transform.position) : transform.position;
        var dir = (hitPoint - transform.position);
        dir.y = 0f;
        var normal = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;

        return new DamageContext(
            attacker:        _attacker != null ? _attacker : transform,
            source:          _source   != null ? _source   : this,
            attackerFaction: _attackerFaction,
            target:          target,
            targetFaction:   targetFaction,
            hitPoint:        hitPoint,
            hitNormal:       normal,
            weaponHand:      _weaponHand,
            slot:            _slot);
    }

    public void ConfigureOwner(Component attackerRoot, Component source, Enums.Faction attackerFaction, bool allowFriendlyFire = false)
    {
        _attacker          = attackerRoot;
        _source            = source;
        _attackerFaction   = attackerFaction;
        _allowFriendlyFire = allowFriendlyFire;
    }

    public int GetAttackerId() => _attacker != null ? _attacker.GetInstanceID() : 0;

    public void ActivateCollider()
    {
        _hitByTarget.Clear();
        _activeAttackInstanceId = 0;
        _activeWindowIndex      = -1;

        if (_collider != null)
        {
            _collider.enabled = true;
        }
    }

    public void DeactivateCollider()
    {
        if (_collider != null)
        {
            _collider.enabled = false;
        }

        _hitByTarget.Clear();
        _activeAttackInstanceId = 0;
        _activeWindowIndex      = -1;
    }
}