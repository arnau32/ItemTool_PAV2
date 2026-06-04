using System.Collections;
using UnityEngine;

public class TutorialKnockdownHint : MonoBehaviour
{
    #region Fields

    [SerializeField] private CanvasGroup _background;
    [SerializeField] private CanvasGroup _knockdownHint;
    [SerializeField] private float _displayTimeout = 4f;
    [SerializeField] private float _fadeDuration    = 0.3f;

    private Coroutine _activeSequence;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (_background != null) SetState(_background, false);
        SetState(_knockdownHint, false);
    }

    private void OnEnable()
    {
        _activeSequence = StartCoroutine(RunSequence());
    }

    private void OnDisable()
    {
        if (_activeSequence != null)
        {
            StopCoroutine(_activeSequence);
            _activeSequence = null;
        }

        if (_background != null) SetState(_background, false);
        SetState(_knockdownHint, false);
    }

    #endregion

    #region Sequence

    private IEnumerator RunSequence()
    {
        if (_background != null)
        {
            SetState(_background, true);
            yield return Fade(_background, 0f, 1f);
        }

        SetState(_knockdownHint, true);
        yield return Fade(_knockdownHint, 0f, 1f);

        yield return new WaitForSeconds(_displayTimeout);

        yield return FadeOut(_knockdownHint);
        if (_background != null) yield return Fade(_background, 1f, 0f);

        enabled = false;
    }

    #endregion

    #region Helpers

    private IEnumerator FadeOut(CanvasGroup cg)
    {
        yield return Fade(cg, 1f, 0f);
        SetState(cg, false);
    }

    private IEnumerator Fade(CanvasGroup cg, float from, float to)
    {
        float elapsed = 0f;
        cg.alpha = from;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / _fadeDuration);
            yield return null;
        }
        cg.alpha = to;
    }

    private static void SetState(CanvasGroup cg, bool active)
    {
        cg.alpha = 0f;
        cg.gameObject.SetActive(active);
    }

    #endregion
}
