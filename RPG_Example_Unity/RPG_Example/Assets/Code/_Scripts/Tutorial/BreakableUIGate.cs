using UnityEngine;

/// Hides a set of GameObjects until the player opens both the map and the
/// inventory at least once. While the player is in proximity and the conditions
/// are not met, a world-space canvas prompts them to do so.
public class BreakableUIGate : MonoBehaviour
{
    [SerializeField] private GameObject[] _toHide;
    [SerializeField] private GameObject  _proximityCanvas;
    [SerializeField] private Transform   _playerTransform;
    [SerializeField] private float       _proximityRadius = 3f;

    private bool _mapOpened;
    private bool _inventoryOpened;
    private bool _unlocked;
    private bool _canvasVisible;

    private InputService _inputService;

    #region Unity Callbacks

    private void Awake()
    {
        if (_proximityCanvas != null)
            _proximityCanvas.SetActive(false);

        SetHidden(true);
    }

    private void Start()
    {
        _inputService = GameServices.Get<InputService>();

        if (_inputService != null)
            _inputService.OnMapOpen += OnMapOpenChanged;

        if (TabViewManager.Instance != null)
            TabViewManager.Instance.TabOpen += OnTabOpened;
    }

    private void Update()
    {
        if (_unlocked || _playerTransform == null || _proximityCanvas == null) return;

        bool inRange = Vector3.Distance(transform.position, _playerTransform.position) <= _proximityRadius;

        if (inRange == _canvasVisible) return;

        _canvasVisible = inRange;
        _proximityCanvas.SetActive(inRange);
    }

    private void OnDestroy()
    {
        if (_inputService != null)
            _inputService.OnMapOpen -= OnMapOpenChanged;

        if (TabViewManager.Instance != null)
            TabViewManager.Instance.TabOpen -= OnTabOpened;
    }

    #endregion

    #region Event Handlers

    private void OnMapOpenChanged(bool opened)
    {
        if (!opened || _mapOpened) return;
        _mapOpened = true;
        TryUnlock();
    }

    private void OnTabOpened(TabType type)
    {
        if (type != TabType.Inventory || _inventoryOpened) return;
        _inventoryOpened = true;
        TryUnlock();
    }

    #endregion

    #region Private

    private void TryUnlock()
    {
        if (!_mapOpened || !_inventoryOpened) return;
        Unlock();
    }

    private void Unlock()
    {
        if (_unlocked) return;
        _unlocked = true;

        if (_inputService != null)
            _inputService.OnMapOpen -= OnMapOpenChanged;

        if (TabViewManager.Instance != null)
            TabViewManager.Instance.TabOpen -= OnTabOpened;

        SetHidden(false);

        if (_proximityCanvas != null)
            _proximityCanvas.SetActive(false);

        enabled = false;
    }

    private void SetHidden(bool hidden)
    {
        for (int i = 0; i < _toHide.Length; i++)
        {
            if (_toHide[i] != null && _toHide[i] != gameObject)
                _toHide[i].SetActive(!hidden);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _proximityRadius);
    }

    #endregion
}
