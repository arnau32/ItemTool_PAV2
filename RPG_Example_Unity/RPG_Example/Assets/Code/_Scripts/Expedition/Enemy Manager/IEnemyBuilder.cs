using UnityEngine;

namespace Gameplay.Enemies
{
    public interface IEnemyBuilder
    {
        EnemyBase BuildEnemy(EnemyDefinition definition, Vector3 position, Quaternion rotation);
    }
}
