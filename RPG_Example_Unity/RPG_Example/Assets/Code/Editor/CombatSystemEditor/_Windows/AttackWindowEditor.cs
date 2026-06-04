using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// FULLY AUTOMATED EDITOR - NO MANUAL MODIFICATION REQUIRED
/// =======================================================
///
/// To add a new window:
/// 1) Add the field in AttackData with the [WindowType] attribute
/// 2) Done - do not touch this file.
///
/// Example:
/// public bool allowSuperArmor;
/// [WindowType("Super Armor", 1f, 0.5f, 0.2f, trackOrder: 6,
///     EnableFieldName = "allowSuperArmor", StructureType = WindowStructureType.List)]
/// public List<WindowEvent> superArmorWindows;
/// </summary>
public class AttackWindowEditor : EditorWindow, ITimelineDragCallbacks<SelectionKey>
{
    #region Fields

    private AttackData _attack;
    private SerializedObject _so;

    private GameObject _playerGO;
    private int _playerInstanceID = -1;

    private readonly EditorWindowPreviewController _preview = new EditorWindowPreviewController();
    private readonly SelectionModel<SelectionKey> _selection = new SelectionModel<SelectionKey>();
    private readonly TimelineDragController<SelectionKey> _timelineDrag = new TimelineDragController<SelectionKey>();

    private readonly List<TimelineBlockVisual<SelectionKey>> _blockVisuals =
        new List<TimelineBlockVisual<SelectionKey>>(256);

    private TimelineLayout _timelineLayout;

    private bool _dragTimeToChange;
    private Rect _timeToChangeHandleRect;

    private float _dragMinLen = 0.005f;

    private readonly SpeedCurveEditor _speedCurveEditor = new SpeedCurveEditor();
    private readonly SpeedCurveEditor _rootMotionCurveEditor = new SpeedCurveEditor();
    private readonly SpeedCurveEditor _proceduralRootMotionCurveEditor = new SpeedCurveEditor();

    private readonly ComboBlendPreview _blendPreview = new ComboBlendPreview();
    private readonly WindowListInspector _listInspector = new WindowListInspector();
    private readonly TimelineDataBuilder _dataBuilder = new TimelineDataBuilder();
    private readonly EditorShortcutsHandler _shortcuts = new EditorShortcutsHandler();

    private readonly WindowReflectionRegistry _registry = new WindowReflectionRegistry();

    private Vector2 _scroll;

    // Cached properties
    private SerializedProperty _cachedClipProp;
    private SerializedProperty _cachedTimeToChangeProp;

    // Reusable temporary lists
    private readonly List<SelectionKey> _tempSelectionList = new List<SelectionKey>(32);

    private readonly Dictionary<string, List<int>> _deleteByFieldNameCache = new Dictionary<string, List<int>>(32);

    // Repaint flag
    private bool _needsRepaint;
    private readonly AnimationPreviewRenderer _animPreview = new AnimationPreviewRenderer();

    #endregion

    #region Layout Constants

    private const float INSPECTOR_W = 320f;

    private const float BLOCK_H = 18f;
    private const float BLOCK_V_PAD = 4f;
    private const float BLOCK_ROW_GAP = 2f;

    // Minimum visual width for a standard window block.
    private const float DEFAULT_WIN_LEN = 0.12f;
    private const float HANDLE_W_PX = 10f;

    // AudioTrigger blocks are point-in-time markers — give them enough
    // visual width to be clickable but keep them visually distinct from
    // full-duration window blocks.
    private const float TRIGGER_MIN_PX = 10f;

    // Cached undo strings
    private const string UNDO_EDIT_WINDOW = "Edit Window";
    private const string UNDO_DELETE_WINDOW = "Delete Window";
    private const string UNDO_EDIT_SPEED = "Edit Speed Curve";
    private const string UNDO_EDIT_ROOT_MOTION = "Edit Root Motion Curve";
    private const string UNDO_EDIT_PROCEDURAL = "Edit Procedural Key";
    private const string UNDO_EDIT_TIME_CHANGE = "Edit Time To Change Anim";
    private const string UNDO_NUDGE = "Nudge Windows";

    #endregion

    #region Unity Lifecycle

    public static void Open(AttackData attack)
    {
        var w = GetWindow<AttackWindowEditor>("Attack Window Editor");
        w.SetAttack(attack);
        w.Show();
    }

    private void OnEnable()
    {
        _registry.Initialize(typeof(AttackData));
        _preview.OnEnable();
        _preview.SetScrubber(0f);
        var player = GetOrFindPlayer();
        if (player != null)
        {
            _animPreview.Initialize(player);
        }
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        _preview.OnDisable();
        _blendPreview.Cleanup();
        _animPreview.Cleanup();
    }

    private void OnEditorUpdate()
    {
        if (_attack == null || _attack.animationClip == null) return;

        _preview.Tick(
            _attack.animationClip,
            GetOrFindPlayer,
            t01 => _attack.EvaluateAnimatorSpeed(t01),
            t01 => Sample(t01, _attack.animationClip),
            () => _needsRepaint = true
        );

        if (_needsRepaint)
        {
            _needsRepaint = false;
            Repaint();
        }
    }

    private void OnGUI()
    {
        if (ScriptableObjectDropZone.DrawHeader("Context", "Attack", "Drag & Drop AttackData Here", ref _attack, SetAttack))
        {
            // Object changed
        }

        if (_attack == null || _so == null)
        {
            EditorGUILayout.HelpBox("Drag & Drop an AttackData or select one.", MessageType.Info);
            return;
        }

        _so.Update();

        if (_cachedClipProp == null)
            _cachedClipProp = _so.FindProperty("animationClip");

        if (_cachedClipProp == null || _cachedClipProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Assign an AnimationClip in AttackData.", MessageType.Warning);
            _so.ApplyModifiedProperties();
            return;
        }

        var clip = (AnimationClip)_cachedClipProp.objectReferenceValue;

        _shortcuts.HandleEvent(
            Event.current,
            _preview,
            clip,
            _selection,
            t01 => ScrubAndAutoPreview(t01, clip),
            DeleteSelection,
            () => _so,
            _registry
        );

        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();

        // LEFT PANEL
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        _preview.DrawPreviewControls(clip, GetOrFindPlayer, t01 => Sample(t01, clip), () => { });

        if (_cachedTimeToChangeProp == null)
            _cachedTimeToChangeProp = _so.FindProperty("timeToChangeAnim");

        _blendPreview.DrawControls(
            _cachedTimeToChangeProp,
            () =>
            {
                BeginAttackEdit(UNDO_EDIT_TIME_CHANGE);
                ApplyAndDirty();
                _preview.EnsurePreviewActive();
                Sample(_preview.Scrubber01, clip);
            }
        );

        EditorGUILayout.Space(8);
        DrawTimeline(clip);
        EditorGUILayout.Space(10);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawSpeedCurves();
        EditorGUILayout.Space(10);
        DrawWindowsLists(clip);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // RIGHT PANEL
        EditorGUILayout.BeginVertical(GUILayout.Width(INSPECTOR_W));
        DrawSideInspector(clip);
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        _so.ApplyModifiedProperties();
    }

