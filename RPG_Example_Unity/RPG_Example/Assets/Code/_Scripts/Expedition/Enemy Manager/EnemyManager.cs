using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Enemies
{
    public class EnemyManager : MonoBehaviour, IGameServices
    {
        [Header("Refs")] [SerializeField] private EnemyPool _enemyPool;
        [SerializeField] private MonoBehaviour _builderComponent;

        private IEnemyBuilder _builder;
        private LootManager   _lootManager;

        private readonly List<EnemyBase>     _activeEnemies    = new List<EnemyBase>();
        private readonly HashSet<EnemyBase>  _activeEnemiesSet = new HashSet<EnemyBase>();

        public IReadOnlyList<EnemyBase> ActiveEnemies => _activeEnemies;

        public event Action<EnemyBase> OnEnemySpawned;
        public event Action<EnemyBase> OnEnemyDespawned;
        public event Action<EnemyBase> OnEnemyDied;

        private void Awake()
        {
            _builder = _builderComponent as IEnemyBuilder;
            if (_builder == null)
                Debug.LogError("EnemyManager: _builderComponent no implementa IEnemyBuilder.");
        }

        private void Start()
        {
            if (_enemyPool == null && !GameServices.TryGet(out _enemyPool))
                Debug.LogError("EnemyManager: no hay EnemyPool asignado ni registrado en GameServices.");

            if (!GameServices.TryGet(out _lootManager))
                Debug.LogWarning("EnemyManager: LootManager not found — enemy drops will be skipped.");

            // Detect enemies already placed in the scene.
            EnemyBase[] existingEnemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);

            foreach (var enemy in existingEnemies)
            {
                if (enemy == null) continue;

                if (_activeEnemiesSet.Contains(enemy)) continue;

                enemy.OnEnemyDied -= HandleEnemyDied;
                enemy.OnEnemyDied += HandleEnemyDied;

                _activeEnemies.Add(enemy);
                _activeEnemiesSet.Add(enemy);

                OnEnemySpawned?.Invoke(enemy);
            }
        }

        // Registers a pre-placed scene enemy that was not created via SpawnEnemy.
        // Safe to call even if the enemy was already registered (idempotent).
        public void RegisterEnemy(EnemyBase enemy)
        {
            if (enemy == null) return;

            enemy.OnEnemyDied -= HandleEnemyDied;
            enemy.OnEnemyDied += HandleEnemyDied;

            if (_activeEnemiesSet.Add(enemy))
                _activeEnemies.Add(enemy);

            OnEnemySpawned?.Invoke(enemy);
        }

        public EnemyBase SpawnEnemy(EnemyDefinition definition, Vector3 position, Quaternion rotation)
        {
            EnemyBase enemy = null;

            if (_enemyPool != null)
            {
                enemy = _enemyPool.GetFromPool(definition);
            }

            if (enemy == null)
            {
                if (_builder == null)
                {
                    Debug.LogError("[SpawnEnemy] NO hay builder asignado.");
                    return null;
                }

                enemy = _builder.BuildEnemy(definition, position, rotation);
                if (enemy == null)
                {
                    Debug.LogError("[SpawnEnemy] El builder devolvió null.");
                    return null;
                }

            }
            else
            {
                enemy.transform.SetPositionAndRotation(position, rotation);
                enemy.gameObject.SetActive(true);
                enemy.OnSpawnFromPool();
            }

            enemy.OnEnemyDied -= HandleEnemyDied;
            enemy.OnEnemyDied += HandleEnemyDied;

            if (_activeEnemiesSet.Add(enemy))
            {
                _activeEnemies.Add(enemy);
            }

            OnEnemySpawned?.Invoke(enemy);
            return enemy;
        }

        private void HandleEnemyDied(EnemyBase enemy)
        {
            EnemyDefinition def = enemy.Definition;

            if (def != null && def.lootTable != null && _lootManager != null)
            {
                _lootManager.SpawnLootBag(def.lootTable, enemy.transform.position);
            }

            OnEnemyDied?.Invoke(enemy);
            DespawnEnemy(enemy);
        }

        public void DespawnEnemy(EnemyBase enemy)
        {
            if (enemy == null) return;

            enemy.OnEnemyDied -= HandleEnemyDied;

            if (_activeEnemiesSet.Remove(enemy))
            {
                RemoveEnemySwapBack(enemy);
            }

            _enemyPool?.ReleaseToPool(enemy);
            OnEnemyDespawned?.Invoke(enemy);
        }

        private void RemoveEnemySwapBack(EnemyBase enemy)
        {
            int index = _activeEnemies.IndexOf(enemy);
            if (index < 0) return;

            int lastIndex = _activeEnemies.Count - 1;
            _activeEnemies[index] = _activeEnemies[lastIndex];
            _activeEnemies.RemoveAt(lastIndex);
        }

        public int GetEnemiesInRadius(Vector3 center, float radius, List<EnemyBase> results)
        {
            if (results == null)
            {
                Debug.LogError("EnemyManager.GetEnemiesInRadius recibió una lista null.");
                return 0;
            }

            results.Clear();

            float sqrRadius = radius * radius;

            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;

                Vector3 delta = enemy.transform.position - center;
                if (delta.sqrMagnitude <= sqrRadius)
                {
                    results.Add(enemy);
                }
            }

            return results.Count;
        }
    }
}