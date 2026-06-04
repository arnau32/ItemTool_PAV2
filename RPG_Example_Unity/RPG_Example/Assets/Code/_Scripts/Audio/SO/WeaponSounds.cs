using FMODUnity;
using UnityEngine;

[CreateAssetMenu(menuName = "FMODAudio/Weapon Sounds", fileName = "WeaponSounds")]
public class WeaponSounds : FMODEventLibrary
{
    [field: Header("Impact")]
    [field: SerializeField]
    public EventReference HitFlesh { get; private set; }

    [field: SerializeField] public EventReference HitBlock { get; private set; }

    [field: Header("Equip")]
    [field: SerializeField]
    public EventReference Equip { get; private set; }

    [field: SerializeField] public EventReference Unequip { get; private set; }

    [field: Header("Ability")]
    [field: SerializeField]
    public EventReference AbilitySound { get; private set; }
}