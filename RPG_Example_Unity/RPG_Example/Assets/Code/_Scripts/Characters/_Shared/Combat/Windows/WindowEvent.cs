using UnityEngine;

[System.Serializable]
public struct WindowEvent
{
    [Range(0f, 1f)] public float start;
    [Range(0f, 1f)] public float end;

    public bool IsActive(float tNorm) => tNorm >= start && tNorm <= end;
}
