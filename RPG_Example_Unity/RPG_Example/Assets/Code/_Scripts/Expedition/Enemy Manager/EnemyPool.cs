using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Enemies
{
    public class EnemyPool : MonoBehaviour, IGameServices
    {
        private readonly Dictionary<EnemyDefinition, Stack<EnemyBase>> _inactiveByDefinition = new();

        /// Returns an inactive enemy of the given type, or null if none available.
        /// Does NOT create new instances.
        public EnemyBase GetFromPool(EnemyDefinition definition)
        {
            if (definition == null)
            {
                Debug.LogError("EnemyPool.GetFromPool: definition is null.");
                return null;
            }

            if (_inactiveByDefinition.TryGetValue(definition, out var stack) && stack.Count > 0)
            {
                return stack.Pop();
            }

            return null;
        }

        /// Registers a newly built enemy so it can be reused later.
        /// Normally called by EnemyManager after the builder creates a new instance.
        public void RegisterNew(EnemyBase enemy)
        {
            if (enemy == null || enemy.Definition == null)
            {
                Debug.LogWarning("EnemyPool.RegisterNew: enemy or definition is null.");
                return;
            }

            if (!_inactiveByDefinition.TryGetValue(enemy.Definition, out var stack))
            {
                stack = new Stack<EnemyBase>();
                _inactiveByDefinition[enemy.Definition] = stack;
            }

            stack.Push(enemy);
        }

        /// Returns an enemy to the pool (marks it inactive).
        public void ReleaseToPool(EnemyBase enemy)
        {
            if (enemy == null || enemy.Definition == null)
            {
                Debug.LogWarning("EnemyPool.ReleaseToPool: enemy or definition is null.");
                return;
            }

            enemy.OnReturnToPool();
            enemy.gameObject.SetActive(false);

            if (!_inactiveByDefinition.TryGetValue(enemy.Definition, out var stack))
            {
                stack = new Stack<EnemyBase>();
                _inactiveByDefinition[enemy.Definition] = stack;
            }

            stack.Push(enemy);
        }
    }
}