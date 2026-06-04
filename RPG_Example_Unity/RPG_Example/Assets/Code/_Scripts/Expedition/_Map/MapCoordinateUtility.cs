using UnityEngine;

public static class MapCoordinateUtility
{
    public static Vector2 WorldToMapUV(Vector3 worldPosition, Map map)
    {
        float u = Mathf.InverseLerp(map.worldMin.x, map.worldMax.x, worldPosition.x);
        float v = Mathf.InverseLerp(map.worldMin.y, map.worldMax.y, worldPosition.z);

        return new Vector2(u, v);
    }

    public static Vector2 WorldToRectPosition(Vector3 worldPosition, Map map, RectTransform rect)
    {
        Vector2 uv = WorldToMapUV(worldPosition, map);

        Rect r = rect.rect;

        return new Vector2(
            Mathf.Lerp(r.xMin, r.xMax, uv.x),
            Mathf.Lerp(r.yMin, r.yMax, uv.y)
        );
    }
}