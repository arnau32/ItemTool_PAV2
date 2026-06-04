using UnityEngine;
using UnityEngine.Localization;

public enum MapPOIType
{
    Zone,
    Quest,
    NPC,
}

public enum MapPOIPopupMode
{
    Never,
    Once,
    EveryEnter
}

[CreateAssetMenu(fileName = "Map POI Data", menuName = "POIs/POI Data", order = 0)]
[System.Serializable]
public class MapPOIData: ScriptableObject
{
    public string id;
    public MapPOIType type;

    [Header("Position")]
    public Vector3 worldTransform;
    public float _allscale = 1;
    public float _iconscale = 0.7f;

    [Header("Visual")]
    public Sprite icon;
    public Sprite border;
    public Sprite background;
    public Color background_color;
    public Color icon_color;
    
    
    public bool showIconFromStart = false;

    [Header("Localization")]
    public LocalizedString localizedName;

    [Header("Popup")]
    public MapPOIPopupMode popupMode = MapPOIPopupMode.Once;

    public Vector3 GetWorldPosition()
    {
        return worldTransform != null ? worldTransform : Vector3.zero;
    }
}