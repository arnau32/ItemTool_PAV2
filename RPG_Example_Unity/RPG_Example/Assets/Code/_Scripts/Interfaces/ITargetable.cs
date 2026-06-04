using UnityEngine;

public interface ITargetable
{
    Transform Transform { get; }
    public Enums.Faction Faction { get; }
    bool IsAlive { get; }
    bool IsNoisy { get; }

    float CurrentHealth01 { get; }
    float TargetWindUp01 { get; }
    float TargetRecovery01 { get; }
    Vector3 Velocity { get; }
}