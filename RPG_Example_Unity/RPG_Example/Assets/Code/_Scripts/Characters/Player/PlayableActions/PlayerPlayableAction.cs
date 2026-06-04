using System;
using UnityEngine;

/// Bitmask of sources that can interrupt an in-progress playable action.
/// Each concrete SO defines which ones apply to it.
[Flags]
public enum PlayableInterruptFlags
{
    None  = 0,
    Hit   = 1 << 0,
    Dodge = 1 << 1,
}

/// Base data container for any animated player action (consume, chest open, etc.).
/// Subclass this SO to define the animation and the effect that fires on completion.
public abstract class PlayerPlayableAction : ScriptableObject
{
    [Header("Animation")]
    [Tooltip("Animator state name to cross-fade into.")]
    public string animationStateName;
    [Range(0f, 0.5f)]
    public float crossFade = 0.15f;

    [Header("Interrupts")]
    [Tooltip("Which sources are allowed to cancel this action mid-way.")]
    public PlayableInterruptFlags interruptFlags = PlayableInterruptFlags.Hit | PlayableInterruptFlags.Dodge;

    [Header("Movement")]
    [Tooltip("If true the player can move freely while this action plays.")]
    public bool allowMovement = false;

    [Header("Combat")]
    [Tooltip("If true the player cannot attack or parry while this action plays.")]
    public bool blockAttack = true;

    [Header("Animator Layer")]
    [Tooltip("Animator layer index to play this action on. Match the layer used in the Animator Controller.")]
    public int animatorLayer = PlayerAnimHashes.LayerOverride;

    private int _stateHash;
    private bool _hashCached;

    public int StateHash
    {
        get
        {
            if (_hashCached) return _stateHash;
            _stateHash  = Animator.StringToHash(animationStateName);
            _hashCached = true;
            return _stateHash;
        }
    }

    public bool CanInterruptWith(PlayableInterruptFlags source) => (interruptFlags & source) != 0;

    public abstract void OnCompleted(PlayerContext context);

    public virtual void OnInterrupted(PlayerContext context) { }

#if UNITY_EDITOR
    private void OnValidate() => _hashCached = false;
#endif
}