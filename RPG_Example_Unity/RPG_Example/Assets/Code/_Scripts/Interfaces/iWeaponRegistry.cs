using Gameplay.Items;
using UnityEngine;

public interface IWeaponRegistry : IVfxUser
{
    GameObject SlashSpawnerPoint { get; }

    void RegisterWeapon(WeaponInstance weapon);
    void ClearWeapons();
    
    void ClearEquipedWeaponToNew(); // NOTE: Kept with the original typo to avoid breaking existing calls.
    void SetActiveVfxRig(VFXRigContainer rig);

    void NotifyWeaponDataEquipped(WeaponData weaponData);
}