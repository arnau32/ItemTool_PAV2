using UnityEngine;
using UnityEngine.AI;

public static class DirectionalSpaceCheck
{
    private static readonly Collider[] _overlapBuffer = new Collider[16];

    private const string ENEMY_TAG = "Enemy";

    public static float GetSideFree01(Transform origin, float radius = 1.5f, int samples = 5)
    {
        float total = 0f;
        float step = radius / samples;
        Vector3 left = -origin.right;
        Vector3 basePos = origin.position;

        for (int i = 1; i <= samples; i++)
        {
            Vector3 p = basePos + left * (i * step);
            if (NavMesh.SamplePosition(p, out _, 0.5f, NavMesh.AllAreas))
                total += 1f;
        }

        return total / samples;
    }

    public static float GetRightFree01(Transform origin, float radius = 1.5f, int samples = 5)
    {
        float total = 0f;
        float step = radius / samples;
        Vector3 right = origin.right;
        Vector3 basePos = origin.position;

        for (int i = 1; i <= samples; i++)
        {
            Vector3 p = basePos + right * (i * step);
            if (NavMesh.SamplePosition(p, out _, 0.5f, NavMesh.AllAreas))
                total += 1f;
        }

        return total / samples;
    }

    /// 0..1 enemy pressure on the left side. Higher = more enemies there → prefer right.
    public static float GetEnemyPressureLeft01(Transform origin, float radius, LayerMask enemyLayer)
    {
        return GetEnemyPressure01(origin, -origin.right, radius, enemyLayer);
    }

    /// 0..1 enemy pressure on the right side. Higher = more enemies there → prefer left.
    public static float GetEnemyPressureRight01(Transform origin, float radius, LayerMask enemyLayer)
    {
        return GetEnemyPressure01(origin, origin.right, radius, enemyLayer);
    }

    private static float GetEnemyPressure01(Transform origin, Vector3 side, float radius, LayerMask enemyLayer)
    {
        Vector3 checkPos = origin.position + side * (radius * 0.6f);

        int count = Physics.OverlapSphereNonAlloc(
            checkPos, radius * 0.5f, _overlapBuffer, enemyLayer,
            QueryTriggerInteraction.Collide);

        int nearby = 0;
        for (int i = 0; i < count; i++)
        {
            var col = _overlapBuffer[i];
            if (col == null) continue;
            if (col.transform == origin) continue;
            if (!col.CompareTag(ENEMY_TAG)) continue;
            nearby++;
        }

        return Mathf.Clamp01(nearby / 2f);
    }
}