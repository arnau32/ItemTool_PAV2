using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// Plays a full fade-in → midpoint → fade-out sequence.
/// Registers itself in a static slot so DialogueAction ScriptableObjects can reach it.
/// [ASSUMPTION] Only one sequencer exists per scene. If multiple scenes are additively loaded
/// with their own sequencer, the last one to Awake wins.
public class FadeTransitionSequencer : MonoBehaviour
{
    #region Fields

    [SerializeField] private GDTFadeEffect _fadeIn;
    [SerializeField] private GDTFadeEffect _fadeOut;

    [Tooltip("Fired at full opacity, between fade-in and fade-out. Wire NPC unlocks / teleports here.")]
    [SerializeField] private UnityEvent _onMidpoint;

    private static FadeTransitionSequencer _instance;
    private Coroutine _activeTransition;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    #endregion

    #region Public API

    public static bool TryGetInstance(out FadeTransitionSequencer sequencer)
    {
        sequencer = _instance;
        return _instance != null;
    }

    /// <summary>
    /// Starts the transition. Fires _onMidpoint (Inspector) at full opacity.
    /// Safe to call from UnityEvents.
    /// </summary>
    public void PlayTransition()
    {
        PlayTransitionWithAction(null);
    }

    /// <summary>
    /// Starts the transition and additionally invokes <paramref name="midpointAction"/> at full
    /// opacity, before _onMidpoint. Used by FadedMoveNpcAction to inject the teleport callback.
    /// </summary>
    public void PlayTransitionWithAction(Action midpointAction)
    {
        if (_activeTransition != null)
            StopCoroutine(_activeTransition);

        _activeTransition = StartCoroutine(TransitionRoutine(midpointAction));
    }

    #endregion

    #region Private

    private IEnumerator TransitionRoutine(Action midpointAction)
    {
        _fadeIn.gameObject.SetActive(true);
        _fadeIn.StartFadeIn();

        yield return new WaitUntil(() => _fadeIn.HasFinished());

        midpointAction?.Invoke();
        _onMidpoint?.Invoke();

        _fadeOut.gameObject.SetActive(true);
        _fadeOut.StartFadeOut();

        _activeTransition = null;
    }

    #endregion
}
