using System.Collections;
using UnityEngine;

public class TutorialConsumableHint : MonoBehaviour
{
    #region Fields

    [SerializeField] private PlayerInputs _playerInputs;
    [SerializeField] private CanvasGroup _background;
    [SerializeField] private CanvasGroup _consumableHint;
    [SerializeField] private float _displayTimeout = 12f;
    [SerializeField] private float _fadeDuration = 0.4f;

    private Coroutine _activeSequence;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (_background != null) SetHintState(_background, false);
        SetHintState(_consumableHint, false);
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
        if (_background != null) SetHintState(_background, false);
        SetHintState(_consumableHint, false);
    }

    #endregion

    #region Sequence

    private IEnumerator RunSequence()
    {
        if (_background != null)
        {
            SetHintState(_background, true);
            yield return Fade(_background, 0f, 1f);
        }

        SetHintState(_consumableHint, true);
        yield return Fade(_consumableHint, 0f, 1f);

        bool dismissed = false;
        void OnUseConsumable(Vector2 _) { dismissed = true; }
        _playerInputs.OnUseConsumable += OnUseConsumable;

        float timer = 0f;
        while (!dismissed && timer < _displayTimeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        _playerInputs.OnUseConsumable -= OnUseConsumable;

        yield return FadeOut(_consumableHint);

        if (_background != null) yield return Fade(_background, 1f, 0f);
        enabled = false;
    }

    #endregion

    #region Helpers

    private IEnumerator FadeOut(CanvasGroup cg)
    {
        yield return Fade(cg, 1f, 0f);
        SetHintState(cg, false);
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

    private static void SetHintState(CanvasGroup cg, bool active)
    {
        cg.alpha = 0f;
        cg.gameObject.SetActive(active);
    }

    #endregion
}
