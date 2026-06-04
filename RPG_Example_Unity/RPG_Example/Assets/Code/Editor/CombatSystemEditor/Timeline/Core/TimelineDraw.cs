using UnityEditor;
using UnityEngine;

internal static class TimelineDraw
{
    // Pure drawing helpers for a timeline UI.

    public static void DrawBackground(Rect totalRect)
    {
        EditorStylesCache.EnsureInit();
        EditorGUI.DrawRect(totalRect, EditorStylesCache.TimelineBg);
    }

    public static void DrawPanels(TimelineLayout layout)
    {
        EditorStylesCache.EnsureInit();

        EditorGUI.DrawRect(layout.HeaderRect, EditorStylesCache.HeaderBg);
        EditorGUI.DrawRect(layout.TimelineRect, EditorStylesCache.TimelinePanel);
        EditorGUI.DrawRect(layout.RulerRect, EditorStylesCache.RulerBg);

        // Vertical separator between header and timeline
        var sep = new Rect(layout.HeaderRect.xMax - 1f, layout.TotalRect.y, 1f, layout.TotalRect.height);
        EditorGUI.DrawRect(sep, EditorStylesCache.TrackSeparator);
    }

    public static void DrawGrid(TimelineLayout layout, int minorDivisions = 20, int majorDivisions = 5)
    {
        EditorStylesCache.EnsureInit();

        Rect r = layout.TracksRect;
        if (r.width <= 1f || r.height <= 1f) return;

        Handles.BeginGUI();

        // Minor vertical lines
        int minor = Mathf.Max(2, minorDivisions);
        for (int i = 0; i <= minor; i++)
        {
            float t = i / (float)minor;
            float x = Mathf.Lerp(layout.ContentXMin, layout.ContentXMax, t);
            Handles.color = EditorStylesCache.GridMinor;
            Handles.DrawLine(new Vector3(x, r.y), new Vector3(x, r.yMax));
        }

        // Major vertical lines
        int major = Mathf.Max(2, majorDivisions);
        for (int i = 0; i <= major; i++)
        {
            float t = i / (float)major;
            float x = Mathf.Lerp(layout.ContentXMin, layout.ContentXMax, t);
            Handles.color = EditorStylesCache.GridMajor;
            Handles.DrawLine(new Vector3(x, r.y), new Vector3(x, r.yMax));
        }

        Handles.EndGUI();
    }

    public static void DrawRuler(TimelineLayout layout, int ticks = 10)
    {
        EditorStylesCache.EnsureInit();

        Rect r = layout.RulerRect;
        if (r.width <= 1f || r.height <= 1f) return;

        int n = Mathf.Max(2, ticks);

        Handles.BeginGUI();

        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            float x = Mathf.Lerp(layout.ContentXMin, layout.ContentXMax, t);

            float tickH = (i % 5 == 0) ? r.height * 0.85f : r.height * 0.55f;
            Handles.color = EditorStylesCache.GridMajor;
            Handles.DrawLine(new Vector3(x, r.yMax - tickH), new Vector3(x, r.yMax));

            if (i % 5 != 0) continue;

            var label = Mathf.RoundToInt(t * 100f) + "%";

            var size = EditorStylesCache.RulerLabel.CalcSize(new GUIContent(label));

            var lr = new Rect(x - size.x * 0.5f, r.y + 2f, size.x, size.y);

            GUI.Label(lr, label, EditorStylesCache.RulerLabel);
        }

        Handles.EndGUI();
    }

    public static void DrawTrackSeparators(TimelineLayout layout)
    {
        EditorStylesCache.EnsureInit();

        if (layout.TrackCount <= 0) return;

        Handles.BeginGUI();
        Handles.color = EditorStylesCache.TrackSeparator;

        for (int i = 0; i < layout.TrackCount; i++)
        {
            Rect tr = layout.GetTrackRect(i);
            float y = tr.yMax + layout.TrackSpacing * 0.5f;
            Handles.DrawLine(new Vector3(layout.TimelineRect.x, y), new Vector3(layout.TimelineRect.xMax, y));
        }

        Handles.EndGUI();
    }

    public static void DrawScrubber(TimelineLayout layout, float t01)
    {
        EditorStylesCache.EnsureInit();

        float x = layout.Time01ToX(t01);
        Rect r = layout.TimelineRect;

        Handles.BeginGUI();
        Handles.color = EditorStylesCache.Scrubber;
        Handles.DrawLine(new Vector3(x, r.y), new Vector3(x, r.yMax));
        Handles.EndGUI();
    }

    public static void DrawTrackHeaderLabel(TimelineLayout layout, int trackIndex, string text)
    {
        EditorStylesCache.EnsureInit();

        Rect hr = layout.GetTrackHeaderRect(trackIndex);
        hr.x += 6f;
        hr.width -= 10f;
        GUI.Label(hr, text ?? string.Empty, EditorStylesCache.TrackHeaderLabel);
    }
    
    public static void DrawTrackHeaderWithAdd(TimelineLayout layout, int trackIndex, string label, bool enabled, System.Action onAdd)
    {
        // Start from the same rect TimelineDraw uses internally for the header label.
        Rect hr = layout.GetTrackHeaderRect(trackIndex);
        hr.x += 6f;
        hr.width -= 10f;

        // Label on the left
        var labelRect = hr;
        labelRect.width -= 28f;
        GUI.Label(labelRect, label ?? string.Empty, EditorStylesCache.TrackHeaderLabel);

        // + button on the right
        if (!enabled || onAdd == null) return;

        var btnRect = new Rect(hr.xMax - 24f, hr.y + 2f, 22f, hr.height - 4f);
        if (GUI.Button(btnRect, "+"))
            onAdd.Invoke();
    }

    public static void DrawBlock(Rect blockRect, string label, Color fill, Color border, bool selected)
    {
        // Generic timeline block drawing (rect + border + optional selected highlight).
        EditorGUI.DrawRect(blockRect, fill);

        DrawRectBorder(blockRect, border);

        if (selected)
        {
            DrawRectBorder(
                new Rect(blockRect.x - 1f, blockRect.y - 1f, blockRect.width + 2f, blockRect.height + 2f),
                new Color(1f, 1f, 1f, 0.35f));
        }

        // Draw resize handles (visual feedback).
        const float HANDLE_W_PX = 10f; // Must match the value used when creating TimelineBlockVisual.
        var lh = new Rect(blockRect.x, blockRect.y, Mathf.Min(HANDLE_W_PX, blockRect.width), blockRect.height);
        var rh = new Rect(blockRect.xMax - Mathf.Min(HANDLE_W_PX, blockRect.width), blockRect.y,
            Mathf.Min(HANDLE_W_PX, blockRect.width), blockRect.height);

        EditorGUI.DrawRect(lh, new Color(0f, 0f, 0f, 0.18f));
        EditorGUI.DrawRect(rh, new Color(0f, 0f, 0f, 0.18f));

        if (!string.IsNullOrEmpty(label))
        {
            var lr = new Rect(blockRect.x + 4f, blockRect.y + 2f, blockRect.width - 8f, blockRect.height - 4f);
            GUI.Label(lr, label, EditorStylesCache.TinyCenteredLabel);
        }
    }

    public static void DrawRectBorder(Rect r, Color color, float thickness = 1f)
    {
        // Top
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, thickness), color);
        // Bottom
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), color);
        // Left
        EditorGUI.DrawRect(new Rect(r.x, r.y, thickness, r.height), color);
        // Right
        EditorGUI.DrawRect(new Rect(r.xMax - thickness, r.y, thickness, r.height), color);
    }
}
