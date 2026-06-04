using UnityEngine;

public class CharacterContextBuilder <TBuilder> where TBuilder : CharacterContextBuilder<TBuilder>
{
    public GameObject Owner { get; }
    public CharacterStats Stats { get; }
    public CharacterHealthSystem Health { get; }
    
    public FactionComponent FactionComponent { get; protected set; }
    
    protected CharacterContextBuilder(GameObject owner, CharacterStats stats, CharacterHealthSystem health)
    {
        Owner = owner;
        Stats = stats;
        Health = health;
    }
    
    public TBuilder WithFaction(FactionComponent faction)
    {
        FactionComponent = faction;
        return (TBuilder)this;
    }
}
