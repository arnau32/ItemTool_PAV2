using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Reusable component for editing AnimationCurve speed multipliers in timeline editors.
/// Handles graph rendering, key selection, recording mode, and tangent manipulation.
internal sealed class SpeedCurveEditor
{
    #region Fields

    private bool _recordMode = false;
    private float _recordSpeed = 1f;
    private int _selectedKeyIndex = -1;

    private bool _isSpeedDrag = false;
    private Vector2 _speedDragStartMouse;
    private float _speedDragStartValue;

    private const float KEY_SNAP_EPS = 0.01f;
    private const float SPEED_DRAG_SENS = 0.02f;
    private const int GRAPH_SAMPLES = 64;
    private const float GRAPH_H = 86f;
    private const float GRAPH_PAD = 6f;
    private const float KEY_HIT_RADIUS = 10f;

    #endregion

    #region Properties

    public bool IsRecording => _recordMode;

    public float RecordSpeed
    {
        get => _recordSpeed;
        set => _recordSpeed = value;
    }

    public int SelectedKeyIndex => _selectedKeyIndex;

    #endregion

    #region Public API

    /// Draws the complete speed curve inspector UI.
    public void DrawInspector(SerializedProperty curveProperty, string curveLabel, float currentTime01, Func<float, float> evaluateSpeed, Action onCurveChanged, AnimationClip clip, Action<float> scrubToTime)
    {
        if (curveProperty == null) return;

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(curveProperty, new GUIContent(curveLabel));
        if (EditorGUI.EndChangeCheck())
        {
            onCurveChanged?.Invoke();
        }

        float speedNow = evaluateSpeed?.Invoke(currentTime01) ?? 1f;
        EditorGUILayout.LabelField($"t={currentTime01:0.00}  speed={speedNow:0.00}");

        Rect graphRect = GUILayoutUtility.GetRect(10, GRAPH_H, GUILayout.ExpandWidth(true));
        DrawSpeedGraph(graphRect, curveProperty, currentTime01, evaluateSpeed);

        DrawCurveKeysOverlay(graphRect, curveProperty, currentTime01, clip, scrubToTime, onCurveChanged);

        EditorGUILayout.Space(6);

        DrawRecordControls(speedNow, currentTime01, curveProperty, onCurveChanged);

        EditorGUILayout.Space(6);

        DrawQuickActions(currentTime01, speedNow, curveProperty, clip, scrubToTime, onCurveChanged);

        DrawTangentTools(curveProperty, onCurveChanged);

        DrawSelectedKeyEditor(curveProperty, clip, scrubToTime, onCurveChanged);
    }

    /// Handles speed drag event (Ctrl/Alt + vertical mouse drag while scrubbing).
    /// Returns true if the event was consumed.
    public bool HandleSpeedDragEvent(Event e, float scrubber01, Func<float, float> evaluateSpeed, SerializedProperty curveProperty, Action onCurveChanged)
    {
        if (!_recordMode) return false;
        if (e == null) return false;

        if (e.type == EventType.MouseDrag && (e.control || e.alt))
        {
            if (!_isSpeedDrag)
            {
                _isSpeedDrag = true;
                _speedDragStartMouse = e.mousePosition;
                _speedDragStartValue = evaluateSpeed?.Invoke(scrubber01) ?? 1f;
            }
            else
            {
                float dy = (_speedDragStartMouse.y - e.mousePosition.y);
                float newSpeed = Mathf.Max(0.01f, _speedDragStartValue + dy * SPEED_DRAG_SENS);
                _recordSpeed = newSpeed;
                AddOrUpdateSpeedKey(scrubber01, _recordSpeed, curveProperty, onCurveChanged);
            }

            return true;
        }

        // Reset speed drag when modifiers released
        if (e.type == EventType.MouseDrag && _isSpeedDrag && !(e.control || e.alt))
        {
            _isSpeedDrag = false;
            return true;
        }

        if (e.type == EventType.MouseUp)
        {
            _isSpeedDrag = false;
        }

        return false;
    }

