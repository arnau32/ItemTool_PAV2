using System;
using UnityEditor;
using UnityEngine;

/// Reusable component for drawing timeline window lists with selection, add, delete controls.
internal sealed class WindowListInspector
{
    #region Public API - Damage List (Special Case)

    /// Draws the damage windows list (has weaponHand and slot fields).
    public void DrawDamageList(SerializedProperty damageListProp, string fieldName, SelectionModel<SelectionKey> selection,
        Action onAdd, Action onClear, Action<bool, int> onSelectItem, Action<int> onDeleteItem, Action<float> onScrubToEnd)
    {
        DrawListHeader("Damage Windows", onAdd, onClear);

        if (damageListProp == null || !damageListProp.isArray || damageListProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No hay Damage Windows.", MessageType.Info);
            return;
        }

        for (int i = 0; i < damageListProp.arraySize; i++)
        {
            var elem = damageListProp.GetArrayElementAtIndex(i);
            if (elem == null) continue;

            var win = elem.FindPropertyRelative("window");
            if (!TryGetStartEnd(win, out float s, out float e)) continue;

            var hand = elem.FindPropertyRelative("weaponHand");
            var slot = elem.FindPropertyRelative("slot");

            string hs = hand != null && hand.propertyType == SerializedPropertyType.Enum
                ? hand.enumDisplayNames[hand.enumValueIndex]
                : "?";

            string sl = slot != null ? slot.intValue.ToString() : "?";

            bool selected = selection.Contains(new SelectionKey { fieldName = fieldName, index = i });
            string label  = $"Damage {i} | {hs} | Slot {sl} | [{s:0.00}-{e:0.00}]";

            if (DrawSelectableRow(selected, label, () =>
                {
                    bool shift = Event.current.shift;
                    onSelectItem?.Invoke(shift, i);
                    onScrubToEnd?.Invoke(e);
                },
                onDeleteItem == null ? (Action)null : () => onDeleteItem(i)))
            {
                break;
            }
        }
    }

    #endregion

    #region Public API - VFX List (Special Case)

    /// Draws the vfx windows list (has vfxId + nested window).
    public void DrawVfxList(SerializedProperty vfxListProp, string fieldName, SelectionModel<SelectionKey> selection, Action onAdd,
        Action onClear, Action<bool, int> onSelectItem, Action<int> onDeleteItem, Action<float> onScrubToEnd)
    {
        DrawListHeader("VFX Windows", onAdd, onClear);

        if (vfxListProp == null || !vfxListProp.isArray || vfxListProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No hay VFX Windows.", MessageType.Info);
            return;
        }

        for (int i = 0; i < vfxListProp.arraySize; i++)
        {
            var elem = vfxListProp.GetArrayElementAtIndex(i);
            if (elem == null) continue;

            var vfxId = elem.FindPropertyRelative("vfxId");
            int id    = vfxId != null ? vfxId.intValue : 0;

            var disableOnEnd = elem.FindPropertyRelative("disableOnEnd");
            var hand         = elem.FindPropertyRelative("hand");

            string handStr = hand != null && hand.propertyType == SerializedPropertyType.Enum
                ? hand.enumDisplayNames[hand.enumValueIndex]
                : "?";

            bool disable = disableOnEnd != null && disableOnEnd.boolValue;

            var win = elem.FindPropertyRelative("window");
            if (!TryGetStartEnd(win, out float s, out float e)) continue;

            bool selected  = selection.Contains(new SelectionKey { fieldName = fieldName, index = i });
            string disStr  = disable ? "[Auto-Stop]" : "[Loop]";
            string label   = $"VFX {i} | {handStr} | Id {id} {disStr} | [{s:0.00}-{e:0.00}]";

            if (DrawSelectableRow(selected, label, () =>
                {
                    bool shift = Event.current.shift;
                    onSelectItem?.Invoke(shift, i);
                    onScrubToEnd?.Invoke(e);
                },
                onDeleteItem == null ? (Action)null : () => onDeleteItem(i)))
            {
                break;
            }
        }
    }

    #endregion

    #region Public API - Audio Trigger List

