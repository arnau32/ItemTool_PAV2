using System;
using UnityEngine;

[Serializable]
public struct WeaponVfxWindow
{
    public WindowEvent window;

    [Tooltip("Which weapon instance receives this VFX (Right/Left for dual-wield).")]
    public Enums.WeaponHand hand;

    [Tooltip("Index inside WeaponVfxController trailGroups.")] [Range(0, 63)] public int vfxId;

    [Tooltip("If true, the VFX is disabled on window exit.")] public bool disableOnEnd;
}