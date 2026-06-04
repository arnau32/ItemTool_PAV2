using UnityEngine;

[System.Serializable]
public sealed class EnemyAlertController
{
    #region Config

    [Header("Thresholds")] 
    [Tooltip("alertValue must exceed this to enter Suspicious. Range: 0.05–0.20")] [SerializeField, Range(0f, 1f)]
    private float _suspiciousThreshold = 0.15f;

    [Tooltip("alertValue must exceed this to enter Alert. Range: 0.40–0.80")] [SerializeField, Range(0f, 1f)]
    private float _alertThreshold = 0.70f;

    [Tooltip("alertValue must reach this to enter Combat. Range: 1.0–1.5")] [SerializeField, Range(0f, 2f)]
    private float _combatThreshold = 1.0f;

    [Header("Accumulation rates (units / second)")] [Tooltip("Added per second with full FOV + LOS at close range. Range: 1.0–3.0")] [SerializeField]
    private float _directVisionRate = 2.5f;

    [Tooltip("Minimum rate multiplier at max detection range. 1 = no distance falloff. Range: 0.1–1.0")] [SerializeField, Range(0.1f, 1f)]
    private float _distanceRateMin = 0.25f;

    [Tooltip("Added per second for peripheral vision (outside FOV cone). Range: 0.3–1.0")] [SerializeField]
    private float _peripheralRate = 0.8f;

    [Tooltip("Added per second for audio stimulus. Range: 0.2–0.8")] [SerializeField]
    private float _audioRate = 0.4f;

    [Tooltip("Applied when an ally broadcasts an alert event. Range: 0.3–1.0")] [SerializeField]
    private float _propagationRate = 0.6f;

    [Header("Decay rates (units / second, no stimulus)")] 
    [Tooltip("Decay per second while Suspicious. Range: 0.10–0.50")] [SerializeField]
    private float _suspiciousDecayRate = 0.25f;

    [Tooltip("Decay per second while Alert. Range: 0.05–0.20")] [SerializeField]
    private float _alertDecayRate = 0.12f;

    [Tooltip("Decay per second while Combat after losing visual. Range: 0.04–0.15")] [SerializeField]
    private float _combatDecayRate = 0.08f;

    [Header("Tick rates (seconds)")] 
    [Tooltip("Perception scan interval while Unaware. Range: 0.15–0.30")] [SerializeField]
    private float _unawareTick = 0.20f;

    [Tooltip("Perception scan interval once Suspicious or above. Range: 0.05–0.15")] [SerializeField]
    private float _awarenessTickFast = 0.10f;

    #endregion

    #region State

    private float _alertValue;
    private Enums.AlertLevel _currentLevel;

    private const float MAX_ALERT_VALUE = 1.5f;

    private float CombatExitThreshold => _combatThreshold * 0.85f;

    #endregion

    #region Properties

    public float AlertValue => _alertValue;
    public float AlertThreshold => _alertThreshold;
    public float CombatThreshold => _combatThreshold;
    public float SuspiciousThreshold => _suspiciousThreshold;
    public Enums.AlertLevel AlertLevel => _currentLevel;
    public float TickInterval => _currentLevel == Enums.AlertLevel.Unaware ? _unawareTick : _awarenessTickFast;

    #endregion

    #region Public API

    public void Reset()
    {
        _alertValue = 0f;
        _currentLevel = Enums.AlertLevel.Unaware;
    }

    // Instantly sets alert to maximum used by CombatWanderState on enter
    public void ForceMaxAlert()
    {
        _alertValue = MAX_ALERT_VALUE;
        _currentLevel = Enums.AlertLevel.Combat;
    }

    /// distanceNorm01: 0 = at enemy position, 1 = at maxDetectionRange.
    /// Modulates directVisionRate so far targets accumulate alert more slowly.
    public void Tick(bool hasDirectVision, bool hasPeripheralVision, bool hasAudio, float dt, float distanceNorm01 = 0f)
    {
        if (hasDirectVision)
        {
            float distMult = Mathf.Lerp(1f, _distanceRateMin, distanceNorm01);
            _alertValue += _directVisionRate * distMult * dt;
        }
        else if (hasPeripheralVision)
        {
            _alertValue += _peripheralRate * dt;
        }
        else if (hasAudio)
        {
            _alertValue += _audioRate * dt;
        }
        else
        {
            ApplyDecay(dt);
        }

        _alertValue = Mathf.Clamp(_alertValue, 0f, MAX_ALERT_VALUE);
        RefreshLevel(hasDirectVision);
    }

    /// Raises alertValue by propagation, guaranteeing a minimum level based on
    /// the source's AlertLevel so allies react proportionally.
    public void ReceivePropagation(float strength, Enums.AlertLevel sourceLevel)
    {
        _alertValue += _propagationRate * strength;

        float minValue = sourceLevel >= Enums.AlertLevel.Combat
            ? _alertThreshold
            : _suspiciousThreshold;

        if (_alertValue < minValue)
        {
            _alertValue = minValue;
        }

        _alertValue = Mathf.Min(_alertValue, MAX_ALERT_VALUE);
        RefreshLevel(hasDirectVision: false);
    }

    #endregion

    #region Helpers

    private void ApplyDecay(float dt)
    {
        float rate = _currentLevel switch
        {
            Enums.AlertLevel.Suspicious => _suspiciousDecayRate,
            Enums.AlertLevel.Alert => _alertDecayRate,
            Enums.AlertLevel.Combat => _combatDecayRate,
            _ => 0f
        };

        _alertValue = Mathf.Max(0f, _alertValue - rate * dt);
    }

    private void RefreshLevel(bool hasDirectVision)
    {
        if (_currentLevel == Enums.AlertLevel.Combat && hasDirectVision) return;

        Enums.AlertLevel newLevel;

        if (_alertValue >= _combatThreshold)
        {
            newLevel = Enums.AlertLevel.Combat;
        }
        else if (_alertValue >= _alertThreshold)
        {
            newLevel = Enums.AlertLevel.Alert;
        }
        else if (_alertValue >= _suspiciousThreshold)
        {
            newLevel = Enums.AlertLevel.Suspicious;
        }
        else
        {
            newLevel = Enums.AlertLevel.Unaware;
        }

        if (_currentLevel == Enums.AlertLevel.Combat && newLevel == Enums.AlertLevel.Alert)
        {
            if (_alertValue >= CombatExitThreshold) return;
        }

        _currentLevel = newLevel;
    }

    #endregion
}