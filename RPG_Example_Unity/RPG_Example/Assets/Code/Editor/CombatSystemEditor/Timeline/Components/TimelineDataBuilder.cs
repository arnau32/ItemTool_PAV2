using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Builds visual data structures for timeline rendering from SerializedProperties.
/// Handles row assignment (stacking) to avoid overlapping blocks.
internal sealed class TimelineDataBuilder
{
    #region Data Structures

    public struct WindowRef
    {
        public string label;
        public string fieldName;
        public int index;
        public SerializedProperty startProp;
        public SerializedProperty endProp;
        public Color color;

        // stacking
        public int row;
        public float start01;
        public float end01;
    }

    #endregion

    #region Public API - Build Methods

    /// Builds window data from damage list (special case with nested window struct).
    public List<WindowRef> BuildFromDamageList(SerializedProperty damageListProp, string fieldName, Color color)
    {
        int n = (damageListProp != null && damageListProp.isArray) ? damageListProp.arraySize : 0;
        if (n <= 0) return null;

        var list = new List<WindowRef>(n);

        for (int i = 0; i < n; i++)
        {
            var elem = damageListProp.GetArrayElementAtIndex(i);
            var win  = elem.FindPropertyRelative("window");
            if (win == null) continue;

            var start = win.FindPropertyRelative("start");
            var end   = win.FindPropertyRelative("end");

            float s = Mathf.Clamp01(start.floatValue);
            float e = Mathf.Clamp01(end.floatValue);
            if (e < s) e = s;

            list.Add(new WindowRef
            {
                label     = $"Damage {i}",
                fieldName = fieldName,
                index     = i,
                startProp = start,
                endProp   = end,
                color     = color,
                start01   = s,
                end01     = e,
                row       = 0
            });
        }

        return list;
    }

    /// Builds window data from vfx list (special case with nested window struct and vfxId).
    public List<WindowRef> BuildFromVfxList(SerializedProperty vfxListProp, string fieldName, Color color)
    {
        int n = (vfxListProp != null && vfxListProp.isArray) ? vfxListProp.arraySize : 0;
        if (n <= 0) return null;

        var list = new List<WindowRef>(n);

        for (int i = 0; i < n; i++)
        {
            var elem = vfxListProp.GetArrayElementAtIndex(i);
            if (elem == null) continue;

            var vfxId = elem.FindPropertyRelative("vfxId");
            int id    = vfxId != null ? vfxId.intValue : 0;

            var win = elem.FindPropertyRelative("window");
            if (win == null) continue;

            var start = win.FindPropertyRelative("start");
            var end   = win.FindPropertyRelative("end");
            if (start == null || end == null) continue;

            float s = Mathf.Clamp01(start.floatValue);
            float e = Mathf.Clamp01(end.floatValue);
            if (e < s) e = s;

            list.Add(new WindowRef
            {
                label     = $"VFX {id}",
                fieldName = fieldName,
                index     = i,
                startProp = start,
                endProp   = end,
                color     = color,
                start01   = s,
                end01     = e,
                row       = 0
            });
        }

        return list;
    }

    /// Builds timeline data from AudioTrigger list.
    /// AudioTrigger uses a single "triggerAt" float — rendered as a zero-width marker.
    /// Both startProp and endProp point to the same "triggerAt" property so the
    /// timeline block has no width, making it look like a vertical tick.
    public List<WindowRef> BuildFromAudioTriggerList(SerializedProperty listProp, string fieldName, Color color)
    {
        int n = (listProp != null && listProp.isArray) ? listProp.arraySize : 0;
        if (n <= 0) return null;

        var list = new List<WindowRef>(n);

        for (int i = 0; i < n; i++)
        {
            var elem = listProp.GetArrayElementAtIndex(i);
            if (elem == null) continue;

            // triggerAt is the sole timing field — no start/end separation.
            var triggerAt = elem.FindPropertyRelative("triggerAt");
            if (triggerAt == null) continue;

            float t = Mathf.Clamp01(triggerAt.floatValue);

            // Render as a very thin block (start == end) so it looks like a marker pin.
            list.Add(new WindowRef
            {
                label     = $"SFX {i}",
                fieldName = fieldName,
                index     = i,
                startProp = triggerAt,
                endProp   = triggerAt, // same prop — forces zero width on timeline
                color     = color,
                start01   = t,
                end01     = t,
                row       = 0
            });
        }

        return list;
    }

