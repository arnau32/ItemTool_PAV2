using System.Collections.Generic;
using UnityEngine;

public sealed class FastWindowTrackerSimple
{
    private float[] _starts;
    private float[] _ends;
    private int _count;

    public bool IsActive { get; private set; }

    public void Bind(IReadOnlyList<WindowEvent> windows)
    {
        Clear();

        if (windows == null || windows.Count == 0) return;

        _count = windows.Count;
        EnsureCapacity(_count);

        for (int i = 0; i < _count; i++)
        {
            _starts[i] = windows[i].start;
            _ends[i] = windows[i].end;
        }
    }

    public void Tick(float tNorm)
    {
        // Early exit optimization - stop as soon as we find one active window
        var anyActive = false;
        for (int i = 0; i < _count; i++)
        {
            if (!(tNorm >= _starts[i]) || !(tNorm <= _ends[i])) continue;

            anyActive = true;
            break;
        }
        IsActive = anyActive;
    }

    public void Clear()
    {
        _count = 0;
        IsActive = false;
    }

    private void EnsureCapacity(int required)
    {
        if (_starts == null || _starts.Length < required)
        {
            int cap = Mathf.Max(required, 4);
            _starts = new float[cap];
            _ends = new float[cap];
        }
    }
}

public interface IWindowHandler<TWindow>
{
    WindowEvent GetEvent(in TWindow window);
    void OnEnter(in TWindow window, int index);
    void OnExit(in TWindow window, int index);
}

/// Optimized window executor with bitmasking for up to 64 windows.
public sealed class FastWindowExecutor64<TWindow, THandler>
    where THandler : struct, IWindowHandler<TWindow>
{
    private const int MaxWindows = 64;

    private TWindow[] _windows;
    private float[] _starts;
    private float[] _ends;

    // Bitmasking for active state tracking
    private ulong _activeBits;

    // When a window is detected as "skipped" (tNorm jumped over start→end in one frame),
    // its bit is set here. The NEXT Tick that would normally call OnExit instead consumes
    // the hold bit and keeps the window active for one extra tick, giving FixedUpdate at
    // least one more physics step to detect the collision via OnTriggerEnter/Stay.
    private ulong _skipHoldBits;

    private int _count;

    // Previous normalized time — used to detect frames that jump over an entire window.
    private float _prevNorm;

    private THandler _handler;

    public FastWindowExecutor64(THandler handler)
    {
        _handler = handler;
        _prevNorm = -1f;
        EnsureCapacity(8);
    }

    public ref THandler Handler => ref _handler;

    public void Bind(IReadOnlyList<TWindow> windows)
    {
        Clear();

        if (windows == null || windows.Count == 0) return;

        int count = windows.Count;

#if UNITY_EDITOR
        if (count > MaxWindows)
        {
            Debug.LogError($"[FastWindowExecutor64] Window count ({count}) exceeds limit ({MaxWindows}). Reduce windows or use FastWindowExecutor128.");
            count = MaxWindows;
        }
#else
        count = Mathf.Min(count, MaxWindows);
#endif

        _count = count;
        EnsureCapacity(count);

        // Cache window data for fast access
        for (int i = 0; i < count; i++)
        {
            TWindow w = windows[i];
            _windows[i] = w;

            WindowEvent ev = _handler.GetEvent(in w);
            _starts[i] = ev.start;
            _ends[i] = ev.end;
        }

        _activeBits   = 0UL;
        _skipHoldBits = 0UL;

        // Treat attack as starting from tNorm=0 so the skip-detection logic
        // can catch windows that are jumped over on the very first Tick.
        // With _prevNorm=-1f (from Clear) the guard "prev >= 0f" would block it.
        _prevNorm = 0f;
    }

    public void Tick(float normalizedTime)
    {
        int count = _count;
        if (count == 0) return;

        var windows = _windows;
        var starts = _starts;
        var ends = _ends;
        ulong bits      = _activeBits;
        ulong holdBits  = _skipHoldBits;
        float prev = _prevNorm;

        for (int i = 0; i < count; i++)
        {
            bool isActive = normalizedTime >= starts[i] && normalizedTime <= ends[i];
            ulong mask = 1UL << i;
            bool wasActive = (bits & mask) != 0UL;

            if (isActive != wasActive)
            {
                if (isActive)
                {
                    bits      |=  mask;
                    holdBits  &= ~mask; // clear any stale hold on natural re-enter
                    _handler.OnEnter(in windows[i], i);
                }
                else
                {
                    // Skip-hold: consume the hold token and keep the window active for
                    // one extra tick so FixedUpdate has a chance to run TryDealDamage.
                    if ((holdBits & mask) != 0UL)
                    {
                        holdBits &= ~mask;
                    }
                    else
                    {
                        bits &= ~mask;
                        _handler.OnExit(in windows[i], i);
                    }
                }
            }
            else if (!isActive && !wasActive && prev >= 0f)
            {
                // This frame's delta skipped over the entire window without landing inside it.
                // Open the window and set a hold token so it stays active for two ticks total,
                // giving at least one FixedUpdate step to detect the physics overlap.
                // Use <= so windows starting at exactly 0 are caught when prev == 0 (set by Bind).
                if (prev <= starts[i] && normalizedTime > ends[i])
                {
                    bits     |= mask;
                    holdBits |= mask;
                    _handler.OnEnter(in windows[i], i);
                }
            }
        }

        _activeBits   = bits;
        _skipHoldBits = holdBits;
        _prevNorm = normalizedTime;
    }

    public void Clear()
    {
        _count        = 0;
        _activeBits   = 0UL;
        _skipHoldBits = 0UL;
        _prevNorm     = -1f;
    }

    private void EnsureCapacity(int required)
    {
        if (_windows != null && _windows.Length >= required) return;

        int cap = Mathf.Max(required, 8);
        _windows = new TWindow[cap];
        _starts = new float[cap];
        _ends = new float[cap];
    }
}

