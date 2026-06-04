using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class DodgeDataWindowEditor : EditorWindow, ITimelineDragCallbacks<SelectionKey>
{
    #region Fields

    private DodgeData _dodge;
    private SerializedObject _so;
    private GameObject _playerGO;

    private readonly EditorWindowPreviewController _preview = new EditorWindowPreviewController();
    private readonly SelectionModel<SelectionKey> _selection = new SelectionModel<SelectionKey>();

    // Timeline system
    private readonly TimelineDragController<SelectionKey> _timelineDrag = new TimelineDragController<SelectionKey>();
    private readonly List<TimelineBlockVisual<SelectionKey>> _blockVisuals = new List<TimelineBlockVisual<SelectionKey>>(32);
    private TimelineLayout _timelineLayout;

    private float _dragMinLen = 0.005f;
    private const float HANDLE_W_PX = 10f; // Must match TimelineDraw.DrawBlock handle width

    // Layout constants (kept small, but computed dynamically when registry is used)
    private const float BLOCK_H = 18f;
    private const float BLOCK_V_PAD = 4f;
    private const float BLOCK_ROW_GAP = 2f;

    // Speed curve
    private readonly SpeedCurveEditor _speedCurveEditor = new SpeedCurveEditor();

    // Animator speed curve (DodgeSpeed multiplier)
    private readonly SpeedCurveEditor _animSpeedCurveEditor = new SpeedCurveEditor();

    private readonly WindowListInspector _listInspector = new WindowListInspector();
    private readonly TimelineDataBuilder _dataBuilder = new TimelineDataBuilder();
    private readonly EditorShortcutsHandler _shortcuts = new EditorShortcutsHandler();

    // Optional: attribute driven registry (if DodgeData has [WindowType] fields)
    private readonly WindowReflectionRegistry _registry = new WindowReflectionRegistry();

    private Vector2 _scroll;

    // Cached properties
    private SerializedProperty _cachedClipProp;

    #endregion

    #region Unity Lifecycle

    public static void Open(DodgeData dodge)
    {
        var w = GetWindow<DodgeDataWindowEditor>("Dodge Window Editor");
        w.SetDodge(dodge);
        w.Show();
    }

    private void OnEnable()
    {
        _registry.Initialize(typeof(DodgeData));
        EditorApplication.update += OnEditorUpdate;
        _preview.OnEnable();
        _preview.SetScrubber(0f);
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        _preview.OnDisable();
    }

    private void OnEditorUpdate()
    {
        if (_dodge == null || _dodge.animationClip == null) return;

        _preview.Tick(
            _dodge.animationClip,
            GetOrFindPlayer,
            t01 => EvalSpeed(t01),
            t01 => Sample(t01, _dodge.animationClip),
            Repaint
        );
    }

    private void OnGUI()
    {
        // Header
        if (ScriptableObjectDropZone.DrawHeader("Context", "Dodge", "Drag & Drop DodgeData Here", ref _dodge, SetDodge))
        {
            // Object changed via drop zone
        }

        if (_dodge == null || _so == null)
        {
            EditorGUILayout.HelpBox("Drag & Drop a DodgeData or select one.", MessageType.Info);
            return;
        }

        _so.Update();

        if (_cachedClipProp == null)
            _cachedClipProp = _so.FindProperty("animationClip");

        if (_cachedClipProp == null || _cachedClipProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Assign an AnimationClip in DodgeData.", MessageType.Warning);
            _so.ApplyModifiedProperties();
            return;
        }

        var clip = (AnimationClip)_cachedClipProp.objectReferenceValue;

        // Shortcuts (registry is optional; F framing will work only if metadata exists)
        _shortcuts.HandleEvent(
            Event.current,
            _preview,
            clip,
            _selection,
            t01 => ScrubAndAutoPreview(t01, clip),
            OnDeleteSelection,
            GetSerializedObject,
            _registry
        );

        EditorGUILayout.Space(8);

        _preview.DrawPreviewControls(clip, GetOrFindPlayer, t01 => Sample(t01, clip), () => { });

        EditorGUILayout.Space(8);

        DrawTimeline(clip);

        EditorGUILayout.Space(10);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawSpeedCurveSection(clip);

        EditorGUILayout.Space(10);

        DrawWindowEventsInspector(clip);

        EditorGUILayout.EndScrollView();

        _so.ApplyModifiedProperties();
    }

    private void OnDeleteSelection()
    {
        // Dodge has no delete selection
    }

    private SerializedObject GetSerializedObject() => _so;

    #endregion

    #region Timeline

    private void DrawTimeline(AnimationClip clip)
    {
        EditorGUILayout.LabelField("TIMELINE", EditorStyles.boldLabel);

        // If DodgeData has [WindowType] attributes, build tracks automatically.
        var metas = _registry.GetAllOrdered();
        bool hasRegistryTracks = metas != null && metas.Count > 0;

        if (hasRegistryTracks)
        {
            DrawTimeline_RegistryDriven(clip, metas);
            return;
        }

        // Fallback: legacy hardcoded 4 tracks
        DrawTimeline_LegacyFallback(clip);
    }

    private void DrawTimeline_RegistryDriven(AnimationClip clip, List<WindowReflectionRegistry.WindowMetadata> metas)
    {
        // Build data per track
        var trackDataList = new List<(WindowReflectionRegistry.WindowMetadata meta, List<TimelineDataBuilder.WindowRef> data, int rows)>(metas.Count);

        int maxRows = 1;
        for (int i = 0; i < metas.Count; i++)
        {
            var meta = metas[i];

            // Dodge editor: treat all tracks as enabled if no enable field exists
            bool enabled = _registry.IsEnabled(_so, meta);

            List<TimelineDataBuilder.WindowRef> data = null;
            if (enabled)
            {
                var prop = _registry.GetSingleProperty(_so, meta);

                switch (meta.StructureType)
                {
                    case WindowStructureType.Single:
                        data = _dataBuilder.BuildFromSingleWindow(prop, meta.FieldName, meta.DisplayName, meta.Color);
                        break;

                    case WindowStructureType.List:
                        data = _dataBuilder.BuildFromSimpleList(prop, meta.FieldName, meta.DisplayName, meta.Color);
                        break;

                    case WindowStructureType.SpecialDamage:
                        data = _dataBuilder.BuildFromDamageList(prop, meta.FieldName, meta.Color);
                        break;

                    case WindowStructureType.SpecialVfx:
                        data = _dataBuilder.BuildFromVfxList(prop, meta.FieldName, meta.Color);
                        break;
                }
            }

            int rows = (data != null) ? _dataBuilder.AssignNonOverlappingRows(data) : 0;
            trackDataList.Add((meta, data, rows));
            maxRows = Mathf.Max(maxRows, rows);
        }

        float trackH = TrackHeight(maxRows);

        const float HEADER_W = 180f;
        const float RULER_H = 22f;
        const float TRACK_SPACING = 6f;
        const float INNER_PAD = 6f;

        var tmpLayout = new TimelineLayout(new Rect(0, 0, 100, 100), trackCount: metas.Count, headerWidth: HEADER_W, rulerHeight: RULER_H,
            trackHeight: trackH, trackSpacing: TRACK_SPACING, innerPadding: INNER_PAD);

        float totalH = Mathf.Max(120f, tmpLayout.RulerHeight + tmpLayout.TotalTracksHeight + INNER_PAD);
        Rect totalRect = GUILayoutUtility.GetRect(10f, totalH, GUILayout.ExpandWidth(true));

        _timelineLayout = new TimelineLayout(totalRect, trackCount: metas.Count, headerWidth: HEADER_W, rulerHeight: RULER_H,
            trackHeight: trackH, trackSpacing: TRACK_SPACING, innerPadding: INNER_PAD);

        TimelineDraw.DrawBackground(_timelineLayout.TotalRect);
        TimelineDraw.DrawPanels(_timelineLayout);
        TimelineDraw.DrawGrid(_timelineLayout, minorDivisions: 20, majorDivisions: 5);
        TimelineDraw.DrawRuler(_timelineLayout, ticks: 10);
        TimelineDraw.DrawTrackSeparators(_timelineLayout);

        _blockVisuals.Clear();

        for (int i = 0; i < trackDataList.Count; i++)
        {
            var (meta, data, _) = trackDataList[i];
            bool enabled = _registry.IsEnabled(_so, meta);

            TimelineDraw.DrawTrackHeaderWithAdd(_timelineLayout, i, meta.DisplayName, enabled, () =>
            {
                ResetWindowToDefault(meta.FieldName, selectIndex: 0);
            });

            DrawTrackBlocks(_timelineLayout, i, enabled, data);
        }

        TimelineDraw.DrawScrubber(_timelineLayout, _preview.Scrubber01);

        _timelineDrag.EnableMarker = false;
        _timelineDrag.HandleEvent(
            Event.current,
            _timelineLayout,
            _blockVisuals,
            _selection,
            this,
            handleSnapPx: 7f,
            allowShiftMultiSelect: true,
            sampleOnScrub: true,
            sampleOnDrag: true,
            moveAllSelected: false
        );
    }

    private void DrawTimeline_LegacyFallback(AnimationClip clip)
    {
        Rect totalRect = GUILayoutUtility.GetRect(10, 160, GUILayout.ExpandWidth(true));
        _timelineLayout = new TimelineLayout(totalRect, trackCount: 4, headerWidth: 160f);

        TimelineDraw.DrawBackground(totalRect);
        TimelineDraw.DrawPanels(_timelineLayout);
        TimelineDraw.DrawGrid(_timelineLayout);
        TimelineDraw.DrawRuler(_timelineLayout);

        _blockVisuals.Clear();

        DrawLegacySingleTrack(0, "iFrame", "iFrameWindow", Color.green);
        DrawLegacySingleTrack(1, "AtkBuffer", "attackBufferWindow", Color.magenta);
        DrawLegacySingleTrack(2, "MoveCancel", "movementCancelation", Color.cyan);
        DrawLegacySingleTrack(3, "BufferEnd", "onBufferEndDodgeAnimation", Color.yellow);

        TimelineDraw.DrawScrubber(_timelineLayout, _preview.Scrubber01);

        _timelineDrag.EnableMarker = false;
        _timelineDrag.HandleEvent(
            Event.current,
            _timelineLayout,
            _blockVisuals,
            _selection,
            this,
            handleSnapPx: 7f,
            allowShiftMultiSelect: true,
            sampleOnScrub: true,
            sampleOnDrag: true,
            moveAllSelected: false
        );
    }

    private void DrawLegacySingleTrack(int trackIndex, string displayLabel, string fieldName, Color color)
    {
        TimelineDraw.DrawTrackHeaderWithAdd(_timelineLayout, trackIndex, displayLabel, true, () =>
        {
            ResetWindowToDefault(fieldName, selectIndex: 0);
        });

        var p = _so.FindProperty(fieldName);
        if (p == null) return;

        var data = _dataBuilder.BuildFromSingleWindow(p, fieldName, displayLabel, color);
        if (data == null || data.Count == 0) return;

        DrawTrackBlocks(_timelineLayout, trackIndex, true, data);
    }

    private void DrawTrackBlocks(TimelineLayout layout, int trackIndex, bool enabled, List<TimelineDataBuilder.WindowRef> windows)
    {
        Rect trackRect = layout.GetTrackRect(trackIndex);
        EditorGUI.DrawRect(trackRect, enabled ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.12f, 0.12f, 0.12f));

        if (!enabled || windows == null || windows.Count == 0) return;

        float trackY = trackRect.y + BLOCK_V_PAD;
        float blockHeightWithGap = BLOCK_H + BLOCK_ROW_GAP;

        for (int i = 0; i < windows.Count; i++)
        {
            var w = windows[i];

            float s = Mathf.Clamp01(w.start01);
            float e = Mathf.Clamp01(w.end01);
            if (e < s) e = s;

            float rowY = trackY + (w.row * blockHeightWithGap);

            float x1 = layout.Time01ToX(s);
            float x2 = layout.Time01ToX(e);
            float wPx = Mathf.Max(2f, x2 - x1);

            var blockRect = new Rect(x1, rowY, wPx, BLOCK_H);

            var id = new SelectionKey { fieldName = w.fieldName, index = w.index };

            _blockVisuals.Add(new TimelineBlockVisual<SelectionKey>(id, trackIndex, blockRect, handleWidthPx: HANDLE_W_PX));
            TimelineDraw.DrawBlock(blockRect, w.label, w.color, Color.black, _selection.Contains(id));
        }
    }

    private float TrackHeight(int rows)
    {
        rows = Mathf.Max(1, rows);
        return (BLOCK_V_PAD * 2f) + (rows * BLOCK_H) + ((rows - 1) * BLOCK_ROW_GAP);
    }

    private void ResetWindowToDefault(string fieldName, int selectIndex)
    {
        var p = _so.FindProperty(fieldName);
        if (p == null) return;

        var start = p.FindPropertyRelative("start");
        var end = p.FindPropertyRelative("end");
        if (start == null || end == null) return;

        const float len = 0.12f;

        float s = Mathf.Clamp01(_preview.Scrubber01);
        float e = Mathf.Clamp01(s + len);

        if (e > 1f)
        {
            e = 1f;
            s = Mathf.Max(0f, e - len);
        }

        BeginDodgeEdit("Reset Dodge Event Window");
        start.floatValue = s;
        end.floatValue = e;
        ApplyAndDirty();

        _selection.SetSingle(new SelectionKey { fieldName = fieldName, index = selectIndex });
        Repaint();
    }

    #endregion

    #region Speed Curve Section

    private void DrawSpeedCurveSection(AnimationClip clip)
    {
        EditorGUILayout.LabelField("Dodge Speed Curve (Final Speed)", EditorStyles.boldLabel);

        _speedCurveEditor.DrawInspector(
            _so.FindProperty("dodgeSpeedCurve"),
            "Speed Curve",
            _preview.Scrubber01,
            t01 => EvalSpeed(t01),
            () =>
            {
                BeginDodgeEdit("Edit Dodge Speed Curve");
                ApplyAndDirty();
            },
            clip,
            t01 => ScrubAndAutoPreview(t01, clip)
        );

        float speedNow = EvalSpeed(_preview.Scrubber01);
        EditorGUILayout.LabelField($"t={_preview.Scrubber01:0.00}  speed={speedNow:0.00}");

        EditorGUILayout.Space(12);

        DrawAnimatorSpeedCurveSection(clip);
    }

    private void DrawAnimatorSpeedCurveSection(AnimationClip clip)
    {
        EditorGUILayout.LabelField("Dodge Animator Speed (DodgeSpeed multiplier)", EditorStyles.boldLabel);

        var useProp = _so.FindProperty("useAnimatorSpeedCurve");
        var curveProp = _so.FindProperty("animatorSpeedCurve");

        if (useProp != null)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(useProp, new GUIContent("Use Animator Speed Curve"));
            if (EditorGUI.EndChangeCheck())
            {
                BeginDodgeEdit("Toggle Dodge Animator Speed Curve");
                ApplyAndDirty();
            }
        }

        bool enabled = (useProp != null) && useProp.boolValue;

        using (new EditorGUI.DisabledScope(!enabled))
        {
            _animSpeedCurveEditor.DrawInspector(
                curveProp,
                "Animator Speed Curve",
                _preview.Scrubber01,
                t01 => EvalAnimatorSpeed(t01),
                () =>
                {
                    BeginDodgeEdit("Edit Dodge Animator Speed Curve");
                    ApplyAndDirty();
                },
                clip,
                t01 => ScrubAndAutoPreview(t01, clip)
            );

            float animSpeedNow = EvalAnimatorSpeed(_preview.Scrubber01);
            EditorGUILayout.LabelField($"t={_preview.Scrubber01:0.00}  DodgeSpeed={animSpeedNow:0.00}");
        }
    }

    private float EvalSpeed(float t01) => _dodge != null ? _dodge.EvaluateDodgeSpeed(t01) : 5f;

    private float EvalAnimatorSpeed(float t01) => _dodge != null ? _dodge.EvaluateAnimatorSpeed(t01) : 1f;

    #endregion

    #region Window Events Inspector

    private void DrawWindowEventsInspector(AnimationClip clip)
    {
        EditorGUILayout.LabelField("Window Events", EditorStyles.boldLabel);

        // Uses fieldName strings; works regardless of registry attributes.
        DrawWindow("iFrame", "iFrameWindow", "Edit iFrame Window", clip);
        DrawWindow("Buffer End", "onBufferEndDodgeAnimation", "Edit Buffer End Window", clip);
        DrawWindow("Movement Cancel", "movementCancelation", "Edit Movement Cancel Window", clip);
        DrawWindow("Attack Buffer", "attackBufferWindow", "Edit Attack Buffer Window", clip);
    }

    private Action<float, float> CreateEditWindowCallback(string fieldName, string undoLabel)
    {
        return (ns, ne) =>
        {
            BeginDodgeEdit(undoLabel);

            var prop = _so.FindProperty(fieldName);
            if (prop == null) return;

            var s = prop.FindPropertyRelative("start");
            var e = prop.FindPropertyRelative("end");
            if (s == null || e == null) return;

            s.floatValue = Mathf.Clamp01(ns);
            e.floatValue = Mathf.Clamp01(ne);

            ApplyAndDirty();
        };
    }

    private void DrawWindow(string label, string fieldName, string undoLabel, AnimationClip clip)
    {
        _listInspector.DrawSingleEventWindow(
            label,
            fieldName,
            0,
            _so.FindProperty(fieldName),
            _selection,
            CreateEditWindowCallback(fieldName, undoLabel),
            (shift, idx) => HandleListSelection(shift, fieldName, idx),
            t => ScrubAndAutoPreview(t, clip)
        );
    }

    #endregion

    #region Selection Helpers

    private void HandleListSelection(bool shift, string fieldName, int index)
    {
        var key = new SelectionKey { fieldName = fieldName, index = index };
        if (shift) _selection.Toggle(key);
        else _selection.SetSingle(key);
        Repaint();
    }

    #endregion

    #region Preview & Sampling

    private GameObject GetOrFindPlayer()
    {
        if (_playerGO == null) _playerGO = GameObject.FindGameObjectWithTag("Player");
        return _playerGO;
    }

    private void Sample(float norm01, AnimationClip clip)
    {
        if (_playerGO == null || clip == null) return;

        _preview.EnsurePreviewActive();

        float tSec = Mathf.Clamp01(norm01) * clip.length;
        AnimationMode.SampleAnimationClip(_playerGO, clip, tSec);

        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    private void ScrubAndAutoPreview(float t01, AnimationClip clip)
    {
        _preview.SetScrubber(Mathf.Clamp01(t01));
        _preview.EnsurePreviewActive();
        Sample(_preview.Scrubber01, clip);
    }

    #endregion

    #region Data Management

    private void SetDodge(DodgeData d)
    {
        _dodge = d;
        _so = _dodge != null ? new SerializedObject(_dodge) : null;

        _cachedClipProp = null;

        _selection.Clear();
        _speedCurveEditor.Reset();
        _animSpeedCurveEditor.Reset();

        _preview.StopPlaying();
        Repaint();
    }

    private void BeginDodgeEdit(string undoLabel)
    {
        if (_dodge == null) return;
        Undo.RecordObject(_dodge, undoLabel);
    }

    private void ApplyAndDirty()
    {
        _so.ApplyModifiedProperties();
        EditorUtility.SetDirty(_dodge);
    }

    #endregion

    #region ITimelineDragCallbacks Implementation

    float ITimelineDragCallbacks<SelectionKey>.GetScrubber01() => _preview.Scrubber01;

    void ITimelineDragCallbacks<SelectionKey>.SetScrubber01(float t01, bool sampleNow)
    {
        _preview.SetScrubber(t01);
        if (sampleNow && _dodge?.animationClip != null)
            Sample(_preview.Scrubber01, _dodge.animationClip);
    }

    bool ITimelineDragCallbacks<SelectionKey>.TryGetRange01(SelectionKey id, out float start01, out float end01)
    {
        start01 = 0f;
        end01 = 0f;

        // Prefer registry-based access (supports lists/special structures if DodgeData is attributed).
        if (_so != null && _registry != null && WindowPropertyAccessor.TryGetRange(_so, id, _registry, out var sReg, out var eReg))
        {
            float s = Mathf.Clamp01(sReg.floatValue);
            float e = Mathf.Clamp01(eReg.floatValue);
            if (e < s) e = s;

            start01 = s;
            end01 = e;
            return true;
        }

        // Fallback: direct single window field lookup (legacy, no attributes).
        if (_so == null || string.IsNullOrEmpty(id.fieldName)) return false;

        var p = _so.FindProperty(id.fieldName);
        if (p == null) return false;

        var sDir = p.FindPropertyRelative("start");
        var eDir = p.FindPropertyRelative("end");
        if (sDir == null || eDir == null) return false;

        start01 = Mathf.Clamp01(sDir.floatValue);
        end01 = Mathf.Clamp01(eDir.floatValue);
        if (end01 < start01) end01 = start01;

        return true;
    }

    void ITimelineDragCallbacks<SelectionKey>.SetRange01(SelectionKey id, float start01, float end01, bool recordUndo)
    {
        if (_so == null || string.IsNullOrEmpty(id.fieldName)) return;

        start01 = Mathf.Clamp01(start01);
        end01 = Mathf.Clamp01(end01);
        if (end01 < start01) end01 = start01;

        // Prefer registry-based
        if (_registry != null && WindowPropertyAccessor.TryGetRange(_so, id, _registry, out var sReg, out var eReg))
        {
            if (recordUndo) BeginDodgeEdit("Edit Dodge Window");

            sReg.floatValue = start01;
            eReg.floatValue = end01;

            ApplyAndDirty();
            return;
        }

        // Fallback direct
        var p = _so.FindProperty(id.fieldName);
        if (p == null) return;

        var sDir = p.FindPropertyRelative("start");
        var eDir = p.FindPropertyRelative("end");
        if (sDir == null || eDir == null) return;

        if (recordUndo) BeginDodgeEdit("Edit Dodge Window");

        sDir.floatValue = start01;
        eDir.floatValue = end01;

        ApplyAndDirty();
    }

    float ITimelineDragCallbacks<SelectionKey>.MinDuration01(SelectionKey id) => _dragMinLen;

    bool ITimelineDragCallbacks<SelectionKey>.CanResize(SelectionKey id) => true;

    void ITimelineDragCallbacks<SelectionKey>.RequestRepaint() => Repaint();

    bool ITimelineDragCallbacks<SelectionKey>.ShouldRecordUndo(Event e) => true;

    #endregion
}
