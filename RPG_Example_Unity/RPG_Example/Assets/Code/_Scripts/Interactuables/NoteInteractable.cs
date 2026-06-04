using UnityEngine;
using UnityEngine.Localization;

public class NoteInteractable : BaseInteractable
{
    #region Fields

    [Header("Note Content")]
    [SerializeField] private LocalizedString _title;
    [SerializeField] private LocalizedString _body;

    [Header("References")]
    [SerializeField] private NoteUI _notePrefab;
    [SerializeField] private RectTransform _hudCanvas;

    private InputService _inputService;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        _inputService = GameServices.Get<InputService>();
    }

    #endregion

    #region IInteractable

    public override void OnInteract()
    {
        base.OnInteract();

        if (_notePrefab == null)
        {
            Debug.LogWarning("[NoteInteractable] NoteUI prefab not assigned.", this);
            return;
        }

        if (_hudCanvas == null)
        {
            Debug.LogWarning("[NoteInteractable] HUD canvas not assigned.", this);
            return;
        }

        _inputService.OnUIOpen();

        NoteUI instance = Instantiate(_notePrefab, _hudCanvas);
        instance.Open(_title, _body);
    }

    #endregion
}
