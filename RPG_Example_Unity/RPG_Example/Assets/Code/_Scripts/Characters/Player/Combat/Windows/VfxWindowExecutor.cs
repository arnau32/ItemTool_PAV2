using System.Collections.Generic;

public sealed class VfxWindowExecutor
{
    private readonly IVfxUser _vfx;

    private AttackData _currentAttack;

    // Tracks ALL VFX that were activated (for OnExit with disableOnEnd=true).
    private readonly HashSet<int> _enabledVfx = new HashSet<int>();

    // Tracks only VFX that have disableOnEnd=true and should be force-disabled on StopAttack.
    private readonly HashSet<int> _forceDisableVfx = new HashSet<int>();

    private FastWindowExecutor64<AttackVfxWindow, VfxWindowHandler> _executor;

    public VfxWindowExecutor(IVfxUser vfx)
    {
        _vfx = vfx;
        _executor = new FastWindowExecutor64<AttackVfxWindow, VfxWindowHandler>(new VfxWindowHandler(_vfx, _enabledVfx, _forceDisableVfx));
    }

    public void StartAttack(AttackData attack)
    {
        StopAttack();
        _currentAttack = attack;

        _enabledVfx.Clear();
        _forceDisableVfx.Clear();

        if (_currentAttack == null || _currentAttack.vfxWindows == null || _currentAttack.vfxWindows.Count == 0)
        {
            _executor.Clear();
            return;
        }

        _executor.Bind(_currentAttack.vfxWindows);
    }

    public void StopAttack()
    {
        if (_currentAttack == null) return;

        // Only force-disable VFX that explicitly requested it (disableOnEnd = true).
        foreach (var id in _forceDisableVfx)
        {
            _vfx.DeactiveAttack(id);
        }

        _enabledVfx.Clear();
        _forceDisableVfx.Clear();
        _currentAttack = null;

        _executor.Clear();
    }

    public void Tick(float tNorm)
    {
        if (_currentAttack == null) return;
        _executor.Tick(tNorm);
    }

    private struct VfxWindowHandler : IWindowHandler<AttackVfxWindow>
    {
        private readonly IVfxUser _vfx;
        private readonly HashSet<int> _enabled;
        private readonly HashSet<int> _forceDisable;

        public VfxWindowHandler(IVfxUser vfx, HashSet<int> enabled, HashSet<int> forceDisable)
        {
            _vfx = vfx;
            _enabled = enabled;
            _forceDisable = forceDisable;
        }

        public WindowEvent GetEvent(in AttackVfxWindow w) => w.window;

        public void OnEnter(in AttackVfxWindow w, int index)
        {
            _vfx.ActivateAttack(w.vfxId);
            _enabled.Add(w.vfxId);

            // Track which VFX need force-disable so StopAttack knows.
            if (w.disableOnEnd)
            {
                _forceDisable.Add(w.vfxId);
            }
        }

        public void OnExit(in AttackVfxWindow w, int index)
        {
            if (!w.disableOnEnd) return;

            _vfx.DeactiveAttack(w.vfxId);
            _enabled.Remove(w.vfxId);
            _forceDisable.Remove(w.vfxId);
        }
    }
}