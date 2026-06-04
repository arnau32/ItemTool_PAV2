using UnityEngine;

public struct AlertEvent
{
    public Vector3 Position;
    public float Radius;
    public float AlertStrength;
    public EnemyBase Source;
    public Enums.AlertLevel SourceAlertLevel;
}