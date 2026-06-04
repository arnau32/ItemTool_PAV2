using System.Collections.Generic;
using UnityEngine;

public class OutlineService : IOutlineService, IShutdownable
{
    readonly List<Renderer> _targets = new(16);

    public bool HasTargets => _targets.Count > 0;
    public IReadOnlyList<Renderer> TargetRenderers => _targets;

    public void Register(Renderer[] renderers)
    {
        foreach (Renderer r in renderers)
        {
            if (r != null && !_targets.Contains(r))
                _targets.Add(r);
        }
    }

    public void Unregister(Renderer[] renderers)
    {
        foreach (Renderer r in renderers)
            _targets.Remove(r);
    }

    public void ClearAll() => _targets.Clear();

    public void Shutdown() => ClearAll();
}