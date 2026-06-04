using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

// Drives both AudioTrigger (fire-once) and AudioWindow (looping) lists
// during attack playback. Called every frame by CombatWindowExecutor.
//
// Design contract:
//   - StartAttack()  → bind the lists, reset state.
//   - Tick(tNorm)    → advance time. Fires triggers once, manages window loops.
//   - StopAttack()   → stop all active AudioWindow instances.
//
// Zero per-frame allocations after StartAttack. All state is pre-allocated.
public sealed class AudioWindowExecutor
{
    // -------------------------------------------------------------------------
    // Audio trigger state — fire-once tracker
    // -------------------------------------------------------------------------

    // Parallel array to audioTriggers. True = already fired this attack.
    private bool[] _triggerFired;
    private IReadOnlyList<AudioTrigger> _audioTriggers;
    private int _triggerCount;

    // -------------------------------------------------------------------------
    // Audio window state — looping instances managed via FastWindowExecutor64
    // -------------------------------------------------------------------------

    private FastWindowExecutor64<AudioWindow, AudioWindowHandler> _windowExecutor;
    private AttackData _currentAttack;

    // World position source — used for 3D PlayOneShot placement.
    private readonly Transform _ownerTransform;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public AudioWindowExecutor(Transform ownerTransform)
    {
        _ownerTransform = ownerTransform;
        _windowExecutor = new FastWindowExecutor64<AudioWindow, AudioWindowHandler>(
            new AudioWindowHandler());
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void StartAttack(AttackData attack)
    {
        StopAttack();
        _currentAttack = attack;

        // --- Bind AudioTriggers ---
        _audioTriggers = (attack != null) ? attack.audioTriggers : null;
        _triggerCount  = _audioTriggers != null ? _audioTriggers.Count : 0;
        EnsureTriggerCapacity(_triggerCount);
        for (int i = 0; i < _triggerCount; i++) _triggerFired[i] = false;

        // --- Bind AudioWindows ---
        if (attack != null && attack.audioWindows != null && attack.audioWindows.Count > 0)
            _windowExecutor.Bind(attack.audioWindows);
        else
            _windowExecutor.Clear();
    }

    public void StopAttack()
    {
        if (_currentAttack == null) return;

        // Force-stop all active AudioWindow instances.
        _windowExecutor.Handler.StopAll();
        _windowExecutor.Clear();

        _currentAttack = null;
        _triggerCount  = 0;
        _audioTriggers = null;
    }

    public void Tick(float tNorm)
    {
        if (_currentAttack == null) return;

        TickTriggers(tNorm);
        _windowExecutor.Tick(tNorm);
    }

    // -------------------------------------------------------------------------
    // Private — trigger logic
    // -------------------------------------------------------------------------

    private void TickTriggers(float tNorm)
    {
        if (_triggerCount == 0) return;

        var triggers = _audioTriggers;
        var fired    = _triggerFired;
        var pos      = _ownerTransform != null ? _ownerTransform.position : Vector3.zero;

        for (int i = 0; i < _triggerCount; i++)
        {
            if (fired[i]) continue;

            // Copy the struct by value — AudioTrigger is small (float + EventReference guid).
            // No allocation. List<T> indexer cannot return ref on C# < 8 runtime targets.
            AudioTrigger trigger = triggers[i];
            if (tNorm < trigger.triggerAt) continue;

            fired[i] = true;

            if (!trigger.sound.IsNull)
                RuntimeManager.PlayOneShot(trigger.sound, pos);
        }
    }

    private void EnsureTriggerCapacity(int required)
    {
        if (_triggerFired == null || _triggerFired.Length < required)
            _triggerFired = new bool[Mathf.Max(required, 8)];
    }

    // -------------------------------------------------------------------------
    // Inner handler — AudioWindow (loop/stop)
    // -------------------------------------------------------------------------

    // Struct handler required by FastWindowExecutor64.
    // Manages the active EventInstances for AudioWindow entries.
    private struct AudioWindowHandler : IWindowHandler<AudioWindow>
    {
        // Parallel arrays — index matches the AudioWindow list index.
        // Allocated lazily on first OnEnter. Sized to FastWindowExecutor64 limit (64).
        private EventInstance[] _instances;
        private bool[]          _active;

        public WindowEvent GetEvent(in AudioWindow w) => w.window;

        public void OnEnter(in AudioWindow w, int index)
        {
            if (w.sound.IsNull) return;

            EnsureCapacity(index + 1);

            // Release any stale instance at this index (safety).
            if (_active[index])
            {
                _instances[index].stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                _instances[index].release();
                _active[index] = false;
            }

            var instance = RuntimeManager.CreateInstance(w.sound);
            instance.start();
            _instances[index] = instance;
            _active[index]    = true;
        }

        public void OnExit(in AudioWindow w, int index)
        {
            if (!_active[index]) return;

            var mode = w.stopImmediate
                ? FMOD.Studio.STOP_MODE.IMMEDIATE
                : FMOD.Studio.STOP_MODE.ALLOWFADEOUT;

            _instances[index].stop(mode);
            _instances[index].release();
            _active[index] = false;
        }

        // Called by StopAttack to force-stop all remaining instances.
        public void StopAll()
        {
            if (_active == null) return;
            for (int i = 0; i < _active.Length; i++)
            {
                if (!_active[i]) continue;
                _instances[i].stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                _instances[i].release();
                _active[i] = false;
            }
        }

        private void EnsureCapacity(int required)
        {
            if (_instances != null && _instances.Length >= required) return;

            int cap = Mathf.Max(required, 8);
            var newInst   = new EventInstance[cap];
            var newActive = new bool[cap];

            if (_instances != null)
            {
                System.Array.Copy(_instances, newInst,   _instances.Length);
                System.Array.Copy(_active,    newActive, _active.Length);
            }

            _instances = newInst;
            _active    = newActive;
        }
    }
}