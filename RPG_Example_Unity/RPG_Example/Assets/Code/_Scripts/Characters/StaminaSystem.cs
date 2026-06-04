using System;
using UnityEngine;

// Manages stamina consumption, recovery, and debt system.
public class StaminaSystem : MonoBehaviour, IStamina
{
    #region Fields

    [Header("Stamina Statistics")]
    [Tooltip("Time to wait after consuming stamina before recovery starts")]
    public float timeToStartRecovering = 1f;

    [SerializeField] private float _recoveryRate = 10f;
    [SerializeField] private float _recoveryAmountPerSecond = 10f;
    [SerializeField] private float _consumeRate = 1.5f;
    [SerializeField] private float _consumptionAmountPerSecond = 1.5f;

    [Header("Debt System")]
    [Tooltip("When stamina goes negative, recovery rate is reduced by this multiplier")]
    [SerializeField, Range(0.1f, 1f)] private float _debtRecoveryMultiplier = 0.4f;

    [Tooltip("Minimum time stamina must stay in debt before recovering (prevents instant recovery)")]
    [SerializeField] private float _minimumDebtDuration = 0.5f;

    [Tooltip("Maximum stamina debt allowed (as percentage of max stamina). Example: 0.5 = can go to -50% max stamina")]
    [SerializeField, Range(0.1f, 2f)] private float _maxDebtRatio = 0.5f;

    private float _currentStamina;
    private float _recoveryTimer;
    private float _debtTimer;
    private bool _isConsumingStamina;
    private bool _isInDebt;
    
    private bool _drainOnlyInCombat;
    private Func<bool> _combatCheckFunc;

    public event Action OnStaminaModifies;

    private CharacterStat _maxStamina;
    private const float STAMINA_EPS = 0.0001f;

    #endregion

    #region Properties

    public float MaxStamina => _maxStamina.Value;
    public float CurrentStamina => _currentStamina;
    public float RecoveryAmountPerSecond => _recoveryAmountPerSecond;
    public float CurrentDebt => _currentStamina < 0 ? -_currentStamina : 0f;
    public bool IsInDebt => _currentStamina < 0f;
    public float CurrentStamina01 => Mathf.Clamp01(_currentStamina / MaxStamina);
    public float CurrentDebt01 => IsInDebt ? Mathf.Clamp01(CurrentDebt / MaxStamina) : 0f;
    public float MaxDebt => MaxStamina * _maxDebtRatio;
    public bool IsDraining => _isConsumingStamina;
    
    public bool CanStartSprint(float minStamina) => !_isConsumingStamina && _currentStamina >= minStamina;

    #endregion

    public void Initialize()
    {
        _maxStamina = GetComponent<CharacterStats>().GetStat(Enums.StatType.Stamina);
        _currentStamina = Mathf.Clamp(_maxStamina.Value, 0f, _maxStamina.Value);
        _isInDebt = false;
        _debtTimer = 0f;
        _recoveryTimer = 0f;
        _isConsumingStamina = false;
    }

    #region Unity Callbacks

    private void Update()
    {
        if (!_isConsumingStamina && _currentStamina >= _maxStamina.Value) return;
        
        if (_isConsumingStamina)
        {
            // Check combat condition EVERY frame if conditional drain is enabled
            var shouldDrainThisFrame = !_drainOnlyInCombat || (_combatCheckFunc?.Invoke() ?? false);
            
            if (shouldDrainThisFrame)
            {
                ConsumeStamina();
            }
            else
            {
                // Drain paused (out of combat) - allow recovery to tick normally
                HandleStaminaRecover();
            }
        }
        else
        {
            HandleStaminaRecover();
        }
    }

    #endregion

    #region Public API

    public void SetRecoveryRate(float v) => _recoveryRate = v;
    public void SetRecoveryAmountPerSecond(float v) => _recoveryAmountPerSecond = v;
    public void SetConsumeRate(float v) => _consumeRate = v;
    public void SetConsumeAmountPerSecond(float v) => _consumptionAmountPerSecond = v;
    
    public void SetCurrentStamina(float value)
    {
        float max = _maxStamina != null ? _maxStamina.Value : 0f;
        _currentStamina = value < 0f ? max : Mathf.Clamp(value, -MaxDebt, max);
        OnStaminaModifies?.Invoke();
    }