    /// Builds timeline data from AudioWindow list (nested WindowEvent + sound).
    public List<WindowRef> BuildFromAudioWindowList(SerializedProperty listProp, string fieldName, Color color)
    {
        int n = (listProp != null && listProp.isArray) ? listProp.arraySize : 0;
        if (n <= 0) return null;

        var list = new List<WindowRef>(n);

        for (int i = 0; i < n; i++)
        {
            var elem = listProp.GetArrayElementAtIndex(i);
            if (elem == null) continue;

            var win = elem.FindPropertyRelative("window");
            if (win == null) continue;

            var start = win.FindPropertyRelative("start");
            var end   = win.FindPropertyRelative("end");
            if (start == null || end == null) continue;

            float s = Mathf.Clamp01(start.floatValue);
            float e = Mathf.Clamp01(end.floatValue);
            if (e < s) e = s;

            list.Add(new WindowRef
            {
                label     = $"AudioWin {i}",
                fieldName = fieldName,
                index     = i,
                startProp = start,
                endProp   = end,
                color     = color,
                start01   = s,
                end01     = e,
                row       = 0
            });
        }

        return list;
    }

    /// Builds window data from a simple list property.
    public List<WindowRef> BuildFromSimpleList(SerializedProperty listProp, string fieldName, string labelPrefix, Color color)
    {
        int n = (listProp != null && listProp.isArray) ? listProp.arraySize : 0;
        if (n <= 0) return null;

        var list = new List<WindowRef>(n);

        for (int i = 0; i < n; i++)
        {
            var elem  = listProp.GetArrayElementAtIndex(i);
            var start = elem.FindPropertyRelative("start");
            var end   = elem.FindPropertyRelative("end");

            float s = Mathf.Clamp01(start.floatValue);
            float e = Mathf.Clamp01(end.floatValue);
            if (e < s) e = s;

            list.Add(new WindowRef
            {
                label     = $"{labelPrefix} {i}",
                fieldName = fieldName,
                index     = i,
                startProp = start,
                endProp   = end,
                color     = color,
                start01   = s,
                end01     = e,
                row       = 0
            });
        }

        return list;
    }

    /// Builds window data from a single window property (like combo window).
    public List<WindowRef> BuildFromSingleWindow(SerializedProperty windowProp, string fieldName, string label, Color color)
    {
        if (windowProp == null) return null;

        var start = windowProp.FindPropertyRelative("start");
        var end   = windowProp.FindPropertyRelative("end");

        float s = Mathf.Clamp01(start.floatValue);
        float e = Mathf.Clamp01(end.floatValue);
        if (e < s) e = s;

        return new List<WindowRef>
        {
            new WindowRef
            {
                label     = label,
                fieldName = fieldName,
                index     = 0,
                startProp = start,
                endProp   = end,
                color     = color,
                start01   = s,
                end01     = e,
                row       = 0
            }
        };
    }

    #endregion

    #region Public API - Row Assignment

    /// Assigns non-overlapping rows to a list of windows.
    /// Returns the total number of rows needed.
    public int AssignNonOverlappingRows(List<WindowRef> windows)
    {
        if (windows == null || windows.Count == 0) return 0;

        windows.Sort((a, b) => a.start01.CompareTo(b.start01));

        List<float> rowEnd = new List<float>();

        for (int i = 0; i < windows.Count; i++)
        {
            var w = windows[i];

            int row = -1;
            for (int r = 0; r < rowEnd.Count; r++)
            {
                if (w.start01 >= rowEnd[r])
                {
                    row = r;
                    break;
                }
            }

            if (row == -1)
            {
                row = rowEnd.Count;
                rowEnd.Add(w.end01);
            }
            else
            {
                rowEnd[row] = w.end01;
            }

            w.row      = row;
            windows[i] = w;
        }

        return rowEnd.Count;
    }

    #endregion
}
