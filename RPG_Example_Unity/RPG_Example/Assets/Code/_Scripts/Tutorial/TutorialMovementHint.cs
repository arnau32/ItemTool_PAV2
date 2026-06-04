using System;
using System.Collections;
using UnityEngine;

public class TutorialMovementHint : MonoBehaviour
{
    [SerializeField] private PlayerInputs _playerInputs;
    [SerializeField] private CanvasGroup _background;
    [SerializeField] private CanvasGroup _moveHint;
    [SerializeField] private CanvasGroup _sprintHint;
    [SerializeField] private float _fadeDuration = 0.4f;

    public event Action OnMovementCompleted;

    public bool IsCompleted => _moveCompleted && _sprintCompleted;

    private bool _moveCompleted;
    private bool _sprintCompleted;
    private Coroutine _activeSequence;

    #region Unity Callbacks

    private void Awake()
    {
        if (_background != null) SetHintState(_background, false);
        SetHintState(_moveHint, false);
        SetHintState(_sprintHint, false);
    }

    private void OnEnable()
    {
        if (_moveCompleted && _sprintCompleted) return;
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
        SetHintState(_moveHint, false);
        SetHintState(_sprintHint, false);
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

        if (!_moveCompleted)
        {
            SetHintState(_moveHint, true);
            StartCoroutine(Fade(_moveHint, 0f, 1f));
        }
        if (!_sprintCompleted)
        {
            SetHintState(_sprintHint, true);
            StartCoroutine(Fade(_sprintHint, 0f, 1f));
        }

        yield return new WaitForSeconds(_fadeDuration);

        bool sprinted = false;
        void OnSprint(bool isSprinting) { if (isSprinting) sprinted = true; }
        _playerInputs.OnSprint += OnSprint;

        while (!_moveCompleted || !_sprintCompleted)
        {
            if (!_moveCompleted && _playerInputs.IsMoving())
            {
                _moveCompleted = true;
                StartCoroutine(FadeOut(_moveHint));
            }

            if (!_sprintCompleted && sprinted)
            {
                _sprintCompleted = true;
                StartCoroutine(FadeOut(_sprintHint));
            }

            yield return null;
        }

        _playerInputs.OnSprint -= OnSprint;

        yield return new WaitForSeconds(_fadeDuration);

        if (_background != null) yield return Fade(_background, 1f, 0f);

        OnMovementCompleted?.Invoke();
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
