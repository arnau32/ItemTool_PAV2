using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;

public class NoteUI : MonoBehaviour
{
    #region Fields

    public static event Action OnOpened;
    public static event Action OnClosed;

    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _bodyText;

    private LocalizedString _title;
    private LocalizedString _body;

    private InputService _inputService;

    #endregion

    #region Public API

    public void Open(LocalizedString title, LocalizedString body)
    {
        _inputService = GameServices.Get<InputService>();

        _title = title;
        _body = body;

        _title.StringChanged += OnTitleChanged;
        _body.StringChanged += OnBodyChanged;

        _inputService.Actions.UI.Cancel.performed += HandleCancel;

        OnOpened?.Invoke();
    }

    public void Close()
    {
        UnsubscribeAll();
        GameServices.Get<InputService>()?.OnUIClose();
        OnClosed?.Invoke();
        Destroy(gameObject);
    }

    #endregion

    #region Unity Callbacks

    private void OnDestroy()
    {
        UnsubscribeAll();
    }

    #endregion

    #region Private

    private void OnTitleChanged(string value)
    {
        if (_titleText != null)
            _titleText.text = value;
    }

    private void OnBodyChanged(string value)
    {
        if (_bodyText != null)
            _bodyText.text = value;
    }

    private void HandleCancel(InputAction.CallbackContext ctx)
    {
        Close();
    }

    private void UnsubscribeAll()
    {
        if (_title != null)
        {
            _title.StringChanged -= OnTitleChanged;
            _title = null;
        }

        if (_body != null)
        {
            _body.StringChanged -= OnBodyChanged;
            _body = null;
        }

        if (_inputService != null)
        {
            _inputService.Actions.UI.Cancel.performed -= HandleCancel;
            _inputService = null;
        }
    }

    #endregion
}
