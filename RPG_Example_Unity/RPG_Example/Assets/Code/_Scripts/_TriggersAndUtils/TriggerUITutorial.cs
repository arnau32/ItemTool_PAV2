using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class TriggerUITutorial : TriggerPlayer
{
    [SerializeField] private GameObject _prefabUITutorial;
    [SerializeField] private float _fadeDuration = 0.4f;

    private CanvasGroup _canvasGroup;
    private Coroutine _activeFade;

    #region Unity Callbacks

    private void Awake()
    {
        _canvasGroup = _prefabUITutorial.GetComponent<CanvasGroup>();
        _prefabUITutorial.SetActive(false);
    }

    #endregion

    #region Trigger

    protected override void OnPlayerTriggerEnter(Collider other)
    {
        SwapFade(0f, 1f, activate: true);
    }

    protected override void OnPlayerTriggerExit(Collider other)
    {
        SwapFade(1f, 0f, activate: false);
    }

    #endregion

    #region Helpers

    private void SwapFade(float from, float to, bool activate)
    {
        if (_activeFade != null)
            StopCoroutine(_activeFade);

        _activeFade = StartCoroutine(FadeRoutine(from, to, activate));
    }

    private IEnumerator FadeRoutine(float from, float to, bool activateOnStart)
    {
        if (activateOnStart)
            _prefabUITutorial.SetActive(true);

        if (_canvasGroup != null)
        {
            float elapsed = 0f;
            _canvasGroup.alpha = from;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / _fadeDuration);
                yield return null;
            }
            _canvasGroup.alpha = to;
        }

        if (!activateOnStart)
            _prefabUITutorial.SetActive(false);

        _activeFade = null;
    }

    #endregion
}
