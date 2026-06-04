using UnityEngine;
using Gameplay.Enemies;

public class EnemySpawnTest : MonoBehaviour
{
    public EnemyDefinition enemyDefinition;
    public int   count = 5;
    public Vector3 area = new Vector3(10, 0, 10);

    private void Start()
    {
        if (!GameServices.TryGet<EnemyManager>(out var enemyManager))
        {
            Debug.LogError("[EnemySpawnTest] EnemyManager not found in GameServices.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            var offset = new Vector3(
                Random.Range(-area.x * 0.5f, area.x * 0.5f),
                0f,
                Random.Range(-area.z * 0.5f, area.z * 0.5f));

            enemyManager.SpawnEnemy(enemyDefinition, transform.position + offset, Quaternion.identity);
        }
    }
}