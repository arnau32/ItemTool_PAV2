using UnityEngine;

public class Point : MonoBehaviour
{
    [Header("Point Settings")]
    [Tooltip("Peso relativo al elegir este punto.")]
    [Min(0f)]
    public float selectionWeight = 1f;

    protected virtual void OnValidate()
    {
        if (selectionWeight < 0f)
            selectionWeight = 0f;
    }

    protected virtual void DrawBaseGizmos(Color color, float wireRadius, float innerRadius = 0.22f)
    {
        Gizmos.color = color;
        Gizmos.DrawSphere(transform.position, innerRadius);
        Gizmos.DrawWireSphere(transform.position, wireRadius);
    }
}
