using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class VfxPoolService : MonoBehaviour, IGameServices
{
    [Serializable]
    public struct PrewarmEntry
    {
        public ParticleSystem prefab;
        public int count;
    }

    [Header("Optional Prewarm")]
    [SerializeField] private List<PrewarmEntry> _prewarm = new List<PrewarmEntry>();

    private readonly Dictionary<int, ObjectPool<ParticleSystem>> _pools = new Dictionary<int, ObjectPool<ParticleSystem>>(32);

    private void Awake()
    {
        Prewarm();
    }

    public ParticleSystem Spawn(ParticleSystem prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;

        var pool = GetOrCreatePool(prefab);
        var ps = pool.Get();

        var t = ps.transform;
        t.SetParent(parent, false);
        t.SetPositionAndRotation(position, rotation);

        ps.gameObject.SetActive(true);
        ps.Clear(true);
        ps.Play(true);

        return ps;
    }

    private void Prewarm()
    {
        for (int i = 0; i < _prewarm.Count; i++)
        {
            var p = _prewarm[i].prefab;
            if (p == null) continue;

            var pool = GetOrCreatePool(p);
            int count = Mathf.Max(0, _prewarm[i].count);

            for (int k = 0; k < count; k++)
            {
                var inst = pool.Get();
                pool.Release(inst);
            }
        }
    }

    private ObjectPool<ParticleSystem> GetOrCreatePool(ParticleSystem prefab)
    {
        int key = prefab.GetInstanceID();
        if (_pools.TryGetValue(key, out var existing)) return existing;

        ObjectPool<ParticleSystem> pool = null;

        pool = new ObjectPool<ParticleSystem>(
            createFunc: () =>
            {
                var inst = Instantiate(prefab, transform);
                inst.gameObject.SetActive(false);

                var autoReturn = inst.GetComponent<PooledParticleAutoReturn>();
                if (autoReturn == null) autoReturn = inst.gameObject.AddComponent<PooledParticleAutoReturn>();

                autoReturn.Bind(() => pool.Release(inst));
                return inst;
            },
            actionOnGet: ps => { },
            actionOnRelease: ps =>
            {
                if (ps == null) return;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);
                ps.transform.SetParent(transform, false);
            },
            actionOnDestroy: ps =>
            {
                if (ps != null) Destroy(ps.gameObject);
            },
            collectionCheck: false,
            defaultCapacity: 8,
            maxSize: 64
        );

        _pools[key] = pool;
        return pool;
    }
}