    /// Draws the AudioTrigger list — shows triggerAt time and the FMOD event path.
    public void DrawAudioTriggerList(SerializedProperty listProp, string fieldName, SelectionModel<SelectionKey> selection,
        Action onAdd, Action onClear, Action<bool, int> onSelectItem, Action<int> onDeleteItem, Action<float> onScrubToTime)
    {
        DrawListHeader("Audio Triggers", onAdd, onClear);

        if (listProp == null || !listProp.isArray || listProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No hay Audio Triggers.", MessageType.Info);
            return;
        }

        for (int i = 0; i < listProp.arraySize; i++)
        {
            var elem = listProp.GetArrayElementAtIndex(i);
            if (elem == null) continue;

            var triggerAt = elem.FindPropertyRelative("triggerAt");
            float t       = triggerAt != null ? Mathf.Clamp01(triggerAt.floatValue) : 0f;

            // Show the FMOD event path to help designers identify the sound.
            var soundProp = elem.FindPropertyRelative("sound");
            string soundName = GetFmodEventShortName(soundProp);

            bool selected = selection.Contains(new SelectionKey { fieldName = fieldName, index = i });
            string label  = $"SFX {i} | @{t:0.00} | {soundName}";

            if (DrawSelectableRow(selected, label, () =>
                {
                    bool shift = Event.current.shift;
                    onSelectItem?.Invoke(shift, i);
                    onScrubToTime?.Invoke(t);
                },
                onDeleteItem == null ? (Action)null : () => onDeleteItem(i)))
            {
                break;
            }
        }
    }

    #endregion

    #region Public API - Audio Window List

    /// Draws the AudioWindow list — shows start/end times and the FMOD event path.
    public void DrawAudioWindowList(SerializedProperty listProp, string fieldName, SelectionModel<SelectionKey> selection,
        Action onAdd, Action onClear, Action<bool, int> onSelectItem, Action<int> onDeleteItem, Action<float> onScrubToEnd)
    {
        DrawListHeader("Audio Windows", onAdd, onClear);

        if (listProp == null || !listProp.isArray || listProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No hay Audio Windows.", MessageType.Info);
            return;
        }

        for (int i = 0; i < listProp.arraySize; i++)
        {
            var elem = listProp.GetArrayElementAtIndex(i);
            if (elem == null) continue;

            var win = elem.FindPropertyRelative("window");
            if (!TryGetStartEnd(win, out float s, out float e)) continue;

            var soundProp   = elem.FindPropertyRelative("sound");
            var stopImm     = elem.FindPropertyRelative("stopImmediate");
            string soundName = GetFmodEventShortName(soundProp);
            string stopStr   = (stopImm != null && stopImm.boolValue) ? "[Immediate]" : "[Fadeout]";

            bool selected = selection.Contains(new SelectionKey { fieldName = fieldName, index = i });
            string label  = $"AudioWin {i} | {soundName} {stopStr} | [{s:0.00}-{e:0.00}]";

            if (DrawSelectableRow(selected, label, () =>
                {
                    bool shift = Event.current.shift;
                    onSelectItem?.Invoke(shift, i);
                    onScrubToEnd?.Invoke(e);
                },
                onDeleteItem == null ? (Action)null : () => onDeleteItem(i)))
            {
                break;
            }
        }
    }

    #endregion

    #region Public API - Simple Window List

    /// Draws a simple window list (only start/end, no extra fields).
    public void DrawSimpleWindowList(string title, string fieldName, SerializedProperty allowBoolProp, SerializedProperty listProp, SelectionModel<SelectionKey> selection,
        Action onAdd, Action onClear, Action<bool, int> onSelectItem, Action<int> onDeleteItem, Action<float> onScrubToEnd)
    {
        if (allowBoolProp != null)
        {
            EditorGUILayout.PropertyField(allowBoolProp);
            if (!allowBoolProp.boolValue) return;
        }

        DrawListHeader(title, onAdd, onClear);

        if (listProp == null || !listProp.isArray || listProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox($"No hay {title}.", MessageType.Info);
            return;
        }

        for (int i = 0; i < listProp.arraySize; i++)
        {
            var elem = listProp.GetArrayElementAtIndex(i);
            if (!TryGetStartEnd(elem, out float s, out float e)) continue;

            bool selected = selection.Contains(new SelectionKey { fieldName = fieldName, index = i });
            string label  = $"{title} {i} | [{s:0.00}-{e:0.00}]";

            if (DrawSelectableRow(selected, label, () =>
                {
                    bool shift = Event.current.shift;
                    onSelectItem?.Invoke(shift, i);
                    onScrubToEnd?.Invoke(e);
                },
                onDeleteItem == null ? (Action)null : () => onDeleteItem(i)))
            {
                break;
            }
        }
    }

    #endregion

    #region Public API - Single Window (Combo)

