using UnityEngine;

/// <summary>
/// World-space quest indicator for Morran's contract system.
/// Mirrors NpcQuestIndicator but reads state from MorranContractService instead of QuestService.
/// States:
///   CanStart  — no active contract  → show "?"
///   InProgress — contract active, not completable → show progress mark
///   CanFinish  — contract active and player has all items → show "!"
/// </summary>
public class MorranContractIndicator : MonoBehaviour
{
    #region Fields

    [SerializeField] private SpriteRenderer _iconRenderer;

    [Header("Sprites")]
    [SerializeField] private Sprite _canStartSprite;
    [SerializeField] private Sprite _inProgressSprite;
    [SerializeField] private Sprite _canFinishSprite;

    [SerializeField] private Color _canStartColor   = Color.yellow;
    [SerializeField] private Color _inProgressColor = Color.white;
    [SerializeField] private Color _canFinishColor  = Color.green;

    private MorranContractService _svc;
    private Transform             _cam;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        _cam = Camera.main?.transform;

        if (!GameServices.TryGet<MorranContractService>(out _svc))
        {
            gameObject.SetActive(false);
            return;
        }

        _svc.OnContractChanged += OnContractChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_svc != null)
            _svc.OnContractChanged -= OnContractChanged;
    }

    private void LateUpdate()
    {
        if (_cam == null || !_iconRenderer.enabled) return;
        _iconRenderer.transform.forward = _cam.forward;
    }

    #endregion

    #region Private

    private void OnContractChanged(MorranContractSO _) => Refresh();

    private void Refresh()
    {
        if (_svc.CanComplete)
        {
            SetIcon(_canFinishSprite, _canFinishColor);
            return;
        }

        if (_svc.HasActive)
        {
            SetIcon(_inProgressSprite, _inProgressColor);
            return;
        }

        SetIcon(_canStartSprite, _canStartColor);
    }

    private void SetIcon(Sprite sprite, Color c)
    {
        _iconRenderer.sprite  = sprite;
        _iconRenderer.enabled = sprite != null;
        _iconRenderer.color   = c;
    }

    #endregion
}