    /// Resets internal state (call on editor window close or data change).
    public void Reset()
    {
        _selectedKeyIndex = -1;
        _isSpeedDrag = false;
        _recordMode = false;
    }

    public void ResetDrag() => _isSpeedDrag = false;

    #endregion

    #region Graph Drawing

    private void DrawSpeedGraph(Rect r, SerializedProperty curveProperty, float currentTime01, Func<float, float> evaluateSpeed)
    {
        EditorGUI.DrawRect(r, new Color(0.13f, 0.13f, 0.13f));
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), new Color(1f, 1f, 1f, 0.08f));
        EditorGUI.DrawRect(new Rect(r.x, r.center.y, r.width, 1f), new Color(1f, 1f, 1f, 0.06f));
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), new Color(1f, 1f, 1f, 0.08f));

        var curve = curveProperty.animationCurveValue;
        if (curve == null || curve.length == 0) return;

        // Determine y-range from curve keys
        float yMin = float.MaxValue;
        float yMax = float.MinValue;
        var keys = curve.keys;
        for (int i = 0; i < keys.Length; i++)
        {
            yMin = Mathf.Min(yMin, keys[i].value);
            yMax = Mathf.Max(yMax, keys[i].value);
        }

        if (Mathf.Abs(yMax - yMin) < 0.0001f)
        {
            yMin -= 1f;
            yMax += 1f;
        }

        yMin = Mathf.Max(0.01f, yMin);
        yMax = Mathf.Max(yMin + 0.01f, yMax);

        // Sample curve
        Vector3[] pts = new Vector3[GRAPH_SAMPLES];
        for (int i = 0; i < GRAPH_SAMPLES; i++)
        {
            float t = i / (GRAPH_SAMPLES - 1f);
            float s = evaluateSpeed?.Invoke(t) ?? 1f;

            float nx = t;
            float ny = Mathf.InverseLerp(yMin, yMax, s);

            float px = Mathf.Lerp(r.x + GRAPH_PAD, r.xMax - GRAPH_PAD, nx);
            float py = Mathf.Lerp(r.yMax - GRAPH_PAD, r.y + GRAPH_PAD, ny);

            pts[i] = new Vector3(px, py, 0f);
        }

        Handles.BeginGUI();
        Handles.color = new Color(0.2f, 0.9f, 0.9f, 1f);
        Handles.DrawAAPolyLine(2.5f, pts);

        // Current time marker
        float mx = Mathf.Lerp(r.x + GRAPH_PAD, r.xMax - GRAPH_PAD, Mathf.Clamp01(currentTime01));
        Handles.color = new Color(1f, 1f, 1f, 0.85f);
        Handles.DrawLine(new Vector3(mx, r.y + 4f), new Vector3(mx, r.yMax - 4f));
        Handles.EndGUI();
    }

    private void DrawCurveKeysOverlay(Rect graphRect, SerializedProperty curveProperty, float currentTime01, AnimationClip clip, Action<float> scrubToTime, Action onCurveChanged)
    {
        var curve = curveProperty.animationCurveValue;
        if (curve == null || curve.length == 0) return;

        // y-range from keys
        float yMin = float.MaxValue;
        float yMax = float.MinValue;
        for (int i = 0; i < curve.length; i++)
        {
            yMin = Mathf.Min(yMin, curve.keys[i].value);
            yMax = Mathf.Max(yMax, curve.keys[i].value);
        }

        if (Mathf.Abs(yMax - yMin) < 0.0001f)
        {
            yMin -= 1f;
            yMax += 1f;
        }

        yMin = Mathf.Max(0.01f, yMin);
        yMax = Mathf.Max(yMin + 0.01f, yMax);

        // Draw key dots
        Handles.BeginGUI();
        for (int i = 0; i < curve.length; i++)
        {
            float t = Mathf.Clamp01(curve.keys[i].time);
            float v = curve.keys[i].value;

            float px = Mathf.Lerp(graphRect.x + GRAPH_PAD, graphRect.xMax - GRAPH_PAD, t);
            float py = Mathf.Lerp(graphRect.yMax - GRAPH_PAD, graphRect.y + GRAPH_PAD,
                Mathf.InverseLerp(yMin, yMax, v));

            float r = (i == _selectedKeyIndex) ? 5f : 4f;
            Handles.color = (i == _selectedKeyIndex) ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0.75f);
            Handles.DrawSolidDisc(new Vector3(px, py), Vector3.forward, r);
        }

        Handles.EndGUI();

        // Click select key
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && graphRect.Contains(e.mousePosition))
        {
            int hit = -1;
            float best = 999999f;

            for (int i = 0; i < curve.length; i++)
            {
                float t = Mathf.Clamp01(curve.keys[i].time);
                float v = curve.keys[i].value;

                float px = Mathf.Lerp(graphRect.x + GRAPH_PAD, graphRect.xMax - GRAPH_PAD, t);
                float py = Mathf.Lerp(graphRect.yMax - GRAPH_PAD, graphRect.y + GRAPH_PAD,
                    Mathf.InverseLerp(yMin, yMax, v));

                float d = Vector2.Distance(e.mousePosition, new Vector2(px, py));
                if (d < best && d <= KEY_HIT_RADIUS)
                {
                    best = d;
                    hit = i;
                }
            }

            if (hit >= 0)
            {
                _selectedKeyIndex = hit;
                scrubToTime?.Invoke(Mathf.Clamp01(curve.keys[hit].time));
                e.Use();
            }
        }
    }

    #endregion

    #region Inspector Controls

    private void DrawRecordControls(float speedNow, float currentTime01, SerializedProperty curveProperty, Action onCurveChanged)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            _recordMode = EditorGUILayout.ToggleLeft("Record", _recordMode, GUILayout.Width(80));

            EditorGUI.BeginChangeCheck();
            _recordSpeed = EditorGUILayout.Slider("Speed", _recordSpeed, 0.1f, 10f);
            if (EditorGUI.EndChangeCheck() && _recordMode)
            {
                AddOrUpdateSpeedKey(currentTime01, _recordSpeed, curveProperty, onCurveChanged);
            }
        }

        if (_recordMode)
        {
            _recordSpeed = speedNow;
        }
    }

    private void DrawQuickActions(float currentTime01, float speedNow, SerializedProperty curveProperty, AnimationClip clip, Action<float> scrubToTime, Action onCurveChanged)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add/Update Key at Time"))
                AddOrUpdateSpeedKey(currentTime01, speedNow, curveProperty, onCurveChanged);

            if (GUILayout.Button("Snap Scrubber to Nearest Key"))
                SnapScrubberToNearestSpeedKey(currentTime01, curveProperty, scrubToTime);
        }
    }

    private void DrawTangentTools(SerializedProperty curveProperty, Action onCurveChanged)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Tangents: Auto"))
                SetAllTangents(curveProperty, AnimationUtility.TangentMode.Auto, onCurveChanged);

            if (GUILayout.Button("Tangents: Linear"))
                SetAllTangents(curveProperty, AnimationUtility.TangentMode.Linear, onCurveChanged);

            if (GUILayout.Button("Tangents: Flat"))
                SetAllTangentsFlat(curveProperty, onCurveChanged);
        }
    }

    private void DrawSelectedKeyEditor(SerializedProperty curveProperty, AnimationClip clip, Action<float> scrubToTime, Action onCurveChanged)
    {
        var curve = curveProperty.animationCurveValue;
        if (curve == null || _selectedKeyIndex < 0 || _selectedKeyIndex >= curve.length) return;

        var keys = curve.keys;
        var k = keys[_selectedKeyIndex];

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Selected Key", EditorStyles.miniBoldLabel);

        EditorGUI.BeginChangeCheck();
        float newT = EditorGUILayout.Slider("Time", Mathf.Clamp01(k.time), 0f, 1f);
        float newV = EditorGUILayout.FloatField("Speed", k.value);
        if (EditorGUI.EndChangeCheck())
        {
            k.time = Mathf.Clamp01(newT);
            k.value = Mathf.Max(0.01f, newV);
            curve.MoveKey(_selectedKeyIndex, k);
            curveProperty.animationCurveValue = curve;
            onCurveChanged?.Invoke();

            scrubToTime?.Invoke(k.time);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
            if (GUILayout.Button("Delete Key"))
            {
                var list = new List<Keyframe>(curve.keys);
                list.RemoveAt(_selectedKeyIndex);
                curveProperty.animationCurveValue = new AnimationCurve(list.ToArray());
                _selectedKeyIndex = -1;
                onCurveChanged?.Invoke();
            }

            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Clear Selection"))
                _selectedKeyIndex = -1;
        }
    }

    #endregion

    #region Key Manipulation

    private void AddOrUpdateSpeedKey(float t01, float speed, SerializedProperty curveProperty, Action onCurveChanged)
    {
        var curve = curveProperty.animationCurveValue;
        if (curve == null)
            curve = new AnimationCurve();

        float t = Mathf.Clamp01(t01);
        float v = Mathf.Max(0.01f, speed);

        int idx = -1;
        var keys = curve.keys;
        for (int i = 0; i < keys.Length; i++)
        {
            if (Mathf.Abs(keys[i].time - t) <= KEY_SNAP_EPS)
            {
                idx = i;
                break;
            }
        }

        if (idx >= 0)
        {
            var k = keys[idx];
            k.time = t;
            k.value = v;
            curve.MoveKey(idx, k);
        }
        else
        {
            curve.AddKey(new Keyframe(t, v));
        }

        // Auto smooth tangents
        for (int i = 0; i < curve.length; i++)
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
        for (int i = 0; i < curve.length; i++)
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);

        curveProperty.animationCurveValue = curve;
        onCurveChanged?.Invoke();
    }

    private void SnapScrubberToNearestSpeedKey(float currentTime01, SerializedProperty curveProperty,
        Action<float> scrubToTime)
    {
        var curve = curveProperty.animationCurveValue;
        if (curve == null) return;

        var keys = curve.keys;
        if (keys == null || keys.Length == 0) return;

        float t = Mathf.Clamp01(currentTime01);
        float best = keys[0].time;
        float bestD = Mathf.Abs(best - t);

        for (int i = 1; i < keys.Length; i++)
        {
            float d = Mathf.Abs(keys[i].time - t);
            if (d < bestD)
            {
                bestD = d;
                best = keys[i].time;
            }
        }

        scrubToTime?.Invoke(Mathf.Clamp01(best));
    }

    private void SetAllTangents(SerializedProperty curveProperty, AnimationUtility.TangentMode mode,
        Action onCurveChanged)
    {
        var curve = curveProperty.animationCurveValue;
        if (curve == null) return;

        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, mode);
            AnimationUtility.SetKeyRightTangentMode(curve, i, mode);
        }

        curveProperty.animationCurveValue = curve;
        onCurveChanged?.Invoke();
    }

    private void SetAllTangentsFlat(SerializedProperty curveProperty, Action onCurveChanged)
    {
        var curve = curveProperty.animationCurveValue;
        if (curve == null) return;

        var ks = curve.keys;
        for (int i = 0; i < ks.Length; i++)
        {
            var k = ks[i];
            k.inTangent = 0f;
            k.outTangent = 0f;
            curve.MoveKey(i, k);
        }

        curveProperty.animationCurveValue = curve;
        onCurveChanged?.Invoke();
    }

    #endregion
}
