using System;

public interface ICombatContext
{
    CharacterAnimation Animation { get; }
    IStamina Stamina { get; }
    IWeaponUser Weapons { get; }
}