    #endregion

    #region Speed Curves

    private void DrawSpeedCurves()
    {
        EditorGUILayout.LabelField("Animator Speed Curve (Final Speed)", EditorStyles.boldLabel);
        var useCurve = _so.FindProperty("useAnimatorSpeedCurve");
        EditorGUILayout.PropertyField(useCurve);

        if (useCurve != null && useCurve.boolValue)
        {
            _speedCurveEditor.DrawInspector(
                _so.FindProperty("animatorSpeedCurve"),
                "Speed Curve",
                _preview.Scrubber01,
                t01 => _attack.EvaluateAnimatorSpeed(t01),
                () =>
                {
                    BeginAttackEdit(UNDO_EDIT_SPEED);
                    ApplyAndDirty();
                },
                _attack.animationClip,
                t01 => ScrubAndAutoPreview(t01, _attack.animationClip)
            );
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Root Motion Multiplier Curve", EditorStyles.boldLabel);
        var useRootMotionCurve = _so.FindProperty("useRootMotionMultiplier");
        EditorGUILayout.PropertyField(useRootMotionCurve);

        if (useRootMotionCurve != null && useRootMotionCurve.boolValue)
        {
            _rootMotionCurveEditor.DrawInspector(
                _so.FindProperty("rootMotionMultiplierCurve"),
                "Root Motion Curve",
                _preview.Scrubber01,
                t01 => _attack.EvaluateRootMotionMultiplier(t01),
                () =>
                {
                    BeginAttackEdit(UNDO_EDIT_ROOT_MOTION);
                    ApplyAndDirty();
                },
                _attack.animationClip,
                t01 => ScrubAndAutoPreview(t01, _attack.animationClip)
            );
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Procedural Root Motion Curve", EditorStyles.boldLabel);
        var useProceduralRootMotion = _so.FindProperty("useProceduralForwardMove");
        var proceduralForwardMoveDistance = _so.FindProperty("proceduralForwardMoveDistance");

        EditorGUILayout.PropertyField(useProceduralRootMotion);
        EditorGUILayout.PropertyField(proceduralForwardMoveDistance);

        if (useProceduralRootMotion != null && useProceduralRootMotion.boolValue)
        {
            _proceduralRootMotionCurveEditor.DrawInspector(
                _so.FindProperty("proceduralForwardMoveCurve"),
                "Procedural Root Motion Curve",
                _preview.Scrubber01,
                t01 => _attack.EvaluateProceduralForwardProgress01(t01),
                () =>
                {
                    BeginAttackEdit(UNDO_EDIT_PROCEDURAL);
                    ApplyAndDirty();
                },
                _attack.animationClip,
                t01 => ScrubAndAutoPreview(t01, _attack.animationClip)
            );
        }
    }

    #endregion

    #region Timeline

    private void DrawTimeline(AnimationClip clip)
    {
        EditorGUILayout.LabelField("TIMELINE", EditorStyles.boldLabel);

        var allMetadata = _registry.GetAllOrdered();

        var trackDataList =
            new List<(WindowReflectionRegistry.WindowMetadata meta, List<TimelineDataBuilder.WindowRef> data, int rows)>(
                allMetadata.Count
            );

        int maxRows = 1;
        for (int i = 0; i < allMetadata.Count; i++)
        {
            var meta = allMetadata[i];
            bool enabled = _registry.IsEnabled(_so, meta);

            List<TimelineDataBuilder.WindowRef> data = enabled ? BuildDataForMetadata(meta) : null;
            int rows = (data != null) ? _dataBuilder.AssignNonOverlappingRows(data) : 0;

            trackDataList.Add((meta, data, rows));
            maxRows = Mathf.Max(maxRows, rows);
        }

        float trackH = TrackHeight(maxRows);

        const float HEADER_W = 180f;
        const float RULER_H = 22f;
        const float TRACK_SPACING = 6f;
        const float INNER_PAD = 6f;

        int trackCount = allMetadata.Count;

        var tmpLayout = new TimelineLayout(
            new Rect(0, 0, 100, 100),
            trackCount,
            HEADER_W,
            RULER_H,
            trackH,
            TRACK_SPACING,
            INNER_PAD
        );

        float totalH = Mathf.Max(120f, tmpLayout.RulerHeight + tmpLayout.TotalTracksHeight + INNER_PAD);
        Rect totalRect = GUILayoutUtility.GetRect(10f, totalH, GUILayout.ExpandWidth(true));

        _timelineLayout = new TimelineLayout(
            totalRect,
            trackCount,
            HEADER_W,
            RULER_H,
            trackH,
            TRACK_SPACING,
            INNER_PAD
        );

        TimelineDraw.DrawBackground(_timelineLayout.TotalRect);
        TimelineDraw.DrawPanels(_timelineLayout);
        TimelineDraw.DrawGrid(_timelineLayout, 20, 5);
        TimelineDraw.DrawRuler(_timelineLayout, 10);
        TimelineDraw.DrawTrackSeparators(_timelineLayout);

        _blockVisuals.Clear();

        for (int i = 0; i < trackDataList.Count; i++)
        {
            var (meta, data, _) = trackDataList[i];
            bool enabled = _registry.IsEnabled(_so, meta);

            TimelineDraw.DrawTrackHeaderWithAdd(_timelineLayout, i, meta.DisplayName, enabled, () => AddWindow(meta));

            DrawTrackBlocks(_timelineLayout, i, enabled, data);
        }

        _blendPreview.DrawMarkerAndBlendZone(_timelineLayout.TimelineRect, clip, _attack.timeToChangeAnim);
        _timeToChangeHandleRect = _blendPreview.GetMarkerHitRect(_timelineLayout.TimelineRect, _attack.timeToChangeAnim);

        TimelineDraw.DrawScrubber(_timelineLayout, _preview.Scrubber01);

        HandleTimelineInput(_timelineLayout, clip);
    }

    private List<TimelineDataBuilder.WindowRef> BuildDataForMetadata(WindowReflectionRegistry.WindowMetadata meta)
    {
        var prop = _registry.GetListProperty(_so, meta);

        switch (meta.StructureType)
        {
            case WindowStructureType.SpecialDamage:
                return _dataBuilder.BuildFromDamageList(prop, meta.FieldName, meta.Color);

            case WindowStructureType.SpecialVfx:
                return _dataBuilder.BuildFromVfxList(prop, meta.FieldName, meta.Color);

            case WindowStructureType.SpecialAudioTrigger:
                return _dataBuilder.BuildFromAudioTriggerList(prop, meta.FieldName, meta.Color);

            case WindowStructureType.SpecialAudioWindow:
                return _dataBuilder.BuildFromAudioWindowList(prop, meta.FieldName, meta.Color);

            case WindowStructureType.Single:
                // For single windows, the property is still the field itself.
                return _dataBuilder.BuildFromSingleWindow(prop, meta.FieldName, meta.DisplayName, meta.Color);

            case WindowStructureType.List:
                return _dataBuilder.BuildFromSimpleList(prop, meta.FieldName, meta.DisplayName, meta.Color);

            default:
                return null;
        }
    }

    private float TrackHeight(int rows)
    {
        rows = Mathf.Max(1, rows);
        return (BLOCK_V_PAD * 2f) + (rows * BLOCK_H) + ((rows - 1) * BLOCK_ROW_GAP);
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
            var key = new SelectionKey { fieldName = w.fieldName, index = w.index };
            bool selected = _selection.Contains(key);

            // AudioTrigger: start == end — render as a centred pin marker.
            // Give it TRIGGER_MIN_PX width so it is always clickable regardless of zoom level.
            bool isTrigger = Mathf.Approximately(s, e);
            if (isTrigger)
            {
                float cx    = layout.Time01ToX(s);
                float half  = TRIGGER_MIN_PX * 0.5f;
                var blockRect = new Rect(cx - half, rowY, TRIGGER_MIN_PX, BLOCK_H);

                DrawTriggerPin(blockRect, w.label, w.color, selected);

                // Register with the full TRIGGER_MIN_PX width so hit-test works.
                _blockVisuals.Add(new TimelineBlockVisual<SelectionKey>(key, trackIndex, blockRect, TRIGGER_MIN_PX));
            }
            else
            {
                float x1 = layout.Time01ToX(s);
                float x2 = layout.Time01ToX(e);
                float wPx = Mathf.Max(2f, x2 - x1);

                var blockRect = new Rect(x1, rowY, wPx, BLOCK_H);

                TimelineDraw.DrawBlock(blockRect, w.label, w.color, new Color(0f, 0f, 0f, 0.35f), selected);
                _blockVisuals.Add(new TimelineBlockVisual<SelectionKey>(key, trackIndex, blockRect, HANDLE_W_PX));
            }
        }
    }

    // Draws an AudioTrigger as a vertical pin: thin stem + diamond head.
    // Visually distinct from window blocks so designers can tell them apart at a glance.
    private static void DrawTriggerPin(Rect blockRect, string label, Color color, bool selected)
    {
        // Background fill — slightly transparent so the grid is still visible.
        var fillColor = new Color(color.r, color.g, color.b, selected ? 0.95f : 0.75f);
        EditorGUI.DrawRect(blockRect, fillColor);

        // Bright vertical centre line — the "stem" of the pin.
        float cx       = blockRect.x + blockRect.width * 0.5f;
        var stemRect   = new Rect(cx - 1f, blockRect.y, 2f, blockRect.height);
        EditorGUI.DrawRect(stemRect, new Color(1f, 1f, 1f, 0.6f));

        // White selection outline.
        if (selected)
        {
            TimelineDraw.DrawRectBorder(
                new Rect(blockRect.x - 1f, blockRect.y - 1f, blockRect.width + 2f, blockRect.height + 2f),
                new Color(1f, 1f, 1f, 0.8f));
        }

        // Label (event name) — clipped to the block width.
        if (!string.IsNullOrEmpty(label))
        {
            var lr = new Rect(blockRect.x + 2f, blockRect.y + 2f, blockRect.width - 4f, blockRect.height - 4f);
            GUI.Label(lr, label, EditorStylesCache.TinyCenteredLabel);
        }
    }

    private void HandleTimelineInput(TimelineLayout layout, AnimationClip clip)
    {
        var e = Event.current;
        if (e == null) return;

        // timeToChangeAnim marker drag
        if (e.type == EventType.MouseDown && e.button == 0 && _timeToChangeHandleRect.Contains(e.mousePosition))
        {
            _dragTimeToChange = true;
            e.Use();
            return;
        }

        if (e.type == EventType.MouseDrag && e.button == 0 && _dragTimeToChange)
        {
            float t = layout.XToTime01(e.mousePosition.x);

            if (_cachedTimeToChangeProp != null)
            {
                BeginAttackEdit(UNDO_EDIT_TIME_CHANGE);
                _cachedTimeToChangeProp.floatValue = t;
                ApplyAndDirty();
                ScrubAndAutoPreview(t, clip);
            }

            e.Use();
            return;
        }

        if (e.type == EventType.MouseUp && e.button == 0 && _dragTimeToChange)
        {
            _dragTimeToChange = false;
            e.Use();
            return;
        }

        // Shift+Click => create first window type
        if (e.type == EventType.MouseDown && e.button == 0 && layout.TimelineContains(e.mousePosition) && e.shift)
        {
            var hit = TimelineHitTest.HitTest(layout, _blockVisuals, e.mousePosition, _preview.Scrubber01, false, 0f, 7f);
            if (hit.Type == TimelineHitType.TracksArea || hit.Type == TimelineHitType.Ruler || hit.Type == TimelineHitType.Scrubber)
            {
                ScrubAndAutoPreview(hit.Time01, clip);

                var firstMeta = _registry.GetAllOrdered().FirstOrDefault();
                if (firstMeta != null) AddWindow(firstMeta);

                e.Use();
                return;
            }
        }

        // Empty click clears selection
        if (e.type == EventType.MouseDown && e.button == 0 && layout.TimelineContains(e.mousePosition) && !e.shift && !e.control && !e.command)
        {
            var hit = TimelineHitTest.HitTest(layout, _blockVisuals, e.mousePosition, _preview.Scrubber01, false, 0f, 7f);
            if (hit.Type == TimelineHitType.TracksArea || hit.Type == TimelineHitType.Ruler || hit.Type == TimelineHitType.Scrubber)
            {
                _selection.Clear();
                _needsRepaint = true;
            }
        }

        _timelineDrag.EnableMarker = false;
        _timelineDrag.HandleEvent(
            e,
            layout,
            _blockVisuals,
            _selection,
            this,
            handleSnapPx: 7f,
            allowShiftMultiSelect: true,
            sampleOnScrub: true,
            sampleOnDrag: true,
            moveAllSelected: false
        );

        // Speed curve drags
        if (_speedCurveEditor.HandleSpeedDragEvent(
                e,
                _preview.Scrubber01,
                t01 => _attack.EvaluateAnimatorSpeed(t01),
                _so.FindProperty("animatorSpeedCurve"),
                () =>
                {
                    BeginAttackEdit("Record Speed Key");
                    ApplyAndDirty();
                }
            ))
            _needsRepaint = true;

        if (_rootMotionCurveEditor.HandleSpeedDragEvent(
                e,
                _preview.Scrubber01,
                t01 => _attack.EvaluateRootMotionMultiplier(t01),
                _so.FindProperty("rootMotionMultiplierCurve"),
                () =>
                {
                    BeginAttackEdit("Record Root Motion Key");
                    ApplyAndDirty();
                }
            ))
            _needsRepaint = true;

        if (_proceduralRootMotionCurveEditor.HandleSpeedDragEvent(
                e,
                _preview.Scrubber01,
                t01 => _attack.EvaluateProceduralForwardProgress01(t01),
                _so.FindProperty("proceduralForwardMoveCurve"),
                () =>
                {
                    BeginAttackEdit("Record Procedural Key");
                    ApplyAndDirty();
                }
            ))
            _needsRepaint = true;

        if (_needsRepaint)
        {
            _needsRepaint = false;
            Repaint();
        }
    }

    #endregion

    #region Window Lists

    private void DrawWindowsLists(AnimationClip clip)
    {
        EditorGUILayout.LabelField("Windows (Lists / Flags)", EditorStyles.boldLabel);

        var allMetadata = _registry.GetAllOrdered();
        for (int i = 0; i < allMetadata.Count; i++)
        {
            DrawWindowListForMetadata(allMetadata[i], clip);
            EditorGUILayout.Space(6);
        }
    }

    private void DrawWindowListForMetadata(WindowReflectionRegistry.WindowMetadata meta, AnimationClip clip)
    {
        switch (meta.StructureType)
        {
            case WindowStructureType.SpecialDamage:
                DrawDamageList(meta, clip);
                break;

            case WindowStructureType.SpecialVfx:
                DrawVfxList(meta, clip);
                break;

            case WindowStructureType.SpecialAudioTrigger:
                DrawAudioTriggerList(meta, clip);
                break;

            case WindowStructureType.SpecialAudioWindow:
                DrawAudioWindowList(meta, clip);
                break;

            case WindowStructureType.Single:
                DrawSingleWindow(meta, clip);
                break;

            case WindowStructureType.List:
                DrawSimpleList(meta, clip);
                break;
        }
    }

    private void DrawDamageList(WindowReflectionRegistry.WindowMetadata meta, AnimationClip clip)
    {
        _listInspector.DrawDamageList(
            _registry.GetListProperty(_so, meta),
            meta.FieldName,
            _selection,
            () => AddWindow(meta),
            () => ClearList(meta),
            (shift, idx) => HandleListSelection(shift, meta.FieldName, idx),
            idx => DeleteOne(meta.FieldName, idx),
            t => ScrubAndAutoPreview(t, clip)
        );
    }

    private void DrawVfxList(WindowReflectionRegistry.WindowMetadata meta, AnimationClip clip)
    {
        _listInspector.DrawVfxList(
            _registry.GetListProperty(_so, meta),
            meta.FieldName,
            _selection,
            () => AddWindow(meta),
            () => ClearList(meta),
            (shift, idx) => HandleListSelection(shift, meta.FieldName, idx),
            idx => DeleteOne(meta.FieldName, idx),
            t => ScrubAndAutoPreview(t, clip)
        );
    }

    private void DrawAudioTriggerList(WindowReflectionRegistry.WindowMetadata meta, AnimationClip clip)
    {
        _listInspector.DrawAudioTriggerList(
            _registry.GetListProperty(_so, meta),
            meta.FieldName,
            _selection,
            () => AddWindow(meta),
            () => ClearList(meta),
            (shift, idx) => HandleListSelection(shift, meta.FieldName, idx),
            idx => DeleteOne(meta.FieldName, idx),
            t => ScrubAndAutoPreview(t, clip)
        );
    }

    private void DrawAudioWindowList(WindowReflectionRegistry.WindowMetadata meta, AnimationClip clip)
    {
        _listInspector.DrawAudioWindowList(
            _registry.GetListProperty(_so, meta),
            meta.FieldName,
            _selection,
            () => AddWindow(meta),
            () => ClearList(meta),
            (shift, idx) => HandleListSelection(shift, meta.FieldName, idx),
            idx => DeleteOne(meta.FieldName, idx),
            t => ScrubAndAutoPreview(t, clip)
        );
    }

    private void DrawSingleWindow(WindowReflectionRegistry.WindowMetadata meta, AnimationClip clip)
    {
        // If there is an enable field, draw it and only show the window if enabled.
        SerializedProperty enableProp = null;
        bool enabled = true;

        if (meta.HasEnableField)
        {
            enableProp = _so.FindProperty(meta.EnableFieldName);
            EditorGUILayout.PropertyField(enableProp);
            enabled = enableProp != null && enableProp.boolValue;
        }

        if (!enabled) return;

        _listInspector.DrawSingleWindow(
            meta.DisplayName,
            meta.FieldName,
            0,
            _registry.GetSingleProperty(_so, meta),
            _selection,
            onDisable: () =>
            {
                BeginAttackEdit($"Disable {meta.DisplayName}");

                if (meta.HasEnableField && enableProp != null)
                {
                    enableProp.boolValue = false;
                }
                else
                {
                    // No enable field: reset window values.
                    var winProp = _registry.GetSingleProperty(_so, meta);
                    if (winProp != null)
                    {
                        var s = winProp.FindPropertyRelative("start");
                        var en = winProp.FindPropertyRelative("end");
                        if (s != null) s.floatValue = 0f;
                        if (en != null) en.floatValue = 0f;
                    }
                }

                RemoveSelectionByFieldName(meta.FieldName);
                ApplyAndDirty();
            },
            onSelectItem: (shift, idx) => HandleListSelection(shift, meta.FieldName, idx),
            onScrubToEnd: t => ScrubAndAutoPreview(t, clip)
        );
    }

    private void DrawSimpleList(WindowReflectionRegistry.WindowMetadata meta, AnimationClip clip)
    {
        // Lists in this project are typically gated by a bool.
        // If no enable field exists, treat it as always enabled.
        SerializedProperty enableProp = null;

        if (meta.HasEnableField)
        {
            enableProp = _so.FindProperty(meta.EnableFieldName);
        }

        if (enableProp != null)
        {
            _listInspector.DrawSimpleWindowList(
                meta.DisplayName,
                meta.FieldName,
                enableProp,
                _registry.GetListProperty(_so, meta),
                _selection,
                () => AddWindow(meta),
                () => ClearList(meta),
                (shift, idx) => HandleListSelection(shift, meta.FieldName, idx),
                idx => DeleteOne(meta.FieldName, idx),
                t => ScrubAndAutoPreview(t, clip)
            );

            return;
        }

        // No enable field: draw a "manual" version always enabled.
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(meta.DisplayName, EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+", GUILayout.Width(28)))
                AddWindow(meta);

            if (GUILayout.Button("Clear", GUILayout.Width(60)))
                ClearList(meta);
        }

        var listProp = _registry.GetListProperty(_so, meta);
        if (listProp == null || !listProp.isArray || listProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox($"No hay {meta.DisplayName}.", MessageType.Info);
            return;
        }

        for (int i = 0; i < listProp.arraySize; i++)
        {
            var elem = listProp.GetArrayElementAtIndex(i);
            var start = elem.FindPropertyRelative("start");
            var end = elem.FindPropertyRelative("end");

            float s = start != null ? Mathf.Clamp01(start.floatValue) : 0f;
            float en = end != null ? Mathf.Clamp01(end.floatValue) : 0f;

            bool selected = _selection.Contains(new SelectionKey { fieldName = meta.FieldName, index = i });

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button(selected ? "●" : "○", GUILayout.Width(24)))
                {
                    bool shift = Event.current.shift;
                    HandleListSelection(shift, meta.FieldName, i);
                    ScrubAndAutoPreview(en, clip);
                }

                GUILayout.Label($"{meta.DisplayName} {i} | [{s:0.00}-{en:0.00}]", GUILayout.ExpandWidth(true));

                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    DeleteOne(meta.FieldName, i);
                    break;
                }
            }
        }
    }

    #endregion

    #region Side Inspector

