using UnityEngine;
using Gameplay.Enemies;

[DisallowMultipleComponent]
public class ExpeditionServiceBootstrap : MonoBehaviour
{
    [Header("Expedition Services")] [SerializeField]
    private AlertPropagationService _alertPropagation;

    [SerializeField] private AttackTokenService _attackTokenService;
    [SerializeField] private EnemyManager _enemyManager;
    [SerializeField] private EnemyPool _enemyPool;
    [SerializeField] private LootManager _lootManager;
    [SerializeField] private ExpeditionManager _expeditionManager;

    private void Awake()
    {
        if (_alertPropagation != null) GameServices.Register(_alertPropagation);
        if (_attackTokenService != null) GameServices.Register(_attackTokenService);
        if (_enemyManager != null) GameServices.Register(_enemyManager);
        if (_enemyPool != null) GameServices.Register(_enemyPool);
        if (_lootManager != null) GameServices.Register(_lootManager);
        if (_expeditionManager != null) GameServices.Register(_expeditionManager);
    }

    private void OnDestroy()
    {
        GameServices.Unregister<AlertPropagationService>();
        GameServices.Unregister<AttackTokenService>();
        GameServices.Unregister<EnemyManager>();
        GameServices.Unregister<EnemyPool>();
        GameServices.Unregister<LootManager>();
        GameServices.Unregister<ExpeditionManager>();
    }
}