using UnityEngine;
using FMODUnity;

public abstract class CharacterSounds : FMODEventLibrary
{
    [field: Header("Locomotion")]
    [field: SerializeField] public EventReference Footstep  { get; private set; }

    [field: Header("Hurt — Random")]
    [field: SerializeField] public EventReference Hurt  { get; private set; }

    [field: Header("Death — Random")]
    [field: SerializeField] public EventReference Death  { get; private set; }

    [field: Header("Hit Reaction")]
    // TODO The sound of receiving a heavy impact (knockback, knockdown).
    // Distinct from Hurt: Hurt = voice grunt, HitReaction = body impact SFX.
    [field: SerializeField] public EventReference HitReaction { get; private set; }
}
