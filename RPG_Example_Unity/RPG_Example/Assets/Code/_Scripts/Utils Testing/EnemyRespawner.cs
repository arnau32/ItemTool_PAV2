using UnityEngine;

[RequireComponent(typeof(EnemyBase))]
public class EnemyRespawner : MonoBehaviour
{
    #region Fields

    [SerializeField] private float _respawnDelay = 3f;

    private EnemyBase _enemy;
    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;
    }

    private void OnEnable()
    {
        _enemy.OnEnemyDied += HandleDied;
    }

    private void OnDisable()
    {
        _enemy.OnEnemyDied -= HandleDied;
    }

    #endregion

    #region Private

    private void HandleDied(EnemyBase _)
    {
        WaitExtension.Wait(_respawnDelay, Respawn);
    }

    private void Respawn()
    {
        transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
        gameObject.SetActive(false);
        gameObject.SetActive(true);
    }

    #endregion
}
