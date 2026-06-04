using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.UI;

public class BlockingTutorialUI : MonoBehaviour
{
    #region Fields

    [Header("Content")]
    [SerializeField] private LocalizedString _title;
    [SerializeField] private LocalizedString _description;
    [SerializeField] private Sprite _image;

    [Header("References")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private Image _imageComponent;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Settings")]
    [SerializeField] private float _fadeDuration = 0.3f;

    private InputService _inputService;
    private TimeScaleManager _timeScaleManager;
    private bool _isOpen;

    private const string TIME_SLOW_ID = "BlockingTutorial";

    #endregion

    #region Public API

    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;

        _inputService    = GameServices.Get<InputService>();
        _timeScaleManager = GameServices.Get<TimeScaleManager>();

        if (_imageComponent != null && _image != null)
            _imageComponent.sprite = _image;

        _title.StringChanged       += OnTitleChanged;
        _description.StringChanged += OnDescriptionChanged;

        _inputService.OnUIOpen();
        _timeScaleManager.RequestSlow(TIME_SLOW_ID, 0f);
        _inputService.Actions.UI.Cancel.performed += HandleClose;

        gameObject.SetActive(true);
        _canvasGroup.alpha = 0f;
        StartCoroutine(FadeRoutine(1f));
    }

    // Wirable to a Button.OnClick in the inspector.
    public void Close()
    {
        if (!_isOpen) return;

        Unsubscribe();
        _timeScaleManager?.ReleaseSlow(TIME_SLOW_ID);
        _inputService?.OnUIClose();
        _inputService     = null;
        _timeScaleManager = null;

        Destroy(gameObject);
    }

    #endregion

    #region Unity Callbacks

    private void OnDestroy()
    {
        Unsubscribe();
    }

    #endregion

    #region Private

    private void OnTitleChanged(string value)
    {
        if (_titleText != null) _titleText.text = value;
    }

    private void OnDescriptionChanged(string value)
    {
        if (_descriptionText != null) _descriptionText.text = value;
    }

    private void HandleClose(InputAction.CallbackContext ctx) => Close();

    private void Unsubscribe()
    {
        if (!_isOpen) return;
        _isOpen = false;

        _title.StringChanged       -= OnTitleChanged;
        _description.StringChanged -= OnDescriptionChanged;

        if (_inputService != null)
            _inputService.Actions.UI.Cancel.performed -= HandleClose;
    }

    private IEnumerator FadeRoutine(float target)
    {
        float start   = _canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed           += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / _fadeDuration);
            yield return null;
        }

        _canvasGroup.alpha = target;
    }

    #endregion
}
