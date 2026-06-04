using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PooledParticleAutoReturn : MonoBehaviour
{
    private Action _release;

    public void Bind(Action release)
    {
        _release = release;

        var ps = GetComponent<ParticleSystem>();
        if (ps == null) return;
        
        var main = ps.main;
        main.stopAction = ParticleSystemStopAction.Callback;
    }

    private void OnParticleSystemStopped()
    {
        _release?.Invoke();
    }
}