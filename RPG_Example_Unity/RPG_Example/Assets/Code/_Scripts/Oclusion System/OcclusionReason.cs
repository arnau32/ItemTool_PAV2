using System;

[Flags]
public enum OcclusionReason
{
    None        = 0,
    LineOfSight = 1 << 0,
    Building    = 1 << 1,
}
