using UnityEngine;

internal readonly struct TimelineLayout
{
    // This struct contains all layout math and coordinate transforms.

    public readonly Rect TotalRect;
    public readonly float HeaderWidth;
    public readonly float RulerHeight;
    public readonly float TrackHeight;
    public readonly float TrackSpacing;
    public readonly int TrackCount;
    public readonly float InnerPadding;

    public TimelineLayout(Rect totalRect, int trackCount, float headerWidth = 150f, float rulerHeight = 22f, float trackHeight = 26f, float trackSpacing = 2f, float innerPadding = 6f)
    {
        TotalRect = totalRect;
        TrackCount = Mathf.Max(0, trackCount);
        HeaderWidth = Mathf.Max(0f, headerWidth);
        RulerHeight = Mathf.Max(0f, rulerHeight);
        TrackHeight = Mathf.Max(1f, trackHeight);
        TrackSpacing = Mathf.Max(0f, trackSpacing);
        InnerPadding = Mathf.Max(0f, innerPadding);
    }

    public Rect HeaderRect => new Rect(TotalRect.x, TotalRect.y, Mathf.Min(HeaderWidth, TotalRect.width), TotalRect.height);

    public Rect TimelineRect
    {
        get
        {
            var x = TotalRect.x + HeaderRect.width;
            var w = Mathf.Max(0f, TotalRect.width - HeaderRect.width);
            return new Rect(x, TotalRect.y, w, TotalRect.height);
        }
    }

    public Rect RulerRect
    {
        get
        {
            Rect t = TimelineRect;
            return new Rect(t.x, t.y, t.width, Mathf.Min(RulerHeight, t.height));
        }
    }

    public Rect TracksRect
    {
        get
        {
            Rect t = TimelineRect;
            float y = t.y + RulerRect.height;
            float h = Mathf.Max(0f, t.height - RulerRect.height);
            return new Rect(t.x, y, t.width, h);
        }
    }

    public float ContentXMin => TracksRect.x + InnerPadding;
    public float ContentXMax => TracksRect.xMax - InnerPadding;
    public float ContentWidth => Mathf.Max(1f, ContentXMax - ContentXMin);

    public float TrackTopY(int trackIndex)
    {
        float baseY = TracksRect.y + InnerPadding;
        return baseY + trackIndex * (TrackHeight + TrackSpacing);
    }

    public Rect GetTrackRect(int trackIndex)
    {
        float y = TrackTopY(trackIndex);
        return new Rect(TracksRect.x, y, TracksRect.width, TrackHeight);
    }

    public Rect GetTrackHeaderRect(int trackIndex)
    {
        float y = TrackTopY(trackIndex);
        Rect h = HeaderRect;
        return new Rect(h.x, y, h.width, TrackHeight);
    }

    // Time transforms are normalized (0..1) to remain independent of clip length.
    public float Time01ToX(float t01)
    {
        t01 = Mathf.Clamp01(t01);
        return Mathf.Lerp(ContentXMin, ContentXMax, t01);
    }

    public float XToTime01(float x)
    {
        float t = Mathf.InverseLerp(ContentXMin, ContentXMax, x);
        return Mathf.Clamp01(t);
    }

    public Rect Time01ToRect(float start01, float end01, Rect trackRect, float minPxWidth = 2f)
    {
        float a = Time01ToX(start01);
        float b = Time01ToX(end01);
        if (b < a) (a, b) = (b, a);

        float w = Mathf.Max(minPxWidth, b - a);
        return new Rect(a, trackRect.y, w, trackRect.height);
    }

    public bool TracksContain(Vector2 mousePos) => TracksRect.Contains(mousePos);
    public bool TimelineContains(Vector2 mousePos) => TimelineRect.Contains(mousePos);
    public bool RulerContains(Vector2 mousePos) => RulerRect.Contains(mousePos);

    public float TotalTracksHeight
    {
        get
        {
            if (TrackCount <= 0) return 0f;
            return TrackCount * TrackHeight + (TrackCount - 1) * TrackSpacing + InnerPadding * 2f;
        }
    }
}
