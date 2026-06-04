using UnityEngine;
public interface IHasFaction
{
    Enums.Faction Faction { get; }
}

[DisallowMultipleComponent]
public class FactionComponent : MonoBehaviour, IHasFaction
{
    [SerializeField] private Enums.Faction _faction = Enums.Faction.Player;

    public Enums.Faction Faction => _faction;
}
