using System.Collections.Generic;
using UnityEngine;

public sealed class AttackWindowExecutor
{
    private readonly IWeaponUser _weapons;
    private readonly int _attackerId;

    private AttackData _currentAttack;

    private readonly HashSet<(Enums.WeaponHand hand, Enums.ColliderSlot slot)> _enabledSlots = new();
    private readonly Dictionary<(Enums.WeaponHand hand, Enums.ColliderSlot slot), int> _activeWindowBySlot = new();

    private static int _attackInstanceCounter;
    private int _attackInstanceId;

    private readonly List<DamageWindowRuntime> _runtimeWindows = new(8);

    private FastWindowExecutor64<DamageWindowRuntime, DamageWindowHandler> _executor;

    public AttackWindowExecutor(IWeaponUser weapons, int attackerId)
    {
        _weapons = weapons;
        _attackerId = attackerId;

        if (_attackerId == 0)
        {
            Debug.LogError("[AttackWindowExecutor] attackerId is 0. Damage window tokens will not match hit colliders. Ensure owner root matches WeaponInstance.ConfigureForOwner.");
        }

        _executor = new FastWindowExecutor64<DamageWindowRuntime, DamageWindowHandler>(
            new DamageWindowHandler(_weapons, _attackerId, _enabledSlots, _activeWindowBySlot)
        );
    }

    public void StartAttack(AttackData attack)
    {
        StopAttack();

        _currentAttack = attack;

        _enabledSlots.Clear();
        _activeWindowBySlot.Clear();

        _attackInstanceId = ++_attackInstanceCounter;

        _runtimeWindows.Clear();

        if (_currentAttack == null || _currentAttack.damageWindows == null || _currentAttack.damageWindows.Count == 0)
        {
            _executor.Clear();
            return;
        }

        var src = _currentAttack.damageWindows;

        if (_runtimeWindows.Capacity < src.Count)
            _runtimeWindows.Capacity = src.Count;

        for (int i = 0; i < src.Count; i++)
            _runtimeWindows.Add(new DamageWindowRuntime(src[i]));

        // Update runtime id used for tokens.
        _executor.Handler.AttackInstanceId = _attackInstanceId;

        _executor.Bind(_runtimeWindows);
    }

    public void StopAttack()
    {
        if (_currentAttack == null) return;

        foreach (var s in _enabledSlots)
        {
            DamageWindowRegistry.ClearSlot(_attackerId, s.hand, s.slot);
            _weapons.DisableSlot(s.hand, s.slot);
        }

        _enabledSlots.Clear();
        _activeWindowBySlot.Clear();

        _currentAttack = null;

        _runtimeWindows.Clear();
        _executor.Clear();

        _attackInstanceId = 0;
    }

    public void Tick(float tNorm)
    {
        if (_currentAttack == null) return;
        _executor.Tick(tNorm);
    }

    public bool TryGetActiveDamageWindowIndex(Enums.WeaponHand hand, Enums.ColliderSlot slot, out int windowIndex)
    {
        if (_currentAttack != null) return _activeWindowBySlot.TryGetValue((hand, slot), out windowIndex);

        windowIndex = -1;
        return false;
    }

    private readonly struct DamageWindowRuntime
    {
        public readonly DamageWindow window;

        public DamageWindowRuntime(DamageWindow window)
        {
            this.window = window;
        }
    }

    private struct DamageWindowHandler : IWindowHandler<DamageWindowRuntime>
    {
        private readonly IWeaponUser _weapons;
        private readonly int _attackerId;

        private readonly HashSet<(Enums.WeaponHand hand, Enums.ColliderSlot slot)> _enabledSlots;
        private readonly Dictionary<(Enums.WeaponHand hand, Enums.ColliderSlot slot), int> _activeWindowBySlot;

        public int AttackInstanceId;

        public DamageWindowHandler(
            IWeaponUser weapons,
            int attackerId,
            HashSet<(Enums.WeaponHand hand, Enums.ColliderSlot slot)> enabledSlots,
            Dictionary<(Enums.WeaponHand hand, Enums.ColliderSlot slot), int> activeWindowBySlot)
        {
            _weapons = weapons;
            _attackerId = attackerId;
            _enabledSlots = enabledSlots;
            _activeWindowBySlot = activeWindowBySlot;
            AttackInstanceId = 0;
        }

        public WindowEvent GetEvent(in DamageWindowRuntime w) => w.window.window;

        public void OnEnter(in DamageWindowRuntime w, int index)
        {
            var dw = w.window;
            var key = (dw.weaponHand, dw.slot);

            DamageWindowRegistry.SetActive(_attackerId, dw.weaponHand, dw.slot,
                new DamageWindowRegistry.DamageWindowToken(
                    AttackInstanceId,
                    index,
                    dw.allowMultiHit,
                    dw.perTargetCooldown,
                    dw.maxHitsPerTarget
                )
            );

            _weapons.EnableSlot(dw.weaponHand, dw.slot);
            _enabledSlots.Add(key);
            _activeWindowBySlot[key] = index;

        }

        public void OnExit(in DamageWindowRuntime w, int index)
        {
            var dw = w.window;
            var key = (dw.weaponHand, dw.slot);

            if (_activeWindowBySlot.TryGetValue(key, out var activeIndex) && activeIndex != index) return;

            DamageWindowRegistry.ClearIfMatches(_attackerId, dw.weaponHand, dw.slot, AttackInstanceId, index);

            _weapons.DisableSlot(dw.weaponHand, dw.slot);
            _enabledSlots.Remove(key);

            if (_activeWindowBySlot.TryGetValue(key, out activeIndex) && activeIndex == index)
            {
                _activeWindowBySlot.Remove(key);
            }

        }
    }
}