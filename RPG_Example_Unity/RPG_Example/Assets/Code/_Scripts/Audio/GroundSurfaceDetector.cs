using UnityEngine;

// Detects ground surface type via downward raycast and exposes it as a
// normalized float for FMOD parameter driving.

public class GroundSurfaceDetector : MonoBehaviour
{
    [SerializeField] private float _raycastDistance = 1.2f;
    [SerializeField] private LayerMask _groundLayers = ~0;
    [SerializeField] private float _defaultTerrainValue = 0f;

    private float _currentTerrainValue;
    public float CurrentTerrainValue => _currentTerrainValue;

    // Mapped Tag → FMOD value
    private static readonly (string tag, float value)[] TagMap =
    {
        ("Terrain_Dirt",  0f),
        ("Terrain_Stone", 1f),  
        ("Terrain_Water", 2f),
        ("Terrain_Wood",  3f),
    };

    private void Awake()
    {
        _currentTerrainValue = _defaultTerrainValue;
    }

    public float DetectSurface()
    {
        if (!Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out var hit, _raycastDistance, _groundLayers))
        {
            return _currentTerrainValue;
        }

        string hitTag = hit.collider.tag;

        for (int i = 0; i < TagMap.Length; i++)
        {
            if (TagMap[i].tag != hitTag) continue;
            
            _currentTerrainValue = TagMap[i].value;
            return _currentTerrainValue;
        }

        _currentTerrainValue = _defaultTerrainValue;
        return _currentTerrainValue;
    }
}