using UnityEditor;
using UnityEngine;

internal static class EditorStylesCache
{
    // Cache GUIStyles to avoid allocations and to keep a consistent look.

    private static bool _initialized;

    private static GUIStyle _bigCenteredButton;
    private static GUIStyle _trackHeaderLabel;
    private static GUIStyle _rulerLabel;
    private static GUIStyle _tinyCenteredLabel;
    private static GUIStyle _box;

    public static void EnsureInit()
    {
        if (_initialized) return;
        _initialized = true;

        _bigCenteredButton = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            fixedHeight = 42f,
            fontStyle = FontStyle.Bold
        };

        _trackHeaderLabel = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 11
        };

        _rulerLabel = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = 10
        };

        _tinyCenteredLabel = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10
        };

        _box = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(6, 6, 6, 6)
        };
    }

    public static GUIStyle BigCenteredButton
    {
        get
        {
            EnsureInit();
            return _bigCenteredButton;
        }
    }

    public static GUIStyle TrackHeaderLabel
    {
        get
        {
            EnsureInit();
            return _trackHeaderLabel;
        }
    }

    public static GUIStyle RulerLabel
    {
        get
        {
            EnsureInit();
            return _rulerLabel;
        }
    }

    public static GUIStyle TinyCenteredLabel
    {
        get
        {
            EnsureInit();
            return _tinyCenteredLabel;
        }
    }

    public static GUIStyle Box
    {
        get
        {
            EnsureInit();
            return _box;
        }
    }

    // Common colors for timeline drawing. Keep them centralized for consistency.
    public static Color TimelineBg => new Color(0.12f, 0.12f, 0.12f, 1f);
    public static Color TimelinePanel => new Color(0.16f, 0.16f, 0.16f, 1f);
    public static Color GridMinor => new Color(1f, 1f, 1f, 0.05f);
    public static Color GridMajor => new Color(1f, 1f, 1f, 0.09f);
    public static Color RulerBg => new Color(0.10f, 0.10f, 0.10f, 1f);
    public static Color Scrubber => new Color(0.20f, 0.65f, 1f, 0.90f);
    public static Color TrackSeparator => new Color(0f, 0f, 0f, 0.25f);
    public static Color HeaderBg => new Color(0.13f, 0.13f, 0.13f, 1f);
}
