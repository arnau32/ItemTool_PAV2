using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HearSensor
{
    [Tooltip("Max distance at which the enemy can hear noisy hostiles (sprinting, shooting). Range: 5–20 m")] [SerializeField]
    private float _hearRange = 10f;

    private float HearRange2 => _hearRange * _hearRange;

    public ITargetable GetBestNoisyHostile(Vector3 position, List<ITargetable> candidates, Enums.Faction faction)
    {
        ITargetable best = null;
        float bestD2 = float.MaxValue;
        float hearRange2 = HearRange2;

        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            var target = candidates[i];

            if (!target.IsNoisy || target.Faction == faction) continue;

            var d2 = (target.Transform.position - position).sqrMagnitude;

            if (d2 > hearRange2) continue;

            if (!(d2 < bestD2)) continue;

            bestD2 = d2;
            best = target;
        }

        return best;
    }
}