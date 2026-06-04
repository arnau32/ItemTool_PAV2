using System.Collections.Generic;
using UnityEngine;

internal interface ITimelineDragCallbacks<TId>
{
    float GetScrubber01();
    void SetScrubber01(float t01, bool sampleNow);

    bool TryGetRange01(TId id, out float start01, out float end01);
    void SetRange01(TId id, float start01, float end01, bool recordUndo);

    float MinDuration01(TId id);
    bool CanResize(TId id);
    void RequestRepaint();

    bool ShouldRecordUndo(Event e);
}

internal sealed class TimelineDragController<TId>
{
    #region Fields

    private EditorEnums.TimelineDragMode _mode = EditorEnums.TimelineDragMode.None;

    private TId _activeId;
    private bool _hasActiveId;

    private float _mouseDownX;

    private float _start01;
    private float _end01;

    private int _hotControl;

    private bool _undoRecordedThisInteraction;

    private struct Range01
    {
        public float s;
        public float e;
    }

    private readonly Dictionary<TId, Range01> _dragStartRanges = new Dictionary<TId, Range01>(64);
    private readonly List<TId> _tmpSelected = new List<TId>(64);
    private readonly List<TId> _dragIds = new List<TId>(64);

    #endregion

    #region Properties

    public EditorEnums.TimelineDragMode Mode => _mode;
    public bool IsInteracting => _mode != EditorEnums.TimelineDragMode.None;

    public bool EnableMarker { get; set; }
    public float Marker01 { get; private set; }

    #endregion

    #region Public API

    public void SetMarker01(float t01) => Marker01 = Mathf.Clamp01(t01);

    public void Reset()
    {
        _mode = EditorEnums.TimelineDragMode.None;
        _hasActiveId = false;
        _activeId = default(TId);
        _hotControl = 0;
        _undoRecordedThisInteraction = false;

        _dragStartRanges.Clear();
        _dragIds.Clear();
        _tmpSelected.Clear();
    }

