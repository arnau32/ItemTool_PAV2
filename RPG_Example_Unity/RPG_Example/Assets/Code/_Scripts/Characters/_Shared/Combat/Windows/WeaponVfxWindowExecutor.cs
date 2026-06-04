using UnityEngine;

public sealed class WeaponVfxWindowExecutor
{
    private sealed class EnabledMask
    {
        public ulong right;
        public ulong left;

        public void Clear()
        {
            right = 0UL;
            left = 0UL;
        }
    }

    private readonly IWeaponVfxUser _vfx;

    // Tracks ALL activated weapon VFX (both disableOnEnd true and false).
    private readonly EnabledMask _enabled = new EnabledMask();

    // Tracks only weapon VFX that have disableOnEnd=true and should be force-disabled on StopAttack.
    private readonly EnabledMask _forceDisable = new EnabledMask();

    private AttackData _currentAttack;

    private FastWindowExecutor64<WeaponVfxWindow, WeaponVfxWindowHandler> _executor;

    public WeaponVfxWindowExecutor(IWeaponVfxUser vfx)
    {
        _vfx = vfx;
        _executor = new FastWindowExecutor64<WeaponVfxWindow, WeaponVfxWindowHandler>(
            new WeaponVfxWindowHandler(_vfx, _enabled, _forceDisable)
        );
    }

    public void StartAttack(AttackData attack)
    {
        StopAttack();
        _currentAttack = attack;
        _enabled.Clear();
        _forceDisable.Clear();

        if (_currentAttack == null || _currentAttack.weaponVfxWindows == null || _currentAttack.weaponVfxWindows.Count == 0)
        {
            _executor.Clear();
            return;
        }
        
        _executor.Bind(_currentAttack.weaponVfxWindows);
    }

    public void StopAttack()
    {
        if (_currentAttack == null) return;

        // Only force-disable VFX that explicitly requested it (disableOnEnd = true).
        DisableMask(Enums.WeaponHand.Right, _forceDisable.right);
        DisableMask(Enums.WeaponHand.Left, _forceDisable.left);

        _enabled.Clear();
        _forceDisable.Clear();
        _currentAttack = null;
        _executor.Clear();
    }

    public void Tick(float tNorm)
    {
        if (_currentAttack == null) return;
        _executor.Tick(tNorm);
    }

    private void DisableMask(Enums.WeaponHand hand, ulong mask)
    {
        if (mask == 0UL) return;

        for (int id = 0; id < 64; id++)
        {
            ulong bit = 1UL << id;
            if ((mask & bit) == 0UL) continue;
            _vfx.DeactiveWeaponVfx(hand, id);
        }
    }

    private struct WeaponVfxWindowHandler : IWindowHandler<WeaponVfxWindow>
    {
        private readonly IWeaponVfxUser _vfx;
        private readonly EnabledMask _enabled;
        private readonly EnabledMask _forceDisable;

        public WeaponVfxWindowHandler(IWeaponVfxUser vfx, EnabledMask enabled, EnabledMask forceDisable)
        {
            _vfx = vfx;
            _enabled = enabled;
            _forceDisable = forceDisable;
        }

        public WindowEvent GetEvent(in WeaponVfxWindow w) => w.window;

        public void OnEnter(in WeaponVfxWindow w, int index)
        {
            _vfx.ActivateWeaponVfx(w.hand, w.vfxId);
            SetBit(_enabled, w.hand, w.vfxId, true);

            // Track which VFX need force-disable so StopAttack knows.
            if (w.disableOnEnd)
            {
                SetBit(_forceDisable, w.hand, w.vfxId, true);
            }
        }

        public void OnExit(in WeaponVfxWindow w, int index)
        {
            if (!w.disableOnEnd) return;

            _vfx.DeactiveWeaponVfx(w.hand, w.vfxId);
            SetBit(_enabled, w.hand, w.vfxId, false);
            SetBit(_forceDisable, w.hand, w.vfxId, false);
        }

        private static void SetBit(EnabledMask mask, Enums.WeaponHand hand, int id, bool enabled)
        {
            if ((uint)id >= 64u) return;

            ulong bit = 1UL << id;

            switch (hand)
            {
                case Enums.WeaponHand.Right:
                    mask.right = enabled ? (mask.right | bit) : (mask.right & ~bit);
                    break;

                case Enums.WeaponHand.Left:
                    mask.left = enabled ? (mask.left | bit) : (mask.left & ~bit);
                    break;
            }
        }
    }
}