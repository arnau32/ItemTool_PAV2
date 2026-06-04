using UnityEngine;

[CreateAssetMenu(fileName = "Map", menuName = "Between Shadows/Map/Map")]
public class Map : ScriptableObject
{
    [Header("Visual")]
    public Sprite mapSprite;

    [Header("World Bounds")]
    public Vector2 worldMin;
    public Vector2 worldMax;

    [Header("Fog")]
    public int maskResolution = 1024;
    public float revealRadiusWorld = 6f;
    public float revealSoftness = 0.25f;

    [Header("Optimization")]
    public float revealUpdateDistance = 1.5f;
}