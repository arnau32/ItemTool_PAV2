using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tracks how many enemies are currently in CombatWanderState.
// Drives AudioService.EnterCombatMusic / ExitCombatMusic with a grace timer
// so a brief gap between enemies doesn't cut the combat track.
public class CombatMusicTracker : MonoBehaviour
{
    #region Singleton

    public static CombatMusicTracker Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneUnloaded(Scene scene)
    {
        ForceExitOnDeath();
    }

    #endregion

    #region Fields

    [SerializeField] private float _exitGraceDuration = 4f;

    private int       _activeCombatCount;
    private Coroutine _exitGraceCoroutine;

    #endregion

    #region Public API

    public void NotifyEnterCombat()
    {
        _activeCombatCount++;

        if (_exitGraceCoroutine != null)
        {
            StopCoroutine(_exitGraceCoroutine);
            _exitGraceCoroutine = null;
        }

        if (_activeCombatCount == 1)
            GameServices.Get<AudioService>()?.EnterCombatMusic();
    }

    public void NotifyExitCombat()
    {
        _activeCombatCount = Mathf.Max(0, _activeCombatCount - 1);

        if (_activeCombatCount > 0) return;

        if (_exitGraceCoroutine != null) StopCoroutine(_exitGraceCoroutine);
        _exitGraceCoroutine = StartCoroutine(ExitAfterGrace());
    }

    public void ResetCombatState()
    {
        _activeCombatCount = 0;
        if (_exitGraceCoroutine != null)
        {
            StopCoroutine(_exitGraceCoroutine);
            _exitGraceCoroutine = null;
        }

        GameServices.Get<AudioService>()?.ExitCombatMusic();
    }

    // Called on player death — bypasses the grace timer and fades out immediately.
    public void ForceExitOnDeath()
    {
        _activeCombatCount = 0;
        if (_exitGraceCoroutine != null)
        {
            StopCoroutine(_exitGraceCoroutine);
            _exitGraceCoroutine = null;
        }

        GameServices.Get<AudioService>()?.ForceExitCombatMusic(1f);
    }

    #endregion

    #region Private

    private IEnumerator ExitAfterGrace()
    {
        yield return new WaitForSeconds(_exitGraceDuration);
        _exitGraceCoroutine = null;
        GameServices.Get<AudioService>()?.ExitCombatMusic();
    }

    #endregion
}
