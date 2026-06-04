using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ProjectileVfxRig : VFXRigContainer
{
    #region Fields

    [SerializeField] private List<ProjectileSlotConfig> _projectileSlots;

    private Component _attacker;
    private Enums.Faction _attackerFaction;
    private EnemyPerception _perception;
    private bool _isInitialized;

    // Indexed by slotId for O(1) lookup at activation time.
    private ProjectilePool[] _pools;

    #endregion

    #region Public API

    public void Initialize(EnemyContext ctx)
    {
        _attacker = ctx.Owner.transform;
        _attackerFaction = ctx.FactionComponent.Faction;
        _perception = ctx.Perception;

        BuildPools();

        _isInitialized = true;
    }

    public override void ActivateAttack(int id)
    {
        if (!_isInitialized || _pools == null || id < 0 || id >= _pools.Length || _pools[id] == null)
        {
            base.ActivateAttack(id);
            return;
        }

        var projectile = _pools[id].GetNext();

        if (projectile == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[ProjectileVfxRig] All projectiles in slot {id} are in flight. " +
                             "Increase Pool Size in the ProjectileSlotConfig.", this);
#endif
            return;
        }

        projectile.Configure(_attacker, _attackerFaction, _perception?.CurrentTarget);
        projectile.gameObject.SetActive(true);
    }

    public override void DeactiveAttack(int id)
    {
        if (_pools != null && id >= 0 && id < _pools.Length && _pools[id] != null)
            return;

        base.DeactiveAttack(id);
    }

    #endregion

    #region Helpers

    private void BuildPools()
    {
        if (_projectileSlots == null || _projectileSlots.Count == 0) return;

        int maxId = 0;
        for (int i = 0; i < _projectileSlots.Count; i++)
        {
            if (_projectileSlots[i].SlotId > maxId) maxId = _projectileSlots[i].SlotId;
        }

        _pools = new ProjectilePool[maxId + 1];

        for (int i = 0; i < _projectileSlots.Count; i++)
        {
            var slot = _projectileSlots[i];

            if (slot.Prefab == null)
            {
                Debug.LogError($"[ProjectileVfxRig] Slot {slot.SlotId} has no prefab assigned.", this);
                continue;
            }

            if (slot.Socket == null)
            {
                Debug.LogError($"[ProjectileVfxRig] Slot {slot.SlotId} has no socket assigned.", this);
                continue;
            }

            _pools[slot.SlotId] = new ProjectilePool(slot.Prefab, slot.Socket, slot.PoolSize);
        }
    }

    #endregion

    #region Inner Types

    private sealed class ProjectilePool
    {
        private readonly EnemyProjectile[] _instances;
        private readonly Transform _socket;
        private int _cursor;

        public ProjectilePool(GameObject prefab, Transform socket, int size)
        {
            _socket = socket;
            _instances = new EnemyProjectile[size];

            for (int i = 0; i < size; i++)
            {
                var go = UnityEngine.Object.Instantiate(prefab, socket);
                go.SetActive(false);
                _instances[i] = go.GetComponent<EnemyProjectile>();
            }
        }

        public EnemyProjectile GetNext()
        {
            int count = _instances.Length;

            for (int i = 0; i < count; i++)
            {
                int idx = (_cursor + i) % count;
                var p = _instances[idx];
                if (p != null && !p.gameObject.activeSelf)
                {
                    _cursor = (idx + 1) % count;
                    SnapToSocket(p);
                    return p;
                }
            }

            return null;
        }

        // Re-parents to socket and syncs world position before activation.
        // This handles the case where the projectile is floating at an old position
        // after the enemy moved, or was orphaned by a parent being destroyed.
        private void SnapToSocket(EnemyProjectile projectile)
        {
            projectile.transform.SetParent(_socket);
            projectile.transform.localPosition = Vector3.zero;
            projectile.transform.localRotation = Quaternion.identity;
        }
    }

    #endregion
}

[Serializable]
public sealed class ProjectileSlotConfig
{
    [Tooltip("Must match the vfxId used in the AttackVfxWindow.")]
    public int SlotId;

    [Tooltip("Projectile prefab asset.")]
    public GameObject Prefab;

    [Tooltip("Transform from which the projectile spawns (e.g. bow hand bone).")]
    public Transform Socket;

    [Tooltip("Max simultaneous projectiles in flight for this slot.")]
    [Min(1)] public int PoolSize = 3;
}