    public bool TryConsumeStamina(float cost)
    {
        // Rule 1: Must have positive stamina to start any action
        if (_currentStamina <= 0f) return false;

        // Rule 2: Action cannot exceed maximum allowed debt
        float resultingStamina = _currentStamina - cost;
        float maxNegative = -MaxDebt;

        if (resultingStamina < maxNegative) return false;

        OnConsumeStamina(cost);
        return true;
    }

    public virtual void OnConsumeStamina(float amount)
    {
        float previousStamina = _currentStamina;
        _currentStamina -= amount;

        // Clamp to max allowed debt
        float maxNegative = -MaxDebt;
        _currentStamina = Mathf.Max(_currentStamina, maxNegative);

        // Reset recovery timer on any consumption
        _recoveryTimer = 0f;

        // Track debt transitions
        bool wasInDebt = previousStamina < 0f;
        _isInDebt = _currentStamina < 0f;

        if (!wasInDebt && _isInDebt)
        {
            _debtTimer = 0f;
        }

        OnStaminaModifies?.Invoke();
    }

    public void StartDrain(bool onlyInCombat = false, Func<bool> combatCheck = null)
    {
        if (_isConsumingStamina) return;

        _isConsumingStamina = true;
        _drainOnlyInCombat = onlyInCombat;
        _combatCheckFunc = combatCheck;

        _recoveryTimer = 0f;
    }

    public void StopDrain()
    {
        if (!_isConsumingStamina) return;

        _isConsumingStamina = false;
        _drainOnlyInCombat = false;
        _combatCheckFunc = null;
    }

    public bool HasStaminaToAction(float cost) => _currentStamina >= cost;

    public bool CanAffordWithDebt(float cost)
    {
        // Must have positive stamina to start
        if (_currentStamina <= 0f) return false;
        
        float resultingStamina = _currentStamina - cost;
        float maxNegative = -MaxDebt;
        return resultingStamina >= maxNegative;
    }

    #endregion

    #region Internal Logic

    private void HandleStaminaRecover()
    {
        // Early exit if already at max
        if (_currentStamina >= _maxStamina.Value) return;

        _recoveryTimer += Time.deltaTime;

        if (_isInDebt)
        {
            _debtTimer += Time.deltaTime;
        }

        // Wait for recovery delay
        if (_recoveryTimer < timeToStartRecovering) return;

        // If in debt, respect minimum debt duration
        if (_isInDebt && _debtTimer < _minimumDebtDuration) return;

        // Calculate effective recovery based on debt state
        float effectiveRecoveryRate = _recoveryRate;
        float effectiveRecoveryAmount = _recoveryAmountPerSecond;

        if (_isInDebt)
        {
            effectiveRecoveryRate /= _debtRecoveryMultiplier;
            effectiveRecoveryAmount *= _debtRecoveryMultiplier;
        }

        ModifyStamina(effectiveRecoveryAmount, effectiveRecoveryRate);

        // Check if we recovered from debt
        if (!_isInDebt || !(_currentStamina >= 0f)) return;
        
        _isInDebt = false;
        _debtTimer = 0f;
    }

    private void ConsumeStamina()
    {
        ModifyStamina(-_consumptionAmountPerSecond, _consumeRate);

        // Track debt transitions
        var wasInDebt = _isInDebt;
        _isInDebt = _currentStamina < 0f;

        if (!wasInDebt && _isInDebt)
        {
            _debtTimer = 0f;
        }

        // Sprint cannot go into debt - auto-stop at 0
        if (!(_currentStamina <= 0f)) return;
        
        _currentStamina = 0f;
        StopDrain();
    }

    protected virtual void ModifyStamina(float v, float rate)
    {
        if (_maxStamina == null || rate <= 0f) return;

        float prev = _currentStamina;
        _currentStamina += (v / rate) * Time.deltaTime;

        if (v < 0f) // Draining
        {
            _currentStamina = Mathf.Max(_currentStamina, 0f);
        }
        else // Recovering
        {
            _currentStamina = Mathf.Min(_currentStamina, _maxStamina.Value);
        }

        // Only invoke event if stamina actually changed
        if (Mathf.Abs(_currentStamina - prev) > STAMINA_EPS)
        {
            OnStaminaModifies?.Invoke();
        }
    }

    #endregion
    

}