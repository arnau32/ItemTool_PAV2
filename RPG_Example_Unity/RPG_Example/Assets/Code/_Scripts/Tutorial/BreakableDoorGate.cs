using UnityEngine;

/// Hides a set of GameObjects until the player completes the movement tutorial
/// (move + sprint). While the player is in proximity and the tutorial is pending,
/// a world-space canvas prompts them to finish the tutorial first.
public class BreakableDoorGate : MonoBehaviour
{
    [SerializeField] private TutorialMovementHint _movementHint;
    [SerializeField] private GameObject[] _toHide;
    [SerializeField] private GameObject _proximityCanvas;
    [SerializeField] private Transform _playerTransform;
    [SerializeField] private float _proximityRadius = 3f;

    private bool _unlocked;
    private bool _canvasVisible;

    #region Unity Callbacks

    private void Awake()
    {
        if (_proximityCanvas != null)
            _proximityCanvas.SetActive(false);

        SetHidden(true);
    }

    private void Start()
    {
        if (_movementHint == null) return;

        if (_movementHint.IsCompleted)
        {
            Unlock();
            return;
        }

        _movementHint.OnMovementCompleted += Unlock;
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
        if (_movementHint != null)
            _movementHint.OnMovementCompleted -= Unlock;
    }

    #endregion

    #region Private

    private void Unlock()
    {
        if (_unlocked) return;
        _unlocked = true;

        if (_movementHint != null)
            _movementHint.OnMovementCompleted -= Unlock;

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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _proximityRadius);
    }

    #endregion
}
