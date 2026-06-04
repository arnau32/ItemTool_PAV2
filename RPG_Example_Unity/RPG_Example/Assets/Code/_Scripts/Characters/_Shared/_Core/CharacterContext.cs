using UnityEngine;

public class CharacterContext
{
    public GameObject Owner { get; }
    public Transform Transform { get; }
    public CharacterStats Stats { get; }
    public CharacterHealthSystem Health { get; }

    public CharacterContext(GameObject owner, CharacterStats stats, CharacterHealthSystem health)
    {
        Owner = owner;
        Transform = Owner.transform;
        Stats = stats;
        Health = health;
    }

}
