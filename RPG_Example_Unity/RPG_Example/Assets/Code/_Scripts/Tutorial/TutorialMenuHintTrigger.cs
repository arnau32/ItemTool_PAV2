using System.Collections;
using UnityEngine;

public class TutorialMenuHintTrigger : MonoBehaviour
{
    [SerializeField] private EnemyBase _enemy;
    [SerializeField] private TutorialMenuHint _menuHint;

    #region Unity Callbacks

    private void Start()
    {
        _enemy.Context.Health.OnDeath += OnEnemyDied;
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    #endregion

    #region Sequence

    private void OnEnemyDied()
    {
        _enemy.Context.Health.OnDeath -= OnEnemyDied;
        TabViewManager.Instance.TabOpen += OnTabOpened;
    }

    private void OnTabOpened(TabType type)
    {
        if (type != TabType.Inventory) return;
        StartCoroutine(CheckLootScreenActive());
    }

    private IEnumerator CheckLootScreenActive()
    {
        yield return null;
        if (!PlayerInventory.Instance.loots.isActive) yield break;

        TabViewManager.Instance.TabOpen -= OnTabOpened;
        TabViewManager.Instance.OnTabClose += OnTabClosed;
    }

    private void OnTabClosed()
    {
        TabViewManager.Instance.OnTabClose -= OnTabClosed;
        _menuHint.enabled = true;
        enabled = false;
    }

    #endregion

    #region Helpers

    private void Unsubscribe()
    {
        if (_enemy != null)
            _enemy.Context.Health.OnDeath -= OnEnemyDied;

        if (TabViewManager.Instance != null)
        {
            TabViewManager.Instance.TabOpen -= OnTabOpened;
            TabViewManager.Instance.OnTabClose -= OnTabClosed;
        }
    }

    #endregion
}
