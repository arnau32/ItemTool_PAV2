using System.Collections.Generic;
using UnityEngine;

public interface IOutlineService : IGameServices
{
    bool HasTargets { get; }
    IReadOnlyList<Renderer> TargetRenderers { get; }
    void Register(Renderer[] renderers);
    void Unregister(Renderer[] renderers);
    void ClearAll();
}