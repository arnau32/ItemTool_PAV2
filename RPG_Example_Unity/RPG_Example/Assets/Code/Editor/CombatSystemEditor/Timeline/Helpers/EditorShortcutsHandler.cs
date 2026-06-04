using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Handles common keyboard shortcuts for timeline editors (Space, S, Delete, F).
internal sealed class EditorShortcutsHandler
{
    #region Public API

    /// Handles keyboard shortcuts. Returns true if the event was consumed.
  public bool HandleEvent(Event e, EditorWindowPreviewController preview, AnimationClip clip, SelectionModel<SelectionKey> selection, 
        Action<float> onScrubToTime, Action onDeleteSelection, Func<SerializedObject> getSerializedObject, WindowReflectionRegistry registry)
    {
        if (e == null) return false;
        if (e.type != EventType.KeyDown) return false;

        // Space => Play/Pause
        if (e.keyCode == KeyCode.Space)
        {
            if (preview.IsPlaying)
                preview.StopPlaying();
            else
                preview.StartPlaying();

            e.Use();
            return true;
        }

        // S => Stop + scrubber to 0
        if (e.keyCode == KeyCode.S)
        {
            preview.StopPlaying();
            preview.SetScrubber(0f);
            preview.EnsurePreviewActive();
            onScrubToTime?.Invoke(0f);
            e.Use();
            return true;
        }

        // Delete/Backspace => delete selection
        if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
        {
            if (selection.Count > 0)
            {
                onDeleteSelection?.Invoke();
                selection.Clear();
            }

            e.Use();
            return true;
        }

        // F => frame selection (scrub to min start time)
        if (e.keyCode == KeyCode.F)
        {
            if (selection.Count > 0)
            {
                float best = 1f;

                var tmp = new List<SelectionKey>();
                selection.CopyTo(tmp);

                var so = getSerializedObject?.Invoke();
                if (so != null && registry != null)
                {
                    for (int i = 0; i < tmp.Count; i++)
                    {
                        var s = tmp[i];
                        var meta = registry.GetMetadata(s.fieldName);
                        if (meta == null) continue;

                        var st = WindowPropertyAccessor.GetStart(so, s.fieldName, s.index, meta.StructureType);
                        if (st != null)
                            best = Mathf.Min(best, Mathf.Clamp01(st.floatValue));
                    }
                }

                onScrubToTime?.Invoke(Mathf.Clamp01(best));
            }

            e.Use();
            return true;
        }

        return false;
    }

    #endregion
}
