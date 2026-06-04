using System.Collections.Generic;
using UnityEngine;

internal enum TimelineHitType
{
    None,
    Ruler,
    TracksArea,
    Scrubber,
    Marker,
    BlockBody,
    BlockHandleL,
    BlockHandleR
}

internal readonly struct TimelineHitResult<TId>
{
    // Unified hit test result. Values are valid depending on Type.
    public readonly TimelineHitType Type;
    public readonly TId Id;
    public readonly int TrackIndex;
    public readonly float Time01;

    public TimelineHitResult(TimelineHitType type, TId id, int trackIndex, float time01)
    {
        Type = type;
        Id = id;
        TrackIndex = trackIndex;
        Time01 = time01;
    }
}

internal readonly struct TimelineBlockVisual<TId>
{
    // Visual-only data used for hit-testing. Keep it frame-local.
    public readonly TId Id;
    public readonly int TrackIndex;
    public readonly Rect Rect;
    public readonly Rect HandleL;
    public readonly Rect HandleR;

    public TimelineBlockVisual(TId id, int trackIndex, Rect rect, float handleWidthPx)
    {
        Id = id;
        TrackIndex = trackIndex;
        Rect = rect;

        float hw = Mathf.Max(2f, handleWidthPx);
        HandleL = new Rect(rect.x, rect.y, hw, rect.height);
        HandleR = new Rect(rect.xMax - hw, rect.y, hw, rect.height);
    }
}

internal static class TimelineHitTest
{
    // Hit testing helpers for timeline UI.

    public static TimelineHitResult<TId> HitTest<TId>(TimelineLayout layout, List<TimelineBlockVisual<TId>> blocks, Vector2 mousePos,
        float scrubber01, bool enableMarker, float marker01, float markerSnapPx = 5f)
    {
        // Marker line hit (optional)
        if (enableMarker)
        {
            float mx = layout.Time01ToX(marker01);
            if (Mathf.Abs(mousePos.x - mx) <= markerSnapPx && layout.TimelineContains(mousePos))
            {
                return new TimelineHitResult<TId>(TimelineHitType.Marker, default(TId), -1, marker01);
            }
        }

        // Scrubber line hit
        {
            float sx = layout.Time01ToX(scrubber01);
            if (Mathf.Abs(mousePos.x - sx) <= markerSnapPx && layout.TimelineContains(mousePos))
            {
                return new TimelineHitResult<TId>(TimelineHitType.Scrubber, default(TId), -1, scrubber01);
            }
        }

        // Blocks hit (handles first)
        for (int i = blocks.Count - 1; i >= 0; i--)
        {
            var b = blocks[i];
            if (!b.Rect.Contains(mousePos)) continue;

            if (b.HandleL.Contains(mousePos))
            {
                return new TimelineHitResult<TId>(TimelineHitType.BlockHandleL, b.Id, b.TrackIndex, layout.XToTime01(mousePos.x));
                
            }

            if (b.HandleR.Contains(mousePos))
            {
                return new TimelineHitResult<TId>(TimelineHitType.BlockHandleR, b.Id, b.TrackIndex, layout.XToTime01(mousePos.x));
            }

            return new TimelineHitResult<TId>(TimelineHitType.BlockBody, b.Id, b.TrackIndex, layout.XToTime01(mousePos.x));
        }

        // Ruler or Tracks area
        if (layout.RulerContains(mousePos))
        {
            return new TimelineHitResult<TId>(TimelineHitType.Ruler, default(TId), -1, layout.XToTime01(mousePos.x));
        }

        if (layout.TracksContain(mousePos))
        {
            return new TimelineHitResult<TId>(TimelineHitType.TracksArea, default(TId), -1, layout.XToTime01(mousePos.x));
        }

        return new TimelineHitResult<TId>(TimelineHitType.None, default(TId), -1, 0f);
    }
}
