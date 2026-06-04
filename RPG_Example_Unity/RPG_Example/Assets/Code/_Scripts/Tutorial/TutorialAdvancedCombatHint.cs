using System;
using System.Collections;
using UnityEngine;

public class TutorialAdvancedCombatHint : MonoBehaviour
{
    #region Fields

    [SerializeField] private PlayerInputs _playerInputs;
    [SerializeField] private EnemyBase    _enemy;
    [SerializeField] private CanvasGroup  _background;
    [SerializeField] private CanvasGroup  _parryHint;
    [SerializeField] private float _slowTimeScale = 0.1f;
    [SerializeField] private float _slowDelay     = 0.3f;
    [SerializeField] private float _fadeDuration  = 0.4f;

    private Coroutine    _activeSequence;
    private Action<bool> _onAttackingHandler;
    private Action       _onParryHandler;

    private bool _attackStarted;
    private bool _attackEnded;
    private bool _playerParried;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (_background != null) SetHintState(_background, false);
        SetHintState(_parryHint, false);
    }

    private void OnEnable()
    {
        _attackStarted = false;
        _attackEnded   = false;
        _playerParried = false;
        _activeSequence = StartCoroutine(RunSequence());
    }

    private void OnDisable()
    {
        if (_activeSequence != null)
        {
            StopCoroutine(_activeSequence);
            _activeSequence = null;
        }

        UnsubscribeAll();
        RestoreTime();

        if (_background != null) SetHintState(_background, false);
        SetHintState(_parryHint, false);
    }

    #endregion

    #region Sequence

    private IEnumerator RunSequence()
    {
        var anim = _enemy?.Context?.Animation;

        _onAttackingHandler = attacking =>
        {
            if (attacking) _attackStarted = true;
            else           _attackEnded   = true;
        };

        if (anim != null)
            anim.OnAttacking += _onAttackingHandler;

        // Listening to parry input — valid during slow-mo even outside the game window.
        _onParryHandler = () => _playerParried = true;
        _playerInputs.OnParry += _onParryHandler;

        while (!_playerParried)
        {
            _attackStarted = false;
            _attackEnded   = false;

            // Wait for the enemy to start an attack.
            yield return new WaitUntil(() => _attackStarted);

            // Configurable delay before slow-mo kicks in (unscaled).
            float elapsed = 0f;
            while (elapsed < _slowDelay && !_attackEnded && !_playerParried)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_playerParried) break;
            if (_attackEnded) continue;

            // Slow time.
            Time.timeScale      = _slowTimeScale;
            Time.fixedDeltaTime = 0.02f * _slowTimeScale;

            // Fade in.
            if (_background != null)
            {
                SetHintState(_background, true);
                yield return FadeUnscaled(_background, 0f, 1f);
            }

            SetHintState(_parryHint, true);
            yield return FadeUnscaled(_parryHint, 0f, 1f);

            // Wait until the player parries OR the attack ends.
            yield return new WaitUntil(() => _playerParried || _attackEnded);

            if (_playerParried) break;

            // Attack ended without a parry — restore and loop.
            RestoreTime();

            yield return FadeUnscaled(_parryHint, 1f, 0f);
            SetHintState(_parryHint, false);

            if (_background != null)
                yield return FadeUnscaled(_background, 1f, 0f);
        }

        UnsubscribeAll();
        RestoreTime();

        yield return FadeUnscaled(_parryHint, 1f, 0f);
        SetHintState(_parryHint, false);

        if (_background != null)
            yield return FadeUnscaled(_background, 1f, 0f);

        enabled = false;
    }

    #endregion

    #region Helpers

    private void UnsubscribeAll()
    {
        var anim = _enemy?.Context?.Animation;
        if (anim != null && _onAttackingHandler != null)
        {
            anim.OnAttacking -= _onAttackingHandler;
            _onAttackingHandler = null;
        }

        if (_playerInputs != null && _onParryHandler != null)
        {
            _playerInputs.OnParry -= _onParryHandler;
            _onParryHandler = null;
        }
    }

    private static void RestoreTime()
    {
        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    private IEnumerator FadeUnscaled(CanvasGroup cg, float from, float to)
    {
        float elapsed = 0f;
        cg.alpha = from;
        while (elapsed < _fadeDuration)
        {
            elapsed  += Time.unscaledDeltaTime;
            cg.alpha  = Mathf.Lerp(from, to, elapsed / _fadeDuration);
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
