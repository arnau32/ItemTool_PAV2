using UnityEngine;

// Manages the lifecycle of any PlayerPlayableAction (consume, chest open, dialogue idle, etc.).
// Completion is driven by an Animation Event (OnPlayableActionComplete via PlayerAnimation)
// Interruptions from combat and dodge are handled via TryInterrupt().

public class PlayerPlayableController : MonoBehaviour
{
    #region Fields

    [Tooltip("Max seconds an action can run before being force-cleared as a stuck-state safeguard.")]
    [SerializeField] private float _actionTimeout = 1.5f;

    private PlayerContext _context;

    private PlayerPlayableAction _activeAction;
    private bool _isInAction;
    private float _actionStartTime;

    private bool _isInDialogue;

    #endregion

    #region Properties

    public bool IsInAction => _isInAction;
    public bool IsInDialogue => _isInDialogue;
    public bool BlocksMovement => _isInAction && _activeAction != null && !_activeAction.allowMovement;
    public bool BlocksAttack => _isInAction && _activeAction != null && _activeAction.blockAttack;

    #endregion

    #region Public API

    public void Initialize(PlayerContext context)
    {
        _context = context;
    }

    // Starts an animated action. Silently rejected if another action is already running.
    public bool StartAction(PlayerPlayableAction action)
    {
        if (_isInAction) return false;
        if (action == null) return false;

        _activeAction = action;
        _isInAction = true;
        _actionStartTime = Time.time;

        _context.Animation.PlayTargetAnimation(action.StateHash, action.crossFade, action.animatorLayer);

        return true;
    }

    // Called by PlayerAnimation.OnPlayableActionComplete() via Animation Event.
    // Fires the action effect and clears the active state.
    public void NotifyComplete()
    {
        if (!_isInAction) return;

        var completed = _activeAction;
        ClearAction();
        completed.OnCompleted(_context);
    }

    // Attempts to interrupt the current action with the given source.
    public bool TryInterrupt(PlayableInterruptFlags source)
    {
        if (!_isInAction) return true;

        if (!_activeAction.CanInterruptWith(source)) return false;

        ForceInterrupt();
        return true;
    }

    // Unconditional cancel — used when the player dies, teleports, etc.
    public void ForceInterrupt()
    {
        if (!_isInAction) return;

        _activeAction.OnInterrupted(_context);
        ClearAction();
    }

    public void EnterDialogue()
    {
        _isInDialogue = true;
        _context.Animation.Animator.SetBool(PlayerAnimHashes.IsInDialogue, true);
    }

    public void ExitDialogue()
    {
        _isInDialogue = false;
        _context.Animation.Animator.SetBool(PlayerAnimHashes.IsInDialogue, false);
    }

    #endregion

    #region Unity Callbacks

    private void Update()
    {
        if (!_isInAction) return;
        if (Time.time - _actionStartTime > _actionTimeout)
        {
            Debug.LogWarning($"[PlayableController] Action '{_activeAction?.name}' superó el timeout de {_actionTimeout}s — forzando interrupción para evitar inputs bloqueados.");
            ForceInterrupt();
        }
    }

    private void OnDisable()
    {
        if (_isInAction) ForceInterrupt();
        if (_isInDialogue) ExitDialogue();
    }

    #endregion

    #region Helpers

    private void ClearAction()
    {
        _activeAction = null;
        _isInAction = false;
    }

    #endregion
}