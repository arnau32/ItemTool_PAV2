using System.Collections.Generic;
using UnityEngine;

/// Limits how many enemies of the same faction can attack the player simultaneously.
/// Enemies request a token before committing to an attack and release it when done.
/// Factions not present in the dictionary are treated as having unlimited tokens.
public class AttackTokenService : MonoBehaviour, IGameServices
{
    #region Fields

    [Tooltip("Default max simultaneous attackers per faction when not overridden.")]
    [SerializeField, Min(1)] private int _defaultMaxTokens = 2;

    [Tooltip("Optional per-faction overrides. Factions not listed use _defaultMaxTokens.")]
    [SerializeField] private FactionTokenConfig[] _factionOverrides;

    [Tooltip("After a token is released, the faction must wait a random [0, max] seconds before the next enemy can acquire one. Adds natural spacing between consecutive attacks.")]
    [SerializeField, Min(0f)] private float _releaseCooldownMax = 2f;

    [Tooltip("Minimum seconds between two token acquisitions of the same faction. Prevents two enemies attacking in the exact same frame.")]
    [SerializeField, Min(0f)] private float _acquireStaggerDelay = 0.25f;

    private readonly Dictionary<Enums.Faction, int>   _activeTokens    = new();
    private readonly Dictionary<Enums.Faction, int>   _maxTokens       = new();
    private readonly Dictionary<Enums.Faction, float> _nextAcquireTime = new();
    private readonly Dictionary<Enums.Faction, float> _lastAcquireTime = new();

    [System.Serializable]
    public struct FactionTokenConfig
    {
        public Enums.Faction faction;
        [Min(1)] public int maxTokens;
    }

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _maxTokens.Clear();
        _activeTokens.Clear();
        _nextAcquireTime.Clear();
        _lastAcquireTime.Clear();

        if (_factionOverrides == null) return;

        for (int i = 0; i < _factionOverrides.Length; i++)
        {
            _maxTokens[_factionOverrides[i].faction] = _factionOverrides[i].maxTokens;
        }
    }

    #endregion

    #region Public API

    /// Returns true and increments the faction token count if a slot is available,
    /// the post-release cooldown has expired, and the acquire stagger delay since
    /// the last acquisition has elapsed.
    public bool TryAcquire(Enums.Faction faction)
    {
        float now = Time.time;

        if (now < GetNextAcquireTime(faction)) return false;

        if (_lastAcquireTime.TryGetValue(faction, out float last) &&
            now < last + _acquireStaggerDelay) return false;

        int max = GetMax(faction);

        _activeTokens.TryGetValue(faction, out int current);

        if (current >= max) return false;

        _activeTokens[faction]    = current + 1;
        _lastAcquireTime[faction] = now;
        return true;
    }

    /// Releases a previously acquired token and starts the per-faction cooldown.
    /// Safe to call if no token was held.
    public void Release(Enums.Faction faction)
    {
        if (!_activeTokens.TryGetValue(faction, out int current)) return;

        _activeTokens[faction] = Mathf.Max(0, current - 1);

        if (_releaseCooldownMax > 0f)
            _nextAcquireTime[faction] = Time.time + Random.Range(0f, _releaseCooldownMax);
    }

    public int ActiveTokens(Enums.Faction faction)
    {
        _activeTokens.TryGetValue(faction, out int current);
        return current;
    }

    public int MaxTokens(Enums.Faction faction) => GetMax(faction);

    #endregion

    #region Helpers

    private int GetMax(Enums.Faction faction)
    {
        return _maxTokens.TryGetValue(faction, out int max) ? max : _defaultMaxTokens;
    }

    private float GetNextAcquireTime(Enums.Faction faction)
    {
        return _nextAcquireTime.TryGetValue(faction, out float t) ? t : 0f;
    }

    #endregion
}