private void DrawSideInspector(AnimationClip clip)
{
    EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);

    // === ANIMATION PREVIEW SECTION ===
    EditorGUILayout.Space(6);
    EditorGUILayout.LabelField("Animation Preview", EditorStyles.boldLabel);
    
    // Preview display options - Two rows for better organization
    using (new EditorGUILayout.HorizontalScope())
    {
        _animPreview.ShowGrid = EditorGUILayout.ToggleLeft("Grid", _animPreview.ShowGrid, GUILayout.Width(60));
        _animPreview.ShowMotionTrail = EditorGUILayout.ToggleLeft("Trail", _animPreview.ShowMotionTrail, GUILayout.Width(60));
        _animPreview.ShowForwardDirection = EditorGUILayout.ToggleLeft("Forward", _animPreview.ShowForwardDirection, GUILayout.Width(80));
        _animPreview.ShowVelocityIndicator = EditorGUILayout.ToggleLeft("Speed", _animPreview.ShowVelocityIndicator, GUILayout.Width(70));
        _animPreview.ShowFrameInfo = EditorGUILayout.ToggleLeft("Frame Info", _animPreview.ShowFrameInfo, GUILayout.Width(90));
        
        GUILayout.FlexibleSpace();
        
        if (GUILayout.Button("Reset Camera", GUILayout.Width(100)))
        {
            _animPreview.ResetCamera();
        }
    }
    
    Rect previewRect = GUILayoutUtility.GetRect(10, 250, GUILayout.ExpandWidth(true));

    if (_animPreview.IsInitialized)
    {
        _animPreview.HandleInput(previewRect);
    }

    if (Event.current.type == EventType.Repaint)
    {
        if (_animPreview.IsInitialized && clip != null)
        {
            Texture previewTexture = _animPreview.RenderPreview(
                previewRect,
                clip,
                _preview.Scrubber01,
                t01 => _attack.EvaluateAnimatorSpeed(t01)
            );

            if (previewTexture != null)
                GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit, false);
        }
        else
        {
            EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.15f));
        }
    }
    
    // Preview controls hint
    if (_animPreview.IsInitialized && clip != null)
    {
        var hintRect = new Rect(previewRect.x + 4, previewRect.yMax - 18, previewRect.width - 8, 16);
        GUI.Label(hintRect, "Drag: Rotate | Scroll: Zoom", EditorStyles.miniLabel);
    }
    else
    {
        var hintRect = new Rect(previewRect.x + 4, previewRect.y + previewRect.height / 2 - 8, previewRect.width - 8, 16);
        string msg = clip == null ? "No animation clip" : "Player not found (tag 'Player')";
        GUI.Label(hintRect, msg, EditorStyles.centeredGreyMiniLabel);
    }

    EditorGUILayout.Space(10);
    
    // === SELECTION INSPECTOR (existing code) ===
    
    int selCount = _selection.Count;

    if (selCount == 0)
    {
        EditorGUILayout.HelpBox("Select a block (Shift to multi-select).", MessageType.Info);
        return;
    }

        if (selCount > 1)
        {
            EditorGUILayout.HelpBox($"{selCount} selected windows.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear")) _selection.Clear();

                GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                if (GUILayout.Button("Delete Selected"))
                {
                    DeleteSelection();
                    _selection.Clear();
                    Repaint();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Nudge (apply to all)", EditorStyles.miniBoldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("-0.01")) NudgeSelection(-0.01f, clip);
                if (GUILayout.Button("+0.01")) NudgeSelection(+0.01f, clip);
            }

            return;
        }

        var single = GetSingleSelection();
        if (!WindowPropertyAccessor.TryGetRange(_so, single, _registry, out var start, out var end))
        {
            EditorGUILayout.HelpBox("Invalid selection (maybe has been deleted).", MessageType.Warning);
            if (GUILayout.Button("Clear")) _selection.Clear();
            return;
        }

        var meta = _registry.GetMetadata(single.fieldName);
        if (meta == null) return;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"{meta.DisplayName}  #{single.index}", EditorStyles.miniBoldLabel);

        EditorGUI.BeginChangeCheck();

        if (meta.StructureType == WindowStructureType.SpecialAudioTrigger)
        {
            // AudioTrigger has a single point in time — show only one slider.
            // "start" and "end" both point to the same "triggerAt" SerializedProperty.
            float t = EditorGUILayout.Slider("Trigger At", Mathf.Clamp01(start.floatValue), 0f, 1f);

            if (EditorGUI.EndChangeCheck())
            {
                BeginAttackEdit("Edit Audio Trigger");
                start.floatValue = t; // writes triggerAt — end is the same prop, no need to write twice
                ApplyAndDirty();
                ScrubAndAutoPreview(t, clip);
            }
        }
        else
        {
            float s = EditorGUILayout.Slider("Start", Mathf.Clamp01(start.floatValue), 0f, 1f);
            float e = EditorGUILayout.Slider("End",   Mathf.Clamp01(end.floatValue),   0f, 1f);
            if (e < s) e = s;

            if (EditorGUI.EndChangeCheck())
            {
                BeginAttackEdit("Edit Window Values");
                start.floatValue = s;
                end.floatValue   = e;
                ApplyAndDirty();
                ScrubAndAutoPreview(e, clip);
            }
        }

        if (meta.StructureType == WindowStructureType.SpecialDamage)
        {
            EditorGUILayout.Space(6);
            var root = WindowPropertyAccessor.GetRoot(_so, single.fieldName, single.index, meta.StructureType);
            if (root != null)
            {
                var hand = root.FindPropertyRelative("weaponHand");
                var slot = root.FindPropertyRelative("slot");
                if (hand != null) EditorGUILayout.PropertyField(hand);
                if (slot != null) EditorGUILayout.PropertyField(slot);
            }
        }

        if (meta.StructureType == WindowStructureType.SpecialVfx)
        {
            EditorGUILayout.Space(6);
            var root = WindowPropertyAccessor.GetRoot(_so, single.fieldName, single.index, meta.StructureType);
            if (root != null)
            {
                var hand = root.FindPropertyRelative("hand");
                if (hand != null) EditorGUILayout.PropertyField(hand, new GUIContent("Weapon Hand"));
                
                var vfxId = root.FindPropertyRelative("vfxId");
                if (vfxId != null) EditorGUILayout.PropertyField(vfxId, new GUIContent("VFX Id"));

                var disableOnEnd = root.FindPropertyRelative("disableOnEnd");
                if (disableOnEnd != null)
                {
                    EditorGUILayout.PropertyField(disableOnEnd, new GUIContent("Disable On End"));
                    EditorGUILayout.HelpBox("On enabled, the VFX will be deactivated when the window closes.", MessageType.Info);
                }
            }
        }

        if (meta.StructureType == WindowStructureType.SpecialAudioTrigger)
        {
            EditorGUILayout.Space(6);
            var root = WindowPropertyAccessor.GetRoot(_so, single.fieldName, single.index, meta.StructureType);
            if (root != null)
            {
                // For AudioTrigger the start/end sliders above both move "triggerAt".
                // Show the FMOD EventReference field so the designer can assign the sound.
                var soundProp = root.FindPropertyRelative("sound");
                if (soundProp != null)
                    EditorGUILayout.PropertyField(soundProp, new GUIContent("FMOD Event"));

                EditorGUILayout.HelpBox(
                    "Fire-once SFX. The sound plays at the 'triggerAt' normalized time and FMOD manages its lifetime.",
                    MessageType.Info);
            }
        }

        if (meta.StructureType == WindowStructureType.SpecialAudioWindow)
        {
            EditorGUILayout.Space(6);
            var root = WindowPropertyAccessor.GetRoot(_so, single.fieldName, single.index, meta.StructureType);
            if (root != null)
            {
                var soundProp = root.FindPropertyRelative("sound");
                if (soundProp != null)
                    EditorGUILayout.PropertyField(soundProp, new GUIContent("FMOD Event"));

                var stopImmediate = root.FindPropertyRelative("stopImmediate");
                if (stopImmediate != null)
                {
                    EditorGUILayout.PropertyField(stopImmediate, new GUIContent("Stop Immediate"));
                    EditorGUILayout.HelpBox(
                        stopImmediate.boolValue
                            ? "Sound will cut immediately when the window closes."
                            : "Sound will fade out according to the FMOD event settings.",
                        MessageType.Info);
                }
            }
        }

        EditorGUILayout.Space(10);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scrub Start")) ScrubAndAutoPreview(Mathf.Clamp01(start.floatValue), clip);
            if (GUILayout.Button("Scrub End")) ScrubAndAutoPreview(Mathf.Clamp01(end.floatValue), clip);
        }

        EditorGUILayout.Space(10);

        GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
        if (GUILayout.Button("Delete Window"))
        {
            DeleteOne(single.fieldName, single.index);
            _selection.Clear();
        }
        GUI.backgroundColor = Color.white;
    }

    private SelectionKey GetSingleSelection()
    {
        if (_selection.HasPrimary) return _selection.Primary;

        if (_selection.Count == 1)
        {
            _tempSelectionList.Clear();
            _selection.CopyTo(_tempSelectionList);
            if (_tempSelectionList.Count > 0) return _tempSelectionList[0];
        }

        return default;
    }

    #endregion

    #region CRUD

    private void AddWindow(WindowReflectionRegistry.WindowMetadata meta)
    {
        BeginAttackEdit($"Add {meta.DisplayName}");

        if (meta.HasEnableField)
            _registry.SetEnabled(_so, meta, true);

        if (meta.StructureType == WindowStructureType.Single)
        {
            var windowProp = _registry.GetSingleProperty(_so, meta);
            SetDefaultWindow(windowProp.FindPropertyRelative("start"), windowProp.FindPropertyRelative("end"));
            _selection.SetSingle(new SelectionKey { fieldName = meta.FieldName, index = 0 });
        }
        else
        {
            var listProp = _registry.GetListProperty(_so, meta);
            int idx = listProp.arraySize;
            listProp.arraySize += 1;

            var elem = listProp.GetArrayElementAtIndex(idx);

            if (meta.StructureType == WindowStructureType.SpecialDamage)
            {
                var win = elem.FindPropertyRelative("window");
                if (win != null)
                    SetDefaultWindow(win.FindPropertyRelative("start"), win.FindPropertyRelative("end"));

                var hand = elem.FindPropertyRelative("weaponHand");
                if (hand != null) hand.enumValueIndex = 0;

                var slot = elem.FindPropertyRelative("slot");
                if (slot != null) slot.intValue = 0;
            }
            else if (meta.StructureType == WindowStructureType.SpecialVfx)
            {
                var vfxId = elem.FindPropertyRelative("vfxId");
                if (vfxId != null) vfxId.intValue = 0;
                
                var hand = elem.FindPropertyRelative("hand");
                if (hand != null) hand.enumValueIndex = 0; // Default to Right

                var win = elem.FindPropertyRelative("window");
                if (win != null)
                    SetDefaultWindow(win.FindPropertyRelative("start"), win.FindPropertyRelative("end"));
            }
            else if (meta.StructureType == WindowStructureType.SpecialAudioTrigger)
            {
                // AudioTrigger: set triggerAt to the current scrubber position.
                var triggerAt = elem.FindPropertyRelative("triggerAt");
                if (triggerAt != null)
                    triggerAt.floatValue = Mathf.Clamp01(_preview.Scrubber01);
                // "sound" EventReference defaults to empty — designer assigns it in the inspector.
            }
            else if (meta.StructureType == WindowStructureType.SpecialAudioWindow)
            {
                // AudioWindow: set window start/end using the same default as other windows.
                var win = elem.FindPropertyRelative("window");
                if (win != null)
                    SetDefaultWindow(win.FindPropertyRelative("start"), win.FindPropertyRelative("end"));
                // "sound" EventReference defaults to empty — designer assigns it in the inspector.
            }
            else
            {
                SetDefaultWindow(elem.FindPropertyRelative("start"), elem.FindPropertyRelative("end"));
            }

            _selection.SetSingle(new SelectionKey { fieldName = meta.FieldName, index = idx });
        }

        ApplyAndDirty();
    }

    private void ClearList(WindowReflectionRegistry.WindowMetadata meta)
    {
        BeginAttackEdit($"Clear {meta.DisplayName}");

        var listProp = _registry.GetListProperty(_so, meta);
        if (listProp != null && listProp.isArray)
            listProp.arraySize = 0;

        RemoveSelectionByFieldName(meta.FieldName);
        ApplyAndDirty();
    }

    private void SetDefaultWindow(SerializedProperty start, SerializedProperty end)
    {
        float len = DEFAULT_WIN_LEN;
        float s = Mathf.Clamp01(_preview.Scrubber01);
        float e = s + len;

        if (e > 1f)
        {
            e = 1f;
            s = Mathf.Max(0f, e - len);
        }

        if (e < s + _dragMinLen)
        {
            e = Mathf.Min(1f, s + _dragMinLen);
            if (e >= 1f) s = Mathf.Max(0f, e - _dragMinLen);
        }

        if (start != null) start.floatValue = s;
        if (end != null) end.floatValue = e;

        _preview.SetScrubber(e);
        _preview.EnsurePreviewActive();

        if (_attack != null && _attack.animationClip != null)
            Sample(_preview.Scrubber01, _attack.animationClip);
    }

    private void DeleteSelection()
    {
        _deleteByFieldNameCache.Clear();

        _tempSelectionList.Clear();
        _selection.CopyTo(_tempSelectionList);

        for (int i = 0; i < _tempSelectionList.Count; i++)
        {
            var key = _tempSelectionList[i];
            if (string.IsNullOrEmpty(key.fieldName)) continue;

            if (!_deleteByFieldNameCache.TryGetValue(key.fieldName, out var list))
            {
                list = new List<int>(8);
                _deleteByFieldNameCache[key.fieldName] = list;
            }

            list.Add(key.index);
        }

        foreach (var kv in _deleteByFieldNameCache)
        {
            kv.Value.Sort((a, b) => b.CompareTo(a));
            for (int i = 0; i < kv.Value.Count; i++)
                DeleteOne(kv.Key, kv.Value[i]);
        }
    }

    private void NudgeSelection(float delta, AnimationClip clip)
    {
        if (_attack == null || _so == null) return;

        BeginAttackEdit(UNDO_NUDGE);

        _tempSelectionList.Clear();
        _selection.CopyTo(_tempSelectionList);

        for (int i = 0; i < _tempSelectionList.Count; i++)
        {
            if (!WindowPropertyAccessor.TryGetRange(_so, _tempSelectionList[i], _registry, out var start, out var end))
                continue;

            float s = Mathf.Clamp01(start.floatValue + delta);
            float e = Mathf.Clamp01(end.floatValue + delta);

            if (e < s + _dragMinLen) e = Mathf.Min(1f, s + _dragMinLen);
            if (e >= 1f && (e - s) < _dragMinLen) s = Mathf.Max(0f, e - _dragMinLen);

            start.floatValue = s;
            end.floatValue = e;
        }

        ApplyAndDirty();
        ScrubAndAutoPreview(Mathf.Clamp01(_preview.Scrubber01 + delta), clip);
    }

    private void DeleteOne(string fieldName, int index)
    {
        if (_attack == null || _so == null) return;

        BeginAttackEdit(UNDO_DELETE_WINDOW);

        var meta = _registry.GetMetadata(fieldName);
        if (meta == null) return;

        if (meta.StructureType == WindowStructureType.Single)
        {
            if (meta.HasEnableField)
            {
                _registry.SetEnabled(_so, meta, false);
                RemoveSelectionByFieldName(fieldName);
            }
            else
            {
                // No enable field: reset values.
                var winProp = _registry.GetSingleProperty(_so, meta);
                if (winProp != null)
                {
                    var s = winProp.FindPropertyRelative("start");
                    var e = winProp.FindPropertyRelative("end");
                    if (s != null) s.floatValue = 0f;
                    if (e != null) e.floatValue = 0f;
                }

                RemoveSelectionByFieldName(fieldName);
            }
        }
        else
        {
            var listProp = _registry.GetListProperty(_so, meta);
            DeleteArrayElementSafe(listProp, index);
            AdjustSelectionAfterDelete(fieldName, index);
        }

        ApplyAndDirty();
        GUI.FocusControl(null);
        Repaint();
    }

    private void DeleteArrayElementSafe(SerializedProperty arrayProp, int index)
    {
        if (arrayProp == null || !arrayProp.isArray) return;
        if (index < 0 || index >= arrayProp.arraySize) return;

        arrayProp.DeleteArrayElementAtIndex(index);

        if (index >= arrayProp.arraySize) return;

        var elem = arrayProp.GetArrayElementAtIndex(index);
        if (elem != null &&
            elem.propertyType == SerializedPropertyType.ObjectReference &&
            elem.objectReferenceValue != null)
        {
            arrayProp.DeleteArrayElementAtIndex(index);
        }
    }

    private void AdjustSelectionAfterDelete(string fieldName, int deletedIndex)
    {
        if (_selection.Count == 0) return;

        var newSel = new HashSet<SelectionKey>();

        _tempSelectionList.Clear();
        _selection.CopyTo(_tempSelectionList);

        for (int i = 0; i < _tempSelectionList.Count; i++)
        {
            var k = _tempSelectionList[i];

            if (k.fieldName != fieldName)
            {
                newSel.Add(k);
                continue;
            }

            if (k.index == deletedIndex) continue;

            newSel.Add(k.index > deletedIndex
                ? new SelectionKey { fieldName = fieldName, index = k.index - 1 }
                : k);
        }

        _selection.Clear();
        foreach (var s in newSel) _selection.Add(s);
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

    private void RemoveSelectionByFieldName(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return;

        _tempSelectionList.Clear();
        _selection.CopyTo(_tempSelectionList);

        for (int i = 0; i < _tempSelectionList.Count; i++)
        {
            if (_tempSelectionList[i].fieldName == fieldName)
                _selection.Remove(_tempSelectionList[i]);
        }
    }

    #endregion

    #region Preview & Sampling

    private GameObject GetOrFindPlayer()
    {
        if (_playerGO != null && _playerGO.GetInstanceID() == _playerInstanceID) return _playerGO;

        _playerGO = GameObject.FindGameObjectWithTag("Player");
        _playerInstanceID = _playerGO != null ? _playerGO.GetInstanceID() : -1;

        return _playerGO;
    }

    private void Sample(float norm01, AnimationClip clip)
    {
        var player = GetOrFindPlayer();
        if (player == null || clip == null) return;

        float tSec = Mathf.Clamp01(norm01) * clip.length;

        AnimationMode.SampleAnimationClip(player, clip, tSec);

        _blendPreview.UpdatePreview(player, clip, norm01, _attack.timeToChangeAnim);

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

    private void SetAttack(AttackData a)
    {
        _attack = a;
        _so = _attack != null ? new SerializedObject(_attack) : null;

        _cachedClipProp = null;
        _cachedTimeToChangeProp = null;

        _selection.Clear();

        _speedCurveEditor.Reset();
        _rootMotionCurveEditor.Reset();
        _proceduralRootMotionCurveEditor.Reset();

        _blendPreview.NextAttack = null;

        _preview.StopPlaying();
        var player = GetOrFindPlayer();
        if (player != null)
        {
            _animPreview.Initialize(player);
        }
    
        Repaint();
    }

    private void BeginAttackEdit(string undoLabel)
    {
        if (_attack == null) return;
        Undo.RecordObject(_attack, undoLabel);
    }

    private void ApplyAndDirty()
    {
        _so.ApplyModifiedProperties();
        EditorUtility.SetDirty(_attack);
    }

    #endregion

    #region ITimelineDragCallbacks

    float ITimelineDragCallbacks<SelectionKey>.GetScrubber01() => _preview.Scrubber01;

    void ITimelineDragCallbacks<SelectionKey>.SetScrubber01(float t01, bool sampleNow)
    {
        _preview.SetScrubber(t01);

        if (!sampleNow || _attack == null || _attack.animationClip == null) return;

        _preview.EnsurePreviewActive();
        Sample(_preview.Scrubber01, _attack.animationClip);
    }

    bool ITimelineDragCallbacks<SelectionKey>.TryGetRange01(SelectionKey id, out float start01, out float end01)
    {
        start01 = 0f;
        end01 = 0f;

        if (!WindowPropertyAccessor.TryGetRange(_so, id, _registry, out var s, out var en)) return false;

        float a = Mathf.Clamp01(s.floatValue);
        float b = Mathf.Clamp01(en.floatValue);
        if (b < a) b = a;

        start01 = a;
        end01 = b;
        return true;
    }

    void ITimelineDragCallbacks<SelectionKey>.SetRange01(SelectionKey id, float start01, float end01, bool recordUndo)
    {
        if (!WindowPropertyAccessor.TryGetRange(_so, id, _registry, out var s, out var en)) return;

        start01 = Mathf.Clamp01(start01);
        end01 = Mathf.Clamp01(end01);
        if (end01 < start01) end01 = start01;

        if (recordUndo) BeginAttackEdit(UNDO_EDIT_WINDOW);

        s.floatValue = start01;
        en.floatValue = end01;

        ApplyAndDirty();
    }

    float ITimelineDragCallbacks<SelectionKey>.MinDuration01(SelectionKey id)
    {
        // AudioTrigger is a point in time — zero minimum duration.
        // Move drag writes start = end = triggerAt, resize is disabled anyway.
        return IsAudioTrigger(id) ? 0f : _dragMinLen;
    }

    bool ITimelineDragCallbacks<SelectionKey>.CanResize(SelectionKey id)
    {
        // AudioTrigger has no duration — only Move is allowed, not ResizeL/ResizeR.
        return !IsAudioTrigger(id);
    }

    void ITimelineDragCallbacks<SelectionKey>.RequestRepaint() => _needsRepaint = true;

    bool ITimelineDragCallbacks<SelectionKey>.ShouldRecordUndo(Event e) => true;

    // Returns true if the given selection key refers to an AudioTrigger field.
    // AudioTrigger start and end props point to the same SerializedProperty ("triggerAt"),
    // so their values are always equal. We detect this by checking the StructureType.
    private bool IsAudioTrigger(SelectionKey id)
    {
        if (string.IsNullOrEmpty(id.fieldName)) return false;
        var meta = _registry.GetMetadata(id.fieldName);
        return meta != null && meta.StructureType == WindowStructureType.SpecialAudioTrigger;
    }

    #endregion
}