// -- EXTENDED 128-WINDOW VERSION (just in case :'))
public sealed class FastWindowExecutor128<TWindow, THandler>
    where THandler : struct, IWindowHandler<TWindow>
{
    private const int MaxWindows = 128;

    private TWindow[] _windows;
    private float[] _starts;
    private float[] _ends;

    private ulong _activeBitsLow;   // Windows 0-63
    private ulong _activeBitsHigh;  // Windows 64-127
    private ulong _skipHoldBitsLow;
    private ulong _skipHoldBitsHigh;
    private int _count;

    private float _prevNorm;

    private THandler _handler;

    public FastWindowExecutor128(THandler handler)
    {
        _handler = handler;
        _prevNorm = -1f;
        EnsureCapacity(8);
    }

    public ref THandler Handler => ref _handler;

    public void Bind(IReadOnlyList<TWindow> windows)
    {
        Clear();

        if (windows == null || windows.Count == 0) return;

        int count = Mathf.Min(windows.Count, MaxWindows);
        _count = count;
        EnsureCapacity(count);

        for (int i = 0; i < count; i++)
        {
            TWindow w = windows[i];
            _windows[i] = w;

            WindowEvent ev = _handler.GetEvent(in w);
            _starts[i] = ev.start;
            _ends[i] = ev.end;
        }

        _activeBitsLow    = 0UL;
        _activeBitsHigh   = 0UL;
        _skipHoldBitsLow  = 0UL;
        _skipHoldBitsHigh = 0UL;
    }

    public void Tick(float normalizedTime)
    {
        int count = _count;
        if (count == 0) return;

        var windows = _windows;
        var starts = _starts;
        var ends = _ends;
        float prev = _prevNorm;

        ulong bitsLow      = _activeBitsLow;
        ulong bitsHigh     = _activeBitsHigh;
        ulong holdBitsLow  = _skipHoldBitsLow;
        ulong holdBitsHigh = _skipHoldBitsHigh;

        for (int i = 0; i < count; i++)
        {
            bool isActive = normalizedTime >= starts[i] && normalizedTime <= ends[i];

            bool wasActive;
            if (i < 64)
            {
                ulong mask = 1UL << i;
                wasActive = (bitsLow & mask) != 0UL;

                if (isActive != wasActive)
                {
                    if (isActive)
                    {
                        bitsLow     |=  mask;
                        holdBitsLow &= ~mask;
                        _handler.OnEnter(in windows[i], i);
                    }
                    else
                    {
                        if ((holdBitsLow & mask) != 0UL)
                        {
                            holdBitsLow &= ~mask;
                        }
                        else
                        {
                            bitsLow &= ~mask;
                            _handler.OnExit(in windows[i], i);
                        }
                    }
                }
                else if (!isActive && !wasActive && prev >= 0f)
                {
                    if (prev <= starts[i] && normalizedTime > ends[i])
                    {
                        bitsLow     |= mask;
                        holdBitsLow |= mask;
                        _handler.OnEnter(in windows[i], i);
                    }
                }
            }
            else
            {
                int shift = i - 64;
                ulong mask = 1UL << shift;
                wasActive = (bitsHigh & mask) != 0UL;

                if (isActive != wasActive)
                {
                    if (isActive)
                    {
                        bitsHigh     |=  mask;
                        holdBitsHigh &= ~mask;
                        _handler.OnEnter(in windows[i], i);
                    }
                    else
                    {
                        if ((holdBitsHigh & mask) != 0UL)
                        {
                            holdBitsHigh &= ~mask;
                        }
                        else
                        {
                            bitsHigh &= ~mask;
                            _handler.OnExit(in windows[i], i);
                        }
                    }
                }
                else if (!isActive && !wasActive && prev >= 0f)
                {
                    if (prev <= starts[i] && normalizedTime > ends[i])
                    {
                        bitsHigh     |= mask;
                        holdBitsHigh |= mask;
                        _handler.OnEnter(in windows[i], i);
                    }
                }
            }
        }

        _activeBitsLow    = bitsLow;
        _activeBitsHigh   = bitsHigh;
        _skipHoldBitsLow  = holdBitsLow;
        _skipHoldBitsHigh = holdBitsHigh;
        _prevNorm = normalizedTime;
    }

    public void Clear()
    {
        _count            = 0;
        _activeBitsLow    = 0UL;
        _activeBitsHigh   = 0UL;
        _skipHoldBitsLow  = 0UL;
        _skipHoldBitsHigh = 0UL;
        _prevNorm         = -1f;
    }

    private void EnsureCapacity(int required)
    {
        if (_windows != null && _windows.Length >= required) return;

        int cap = Mathf.Max(required, 8);
        _windows = new TWindow[cap];
        _starts = new float[cap];
        _ends = new float[cap];
    }
}

// -- Active window counter
public static class WindowExecutorUtils
{
    public static int CountActiveBits(ulong bits)
    {
        // Brian Kernighan's algorithm
        int count = 0;
        while (bits != 0)
        {
            bits &= bits - 1;
            count++;
        }
        return count;
    }

    // Get index of first active window, or -1 if none
    public static int GetFirstActiveIndex(ulong bits)
    {
        if (bits == 0) return -1;

        int index = 0;
        while ((bits & 1) == 0)
        {
            bits >>= 1;
            index++;
        }
        return index;
    }
}