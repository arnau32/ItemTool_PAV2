using UnityEngine;

[CreateAssetMenu(fileName = "AnalyticsConfig", menuName = "Analytics/Config")]
public class AnalyticsConfig : ScriptableObject
{
    private const string RESOURCE_PATH = "AnalyticsConfig";

    [Tooltip("Manual tag appended to Application.version. Change this before every playtest. " +
             "Example: b0.3-combat-rework   →   full build id: 0.1.0_b0.3-combat-rework")]
    public string buildLabel = "";

    public string FullBuildVersion
    {
        get
        {
            string version = Application.version;
            string label   = buildLabel != null ? buildLabel.Trim() : "";
            return string.IsNullOrEmpty(label) ? version : $"{version}_{label}";
        }
    }

    private static AnalyticsConfig _cached;

    public static AnalyticsConfig Load()
    {
        if (_cached != null) return _cached;
        _cached = Resources.Load<AnalyticsConfig>(RESOURCE_PATH);
        return _cached;
    }
}