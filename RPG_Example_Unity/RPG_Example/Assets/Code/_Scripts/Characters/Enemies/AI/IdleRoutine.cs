using System;
using UnityEngine;

[Serializable]
public struct IdleRoutine
{
    public enum RoutineType { Patrol, Stand, LookAround, InteractPoint }

    public RoutineType type;

    [Range(0f, 1f)]
    public float weight;

    [Tooltip("Minimum time in seconds spent executing this routine.")]
    [Min(0.5f)] public float minDuration;

    [Tooltip("Maximum time in seconds spent executing this routine.")]
    [Min(0.5f)] public float maxDuration;
}