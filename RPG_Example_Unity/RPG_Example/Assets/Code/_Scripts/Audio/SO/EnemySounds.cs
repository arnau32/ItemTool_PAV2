using FMODUnity;
using UnityEngine;

[CreateAssetMenu(menuName = "FMODAudio/Enemy Sounds", fileName = "EnemySounds")]
public class EnemySounds : CharacterSounds
{
    [field: Header("Behaviour")]
    [field: SerializeField] public EventReference Idle    { get; private set; }
    [field: SerializeField] public EventReference Alert   { get; private set; }
}