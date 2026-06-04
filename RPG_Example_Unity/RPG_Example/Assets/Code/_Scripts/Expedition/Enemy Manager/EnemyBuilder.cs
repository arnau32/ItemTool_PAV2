using UnityEngine;

namespace Gameplay.Enemies
{
    public class EnemyBuilder : MonoBehaviour, IEnemyBuilder
    {
        // Assigned in Inspector alongside EnemyManager — both live in the same scene prefab.
        [SerializeField] private EnemyPool _enemyPool;

        public EnemyBase BuildEnemy(EnemyDefinition definition, Vector3 position, Quaternion rotation)
        {
            if (definition == null || definition.prefab == null)
            {
                Debug.LogError("EnemyBuilder: definición nula o sin prefab.");
                return null;
            }

            Transform poolParent = _enemyPool != null ? _enemyPool.transform : null;
            var go = Object.Instantiate(definition.prefab, position, rotation, poolParent);
            var enemy = go.GetComponent<EnemyBase>();

            if (enemy == null)
            {
                Debug.LogError($"EnemyBuilder: el prefab {definition.prefab.name} no tiene EnemyBase.");
                Object.Destroy(go);
                return null;
            }

            return enemy;
        }
    }
}