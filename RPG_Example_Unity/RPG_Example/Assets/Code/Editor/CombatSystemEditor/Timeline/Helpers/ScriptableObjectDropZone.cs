using System;
using UnityEditor;
using UnityEngine;

/// Reusable UI component for drawing ScriptableObject selection and drag-drop zones.
internal static class ScriptableObjectDropZone
{
    #region Public API

    /// Draws a complete header with object field and drag-drop zone.
    /// Returns true if the object changed.
    public static bool DrawHeader<T>(string contextLabel, string objectFieldLabel, string dropZoneLabel, ref T current, Action<T> onChanged) where T : ScriptableObject
    {
        EditorGUILayout.LabelField(contextLabel, EditorStyles.boldLabel);

        bool changed = false;

        EditorGUI.BeginChangeCheck();
        var newObj = (T)EditorGUILayout.ObjectField(objectFieldLabel, current, typeof(T), false);
        if (EditorGUI.EndChangeCheck())
        {
            current = newObj;
            onChanged?.Invoke(current);
            changed = true;
        }

        EditorGUILayout.Space(4);

        var dropRect = GUILayoutUtility.GetRect(0, 36, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, dropZoneLabel, EditorStyles.helpBox);

        var e = Event.current;
        if (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)
        {
            if (dropRect.Contains(e.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is T validObj)
                        {
                            current = validObj;
                            onChanged?.Invoke(current);
                            changed = true;
                            break;
                        }
                    }
                }

                e.Use();
            }
        }

        return changed;
    }

    /// Draws just the drop zone (no object field).
    public static bool DrawDropZoneOnly<T>(string label, float height, Action<T> onDropped) where T : ScriptableObject
    {
        var dropRect = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, label, EditorStyles.helpBox);

        var e = Event.current;
        if (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)
        {
            if (dropRect.Contains(e.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is T validObj)
                        {
                            onDropped?.Invoke(validObj);
                            e.Use();
                            return true;
                        }
                    }
                }

                e.Use();
            }
        }

        return false;
    }

    #endregion
}