    public void HandleEvent(
        Event e,
        TimelineLayout layout,
        List<TimelineBlockVisual<TId>> blockVisuals,
        SelectionModel<TId> selection,
        ITimelineDragCallbacks<TId> cb,
        float handleSnapPx = 7f,
        bool allowShiftMultiSelect = true,
        bool sampleOnScrub = true,
        bool sampleOnDrag = true,
        bool moveAllSelected = true)
    {
        if (e == null || cb == null) return;

        bool insideTimeline = layout.TimelineContains(e.mousePosition);

        if (e.type == EventType.MouseDown && e.button == 0 && insideTimeline)
        {
            var hit = TimelineHitTest.HitTest(
                layout,
                blockVisuals,
                e.mousePosition,
                cb.GetScrubber01(),
                EnableMarker,
                Marker01,
                handleSnapPx);

            _mouseDownX = e.mousePosition.x;

            _hotControl = GUIUtility.GetControlID(FocusType.Passive);
            GUIUtility.hotControl = _hotControl;

            if (hit.Type == TimelineHitType.Marker && EnableMarker)
            {
                _mode = EditorEnums.TimelineDragMode.Marker;
                _undoRecordedThisInteraction = false;
                e.Use();
                cb.RequestRepaint();
                return;
            }

            if (hit.Type == TimelineHitType.Scrubber || hit.Type == TimelineHitType.Ruler || hit.Type == TimelineHitType.TracksArea)
            {
                _mode = EditorEnums.TimelineDragMode.Scrub;
                _undoRecordedThisInteraction = false;
                cb.SetScrubber01(hit.Time01, sampleOnScrub);
                e.Use();
                cb.RequestRepaint();
                return;
            }

            if (hit.Type == TimelineHitType.BlockBody || hit.Type == TimelineHitType.BlockHandleL || hit.Type == TimelineHitType.BlockHandleR)
            {
                _activeId = hit.Id;
                _hasActiveId = true;
                _undoRecordedThisInteraction = false;

                bool multi = allowShiftMultiSelect && (e.shift || e.control || e.command);

                if (multi)
                {
                    selection.Toggle(_activeId);
                    selection.SetPrimary(_activeId);
                }
                else
                {
                    if (!selection.Contains(_activeId)) selection.SetSingle(_activeId);
                    else selection.SetPrimary(_activeId);
                }

                bool canResize = cb.CanResize(_activeId);

                if (hit.Type == TimelineHitType.BlockHandleL && canResize) _mode = EditorEnums.TimelineDragMode.ResizeL;
                else if (hit.Type == TimelineHitType.BlockHandleR && canResize) _mode = EditorEnums.TimelineDragMode.ResizeR;
                else _mode = EditorEnums.TimelineDragMode.Move;

                CacheDragStartRanges(selection, cb, moveAllSelected);

                e.Use();
                cb.RequestRepaint();
                return;
            }

            _mode = EditorEnums.TimelineDragMode.None;
            e.Use();
            cb.RequestRepaint();
            return;
        }

        if (e.type == EventType.MouseDrag && e.button == 0)
        {
            if (GUIUtility.hotControl != _hotControl) return;

            if (_mode == EditorEnums.TimelineDragMode.Scrub)
            {
                float t01 = layout.XToTime01(e.mousePosition.x);
                cb.SetScrubber01(t01, sampleOnScrub);
                e.Use();
                cb.RequestRepaint();
                return;
            }

            if (_mode == EditorEnums.TimelineDragMode.Marker && EnableMarker)
            {
                Marker01 = layout.XToTime01(e.mousePosition.x);
                e.Use();
                cb.RequestRepaint();
                return;
            }

            if ((_mode == EditorEnums.TimelineDragMode.Move || _mode == EditorEnums.TimelineDragMode.ResizeL || _mode == EditorEnums.TimelineDragMode.ResizeR) && _hasActiveId)
            {
                float dx = e.mousePosition.x - _mouseDownX;
                float dt01 = (layout.ContentWidth <= 1f) ? 0f : dx / layout.ContentWidth;

                bool recordUndo = !_undoRecordedThisInteraction && cb.ShouldRecordUndo(e);

                if (_mode == EditorEnums.TimelineDragMode.Move)
                {
                    ApplyMove(dt01, selection, cb, recordUndo, moveAllSelected);
                    if (recordUndo) _undoRecordedThisInteraction = true;

                    if (sampleOnDrag)
                    {
                        TId followId = selection.HasPrimary ? selection.Primary : _activeId;
                        if (_dragStartRanges.TryGetValue(followId, out var r0))
                        {
                            float len = Mathf.Max(0f, r0.e - r0.s);
                            float ns = Mathf.Clamp01(r0.s + dt01);
                            float ne = ns + len;
                            if (ne > 1f)
                            {
                                ne = 1f;
                                ns = Mathf.Max(0f, ne - len);
                            }

                            cb.SetScrubber01(ns, true);
                        }
                    }

                    e.Use();
                    cb.RequestRepaint();
                    return;
                }

                TId targetId = selection.HasPrimary ? selection.Primary : _activeId;
                if (!_dragStartRanges.TryGetValue(targetId, out var baseRange)) return;

                float minDur = Mathf.Max(0.0001f, cb.MinDuration01(targetId));

                if (_mode == EditorEnums.TimelineDragMode.ResizeL)
                {
                    float ns = Mathf.Clamp01(_start01 + dt01);
                    float ne = baseRange.e;

                    if (ne - ns < minDur) ns = ne - minDur;
                    ns = Mathf.Clamp01(ns);
                    if (ns > ne - minDur) ns = Mathf.Max(0f, ne - minDur);

                    cb.SetRange01(targetId, ns, ne, recordUndo);
                    if (recordUndo) _undoRecordedThisInteraction = true;

                    if (sampleOnDrag) cb.SetScrubber01(ns, true);

                    e.Use();
                    cb.RequestRepaint();
                    return;
                }

                if (_mode == EditorEnums.TimelineDragMode.ResizeR)
                {
                    float ns = baseRange.s;
                    float ne = Mathf.Clamp01(_end01 + dt01);

                    if (ne - ns < minDur) ne = ns + minDur;
                    ne = Mathf.Clamp01(ne);
                    if (ne < ns + minDur) ne = Mathf.Min(1f, ns + minDur);

                    cb.SetRange01(targetId, ns, ne, recordUndo);
                    if (recordUndo) _undoRecordedThisInteraction = true;

                    if (sampleOnDrag) cb.SetScrubber01(ne, true);

                    e.Use();
                    cb.RequestRepaint();
                    return;
                }
            }

            return;
        }

        if (e.type == EventType.MouseUp && e.button == 0)
        {
            // Only consume MouseUp if this controller owns the hot control.
            if (_hotControl == 0 || GUIUtility.hotControl != _hotControl) return;

            GUIUtility.hotControl = 0;
            _hotControl = 0;

            _hasActiveId = false;
            _activeId = default(TId);
            _mode = EditorEnums.TimelineDragMode.None;
            _undoRecordedThisInteraction = false;

            _dragStartRanges.Clear();
            _dragIds.Clear();
            _tmpSelected.Clear();

            e.Use();
            cb.RequestRepaint();
        }
    }

