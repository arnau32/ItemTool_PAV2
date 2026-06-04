using System.Collections;
using UnityEngine;

public class TutorialLockOnHint : MonoBehaviour
{
    #region Fields

    [SerializeField] private PlayerInputs _playerInputs;
    [SerializeField] private PlayerLockOnSystem _lockOnSystem;
    [SerializeField] private CanvasGroup _background;
    [SerializeField] private CanvasGroup _lockOnHint;
    [SerializeField] private CanvasGroup _swapTargetHint;
    [SerializeField] private float _displayTimeout = 8f;
    [SerializeField] private float _fadeDuration = 0.4f;

    private Coroutine _activeSequence;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (_background != null) SetHintState(_background, false);
        SetHintState(_lockOnHint, false);
        SetHintState(_swapTargetHint, false);
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
        SetHintState(_lockOnHint, false);
        SetHintState(_swapTargetHint, false);
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

        SetHintState(_lockOnHint, true);
        SetHintState(_swapTargetHint, true);
        StartCoroutine(Fade(_lockOnHint, 0f, 1f));
        StartCoroutine(Fade(_swapTargetHint, 0f, 1f));

        yield return new WaitForSeconds(_fadeDuration);

        bool lockOnDone = false;
        bool swapDone = false;

        void OnLockOn() { lockOnDone = true; }
        _playerInputs.OnLockOn += OnLockOn;

        float lockOnTimer = 0f;
        float swapTimer = 0f;
        bool lockOnFading = false;
        bool swapFading = false;

        while (!lockOnFading || !swapFading)
        {
            lockOnTimer += Time.deltaTime;
            swapTimer += Time.deltaTime;

            if (!swapDone && _lockOnSystem != null && _lockOnSystem.IsLockedOn)
            {
                Vector2 rightStick = _playerInputs.GetRightStickDirection();
                if (rightStick.sqrMagnitude > 0.09f)
                    swapDone = true;
            }

            if (!lockOnFading && (lockOnDone || lockOnTimer >= _displayTimeout))
            {
                lockOnFading = true;
                StartCoroutine(FadeOut(_lockOnHint));
            }

            if (!swapFading && (swapDone || swapTimer >= _displayTimeout))
            {
                swapFading = true;
                StartCoroutine(FadeOut(_swapTargetHint));
            }

            yield return null;
        }

        _playerInputs.OnLockOn -= OnLockOn;

        yield return new WaitForSeconds(_fadeDuration);

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
