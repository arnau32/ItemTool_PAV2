using System;
using System.Collections.Generic;

public sealed class WindowSequenceExecutor
{
    private WindowEvent[] _windows;
    private int _count;
    private int _index;

    public bool Active { get; private set; }

    public void Bind(List<WindowEvent> windows)
    {
        Reset();

        if (windows == null || windows.Count == 0) return;

        EnsureCapacity(windows.Count);
        _count = windows.Count;

        for (int i = 0; i < _count; i++)
        {
            _windows[i] = windows[i];
        }

        Array.Sort(_windows, 0, _count, WindowEventStartComparer.Instance);
    }

    public void Reset()
    {
        _count = 0;
        _index = 0;
        Active = false;
    }

    public void Tick(float tNorm)
    {
        int count = _count;
        if (count == 0)
        {
            Active = false;
            return;
        }

        var windows = _windows;
        int idx = _index;

        while (idx < count && tNorm > windows[idx].end)
        {
            idx++;
        }

        _index = idx;

        if (idx >= count)
        {
            Active = false;
            return;
        }

        var w = windows[idx];
        Active = tNorm >= w.start && tNorm <= w.end;
    }

    private void EnsureCapacity(int required)
    {
        if (_windows == null || _windows.Length < required)
        {
            _windows = new WindowEvent[Math.Max(required, 4)];
        }
    }

    private sealed class WindowEventStartComparer : IComparer<WindowEvent>
    {
        public static readonly WindowEventStartComparer Instance = new WindowEventStartComparer();

        public int Compare(WindowEvent a, WindowEvent b)
        {
            int c = a.start.CompareTo(b.start);
            if (c != 0) return c;
            return a.end.CompareTo(b.end);
        }
    }
}