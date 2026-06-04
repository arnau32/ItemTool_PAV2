using FMODUnity;
using UnityEngine;

// Global sounds that AudioManager owns directly — music and ambience.
[CreateAssetMenu(menuName = "Audio/Ambience & Music", fileName = "AmbienceMusicSounds")]
public class AmbienceMusicSounds : FMODEventLibrary
{
    [field: Header("Music")]
    [field: SerializeField] public EventReference Music       { get; private set; }
    [field: SerializeField] public EventReference CombatMusic { get; private set; }

    [field: Header("Ambience")]
    [field: SerializeField] public EventReference Ambience    { get; private set; }

    [field: Header("Ambient Layers")]
    [field: SerializeField] public EventReference[] AmbientLayers { get; private set; }
}