    #endregion

    #region Internal

    private void CacheDragStartRanges(SelectionModel<TId> selection, ITimelineDragCallbacks<TId> cb, bool moveAllSelected)
    {
        _dragStartRanges.Clear();
        _dragIds.Clear();

        TId primaryId = selection.HasPrimary ? selection.Primary : _activeId;
        if (cb.TryGetRange01(primaryId, out var s0, out var e0))
        {
            s0 = Mathf.Clamp01(s0);
            e0 = Mathf.Clamp01(e0);
            if (e0 < s0) e0 = s0;

            _start01 = s0;
            _end01 = e0;

            _dragStartRanges[primaryId] = new Range01 { s = s0, e = e0 };
            _dragIds.Add(primaryId);
        }

        if (!moveAllSelected) return;

        _tmpSelected.Clear();
        selection.CopyTo(_tmpSelected);

        for (int i = 0; i < _tmpSelected.Count; i++)
        {
            var id = _tmpSelected[i];
            if (_dragStartRanges.ContainsKey(id)) continue;

            if (!cb.TryGetRange01(id, out var s, out var e)) continue;

            s = Mathf.Clamp01(s);
            e = Mathf.Clamp01(e);
            if (e < s) e = s;

            _dragStartRanges[id] = new Range01 { s = s, e = e };
            _dragIds.Add(id);
        }
    }

    private void ApplyMove(float dt01, SelectionModel<TId> selection, ITimelineDragCallbacks<TId> cb, bool recordUndo, bool moveAllSelected)
    {
        if (moveAllSelected)
        {
            for (int i = 0; i < _dragIds.Count; i++)
            {
                var id = _dragIds[i];
                if (!_dragStartRanges.TryGetValue(id, out var r0)) continue;

                float len = Mathf.Max(0f, r0.e - r0.s);
                float ns = Mathf.Clamp01(r0.s + dt01);
                float ne = ns + len;

                if (ne > 1f)
                {
                    ne = 1f;
                    ns = Mathf.Max(0f, ne - len);
                }

                cb.SetRange01(id, ns, ne, recordUndo);
            }

            return;
        }

        var targetId = selection.HasPrimary ? selection.Primary : _activeId;
        if (!_dragStartRanges.TryGetValue(targetId, out var baseRange)) return;

        float baseLen = Mathf.Max(0f, baseRange.e - baseRange.s);
        float nS = Mathf.Clamp01(baseRange.s + dt01);
        float nE = nS + baseLen;

        if (nE > 1f)
        {
            nE = 1f;
            nS = Mathf.Max(0f, nE - baseLen);
        }

        cb.SetRange01(targetId, nS, nE, recordUndo);
    }

    #endregion
}
