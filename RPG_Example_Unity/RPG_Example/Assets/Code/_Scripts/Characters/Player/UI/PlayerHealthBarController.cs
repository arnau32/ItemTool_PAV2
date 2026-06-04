using UnityEngine;
using UnityEngine.UI;

// Drives the ghost bars on the player's HUD health bar (UGUI Images).
//
//   Damage ghost      — shows the previous higher health value after taking damage;
//                       decays down toward current health after a brief delay.
//
//   Heal forward ghost — appears above the current health bar when a healing
//                        consumable is consumed. Tracks the REMAINING heal amount
//                        so it automatically re-anchors when damage is taken mid-heal:
//                          target = currentHealth + remainingHeal
//                        Each heal tick reduces remainingHeal; damage only shifts
//                        the landing position, not the remaining amount.
//
// Layer order in the Canvas (back to front):
//   Background → HealForwardGhost → DamageGhost → MainHealthBar
//
// Call SetHealthSystem() from PlayerLifeController.Initialize() to wire events.
public class PlayerHealthBarController : MonoBehaviour
{
    #region Fields

    [Header("Bars")]
    [Tooltip("Behind main bar. Shows previous higher health after damage.")]
    [SerializeField] private Image _damageGhostImage;

    [Tooltip("Behind damage ghost. Shows expected health level when consuming a heal.")]
    [SerializeField] private Image _healForwardGhostImage;

    [Header("Damage Ghost Settings")]
    [SerializeField] private float _ghostDelay = 0.15f;
    [SerializeField] private float _ghostSpeed = 4f;

    private CharacterHealthSystem _health;

    // Damage ghost state
    private float _damageGhost01;
    private float _damageGhostTimer;
    private float _lastHealth01;

    // Remaining heal (normalized 0-1).
    // Decreases as heal ticks apply; unaffected by damage so the ghost
    // automatically re-anchors at newHealth + remaining when hurt.
    private float _remainingHeal01;

    private const float EPSILON = 0.001f;

    #endregion

    #region Properties

    public static PlayerHealthBarController Instance { get; private set; }

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        BuffHandler.OnPlayerHealStarted += OnHealStarted;
    }

    private void OnDisable()
    {
        BuffHandler.OnPlayerHealStarted -= OnHealStarted;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SetHealthSystem(null);
    }

    private void LateUpdate()
    {
        if (_health == null) return;

        float current01 = _health.CurrentHealth01;

        TickDamageGhost(current01, Time.deltaTime);
        UpdateDamageGhostBar(current01);
        UpdateHealForwardBar(current01);
    }

    #endregion

    #region Public API

    public void SetHealthSystem(CharacterHealthSystem health)
    {
        if (_health != null)
            _health.OnHealthChanged -= OnHealthChanged;

        _health = health;

        if (_health == null) return;

        _health.OnHealthChanged += OnHealthChanged;
        _damageGhost01    = _health.CurrentHealth01;
        _lastHealth01     = _health.CurrentHealth01;
        _remainingHeal01  = 0f;
        _damageGhostTimer = 0f;
    }

    #endregion

    #region Damage Ghost

    private void OnHealthChanged(float health01)
    {
        float delta = health01 - _lastHealth01;

        if (delta < -EPSILON)
        {
            // Damage: keep ghost at old higher value, reset decay timer.
            // Remaining heal is unchanged — target shifts automatically to newHealth + remaining.
            _damageGhostTimer = 0f;
        }
        else if (delta > EPSILON)
        {
            // Healed: snap damage ghost to current (no damage ghost while healing).
            // Consume the healed amount from the remaining forward heal.
            _damageGhost01    = health01;
            _damageGhostTimer = 0f;
            _remainingHeal01  = Mathf.Max(0f, _remainingHeal01 - delta);
        }

        // At full health no future heal can land — clear any residual forward ghost.
        // Covers: heal while already at max (delta=0), heal that overshoots max,
        // and HoT ticks that arrive after health is already full.
        if (health01 >= 1f - EPSILON)
            _remainingHeal01 = 0f;

        _lastHealth01 = health01;
    }

    private void TickDamageGhost(float current01, float dt)
    {
        if (Mathf.Abs(_damageGhost01 - current01) < EPSILON)
        {
            _damageGhost01 = current01;
            return;
        }

        _damageGhostTimer += dt;
        if (_damageGhostTimer < _ghostDelay) return;

        _damageGhost01 = Mathf.MoveTowards(_damageGhost01, current01, _ghostSpeed * dt);
    }

    private void UpdateDamageGhostBar(float current01)
    {
        if (_damageGhostImage == null) return;

        bool show = _damageGhost01 > current01 + EPSILON;

        if (_damageGhostImage.enabled != show)
            _damageGhostImage.enabled = show;

        if (show)
            _damageGhostImage.fillAmount = _damageGhost01;
    }

    #endregion

    #region Heal Forward Ghost

    private void OnHealStarted(float totalHeal)
    {
        if (_health == null || _health.MaxHealth <= 0f) return;

        // Accumulate remaining heal — multiple buffs stack.
        _remainingHeal01 += totalHeal / _health.MaxHealth;
    }

    private void UpdateHealForwardBar(float current01)
    {
        if (_healForwardGhostImage == null) return;

        if (_remainingHeal01 < EPSILON)
        {
            _remainingHeal01 = 0f;
            if (_healForwardGhostImage.enabled)
                _healForwardGhostImage.enabled = false;
            return;
        }

        // Re-anchor every frame: target moves with current health when damage is taken.
        float target01 = Mathf.Clamp01(current01 + _remainingHeal01);
        bool  show     = target01 > current01 + EPSILON;

        if (_healForwardGhostImage.enabled != show)
            _healForwardGhostImage.enabled = show;

        if (show)
            _healForwardGhostImage.fillAmount = target01;
    }

    #endregion
}