using System.Collections;
using UnityEngine;

public class TutorialAbilityHint : MonoBehaviour
{
    #region Fields

    [SerializeField] private CanvasGroup _background;
    [SerializeField] private CanvasGroup _abilityHint;
    [SerializeField] private float _fadeDuration = 0.3f;

    private Coroutine _activeSequence;
    private System.Action _onAbilityUsedDismiss;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (_background != null) SetState(_background, false);
        SetState(_abilityHint, false);
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

        if (_onAbilityUsedDismiss != null)
        {
            AbilityScoreSystem.OnAbilityReady -= _onAbilityUsedDismiss;
            _onAbilityUsedDismiss = null;
        }

        if (_background != null) SetState(_background, false);
        SetState(_abilityHint, false);
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

        SetState(_abilityHint, true);
        yield return Fade(_abilityHint, 0f, 1f);

        bool dismissed = false;
        _onAbilityUsedDismiss = () => dismissed = true;
        AbilityScoreSystem.OnAbilityReady += _onAbilityUsedDismiss;

        while (!dismissed)
            yield return null;

        AbilityScoreSystem.OnAbilityReady -= _onAbilityUsedDismiss;
        _onAbilityUsedDismiss = null;

        yield return FadeOut(_abilityHint);
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