    /// Draws a single window (like combo window).
    public void DrawSingleWindow(string title, string fieldName, int index, SerializedProperty windowProp, SelectionModel<SelectionKey> selection,
        Action onDisable, Action<bool, int> onSelectItem, Action<float> onScrubToEnd)
    {
        if (!TryGetStartEnd(windowProp, out float s, out float e)) return;

        bool selected = selection.Contains(new SelectionKey { fieldName = fieldName, index = index });

        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            if (GUILayout.Button(selected ? "●" : "○", GUILayout.Width(24)))
            {
                bool shift = Event.current.shift;
                onSelectItem?.Invoke(shift, index);
                onScrubToEnd?.Invoke(e);
            }

            GUILayout.Label($"{title} | [{s:0.00}-{e:0.00}]", GUILayout.ExpandWidth(true));

            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
            if (GUILayout.Button("Disable", GUILayout.Width(70)))
            {
                onDisable?.Invoke();
            }
            GUI.backgroundColor = Color.white;
        }
    }

    #endregion

    #region Public API - Single Event Window (DodgeData)

    /// Draws a single event window with inline editable sliders.
    public void DrawSingleEventWindow(string title, string fieldName, int index, SerializedProperty windowProp, SelectionModel<SelectionKey> selection,
        Action<float, float> onRangeChanged, Action<bool, int> onSelectItem, Action<float> onScrubToEnd)
    {
        if (!TryGetStartEnd(windowProp, out float s, out float e)) return;

        bool selected = selection.Contains(new SelectionKey { fieldName = fieldName, index = index });

        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            if (GUILayout.Button(selected ? "●" : "○", GUILayout.Width(24)))
            {
                bool shift = Event.current.shift;
                onSelectItem?.Invoke(shift, index);
                onScrubToEnd?.Invoke(e);
            }

            GUILayout.Label($"{title} | [{s:0.00}-{e:0.00}]", GUILayout.ExpandWidth(true));
        }

        EditorGUI.BeginChangeCheck();
        float ns = EditorGUILayout.Slider("Start", s, 0f, 1f);
        float ne = EditorGUILayout.Slider("End",   e, 0f, 1f);
        if (ne < ns) ne = ns;

        if (EditorGUI.EndChangeCheck())
        {
            onRangeChanged?.Invoke(ns, ne);
            onScrubToEnd?.Invoke(ne);
        }

        EditorGUILayout.Space(4);
    }

    #endregion

    #region Internal Helpers

    private static void DrawListHeader(string title, Action onAdd, Action onClear)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+", GUILayout.Width(28)))
                onAdd?.Invoke();

            if (GUILayout.Button("Clear", GUILayout.Width(60)))
                onClear?.Invoke();
        }
    }

    private static bool DrawSelectableRow(bool selected, string label, Action onSelect, Action onDelete)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            if (GUILayout.Button(selected ? "●" : "○", GUILayout.Width(24)))
                onSelect?.Invoke();

            GUILayout.Label(label, GUILayout.ExpandWidth(true));

            if (onDelete != null && GUILayout.Button("X", GUILayout.Width(24)))
            {
                onDelete.Invoke();
                return true; // caller can break the loop safely
            }
        }

        return false;
    }

    private static bool TryGetStartEnd(SerializedProperty windowOrElementProp, out float s, out float e)
    {
        s = 0f;
        e = 0f;

        if (windowOrElementProp == null) return false;

        var start = windowOrElementProp.FindPropertyRelative("start");
        var end   = windowOrElementProp.FindPropertyRelative("end");
        if (start == null || end == null) return false;

        s = Mathf.Clamp01(start.floatValue);
        e = Mathf.Clamp01(end.floatValue);
        if (e < s) e = s;

        return true;
    }

    // Extracts the last segment of an FMOD event path for compact display.
    // e.g. "event:/Combat/Weapons/SwordSwing" → "SwordSwing"
    // Returns "—" if the event reference is empty or null.
    private static string GetFmodEventShortName(SerializedProperty soundProp)
    {
        if (soundProp == null) return "—";

        // EventReference serializes its path inside a child property called "Path".
        var pathProp = soundProp.FindPropertyRelative("Path");
        if (pathProp == null || string.IsNullOrEmpty(pathProp.stringValue)) return "—";

        string path = pathProp.stringValue;
        int lastSlash = path.LastIndexOf('/');
        return lastSlash >= 0 && lastSlash < path.Length - 1
            ? path.Substring(lastSlash + 1)
            : path;
    }

    #endregion
}
