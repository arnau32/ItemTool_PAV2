using System;
using System.Collections;
using UnityEngine;

public class TutorialCombatHint : MonoBehaviour
{
    #region Events

    public event Action OnComplete;

    #endregion

    #region Fields

    [SerializeField] private PlayerInputs _playerInputs;
    [SerializeField] private EnemyBase _enemy;
    [SerializeField] private CanvasGroup _background;
    [SerializeField] private CanvasGroup _dodgeHint;
    [SerializeField] private float _slowTimeScale = 0.1f;
    [SerializeField] private float _fadeDuration  = 0.4f;

    private Coroutine _activeSequence;

    private Action<bool> _onAttackingHandler;
    private Action       _onDodgeHandler;

    private bool _enemyAttacked;
    private bool _playerDodged;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (_background != null) SetHintState(_background, false);
        SetHintState(_dodgeHint, false);
    }

    private void OnEnable()
    {
        _enemyAttacked = false;
        _playerDodged  = false;
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
        SetHintState(_dodgeHint, false);
    }

    #endregion

    #region Sequence

    private IEnumerator RunSequence()
    {
        var anim = _enemy?.Context?.Animation;

        bool attackStarted = false;
        bool attackEnded   = false;

        _onAttackingHandler = attacking =>
        {
            if (attacking) attackStarted = true;
            else           attackEnded   = true;
        };

        if (anim != null)
            anim.OnAttacking += _onAttackingHandler;

        _onDodgeHandler = () => _playerDodged = true;
        _playerInputs.OnDodge += _onDodgeHandler;

        while (!_playerDodged)
        {
            // Wait for the enemy to start an attack.
            attackStarted = false;
            attackEnded   = false;
            yield return new WaitUntil(() => attackStarted);

            // Slow time and show hint.
            Time.timeScale      = _slowTimeScale;
            Time.fixedDeltaTime = 0.02f * _slowTimeScale;

            if (_background != null)
            {
                SetHintState(_background, true);
                yield return FadeUnscaled(_background, 0f, 1f);
            }

            SetHintState(_dodgeHint, true);
            yield return FadeUnscaled(_dodgeHint, 0f, 1f);

            // Wait until the player dodges OR the attack window ends.
            yield return new WaitUntil(() => _playerDodged || attackEnded);

            if (_playerDodged) break;

            // Attack ended without a dodge — restore time and hide canvas.
            RestoreTime();

            yield return FadeUnscaled(_dodgeHint, 1f, 0f);
            SetHintState(_dodgeHint, false);

            if (_background != null)
            {
                yield return FadeUnscaled(_background, 1f, 0f);
            }
        }

        _playerInputs.OnDodge -= _onDodgeHandler;
        _onDodgeHandler = null;

        if (anim != null)
            anim.OnAttacking -= _onAttackingHandler;
        _onAttackingHandler = null;

        // Restore time and fade out on successful dodge.
        RestoreTime();

        yield return FadeUnscaled(_dodgeHint, 1f, 0f);
        SetHintState(_dodgeHint, false);

        if (_background != null)
            yield return FadeUnscaled(_background, 1f, 0f);

        OnComplete?.Invoke();
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

        if (_playerInputs != null && _onDodgeHandler != null)
        {
            _playerInputs.OnDodge -= _onDodgeHandler;
            _onDodgeHandler = null;
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
