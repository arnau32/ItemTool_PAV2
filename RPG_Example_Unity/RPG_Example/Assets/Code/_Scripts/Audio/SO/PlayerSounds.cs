using FMODUnity;
using UnityEngine;

[CreateAssetMenu(menuName = "FMODAudio/Player Sounds", fileName = "PlayerSounds")]
public class PlayerSounds : CharacterSounds
{
    [field: Header("Dodge")]
    [field: SerializeField]
    public EventReference Dodge { get; private set; }

    [field: SerializeField] public EventReference Land { get; private set; }

    [field: Header("Parry")]
    [field: SerializeField]
    public EventReference ParryAttempt { get; private set; }
    [field: SerializeField] public EventReference ParrySuccess { get; private set; }

    [field: Header("Stamina")]
    [field: SerializeField]
    public EventReference StaminaExhausted { get; private set; }

    [field: Header("Interaction")]
    [field: SerializeField]
    public EventReference Heal { get; private set; }
    
    [field: Header("Voices")]
    [field: SerializeField] public EventReference DodgeVoice { get; private set; }
}
