using System;
using UnityEngine;

public class AbilityScoreSystem
{
    private PlayerContext _context;
    private float _currentScore;
    private float _maxScore;

    public float CurrentScore01 => _maxScore <= 0f ? 0f : Mathf.Clamp01(_currentScore / _maxScore);
    public float MaxScore => _maxScore;
    public bool CanUseAbility => _maxScore > 0f && _currentScore >= _maxScore;

    // Fires once when CanUseAbility transitions false → true.
    public static event Action OnAbilityReady;
    // Fires every time the ability is consumed.
    public static event Action OnAbilityUsed;

    public void Initialize(PlayerContext ctx)
    {
        Shutdown();

        _context = ctx;

        if (_context != null && _context.EquipmentHandler != null)
        {
            _context.EquipmentHandler.OnEquipWeapon += SetWeaponScore;
        }
    }

    public void Shutdown()
    {
        if (_context != null && _context.EquipmentHandler != null)
        {
            _context.EquipmentHandler.OnEquipWeapon -= SetWeaponScore;
        }

        _context = null;
    }

    public void SetWeaponScore(WeaponData data)
    {
        bool wasReady = CanUseAbility;

        _maxScore = data.skillScoreNeeded;

        // During onboarding, preserve a charged ability across weapon switches.
        // The tutorial hands the player a weapon after charging, and losing the
        // charge mid-tutorial breaks the flow entirely.
        if (wasReady && IsOnboardingActive())
        {
            _currentScore = _maxScore;
            _context.Hud.UpdateSkill(CurrentScore01, CanUseAbility);
            return;
        }

        _currentScore = 0f;
        _context.Hud.ResetSkill();
        OnAbilityUsed?.Invoke();
    }

    public void AddScore(float v)
    {
        bool wasReady = CanUseAbility;
        _currentScore += v;
        ClampScore();
        _context.Hud.UpdateSkill(CurrentScore01, CanUseAbility);

        if (!wasReady && CanUseAbility)
            OnAbilityReady?.Invoke();
    }

    public void SetMaxScore(float sc)
    {
        _maxScore = sc;
        ClampScore();
        _context.Hud.UpdateSkill(CurrentScore01, CanUseAbility);
    }

    public void UseAbility()
    {
        _currentScore = 0;
        _context.Hud.UpdateSkill(CurrentScore01, CanUseAbility);
        OnAbilityUsed?.Invoke();
    }

    private void ClampScore()
    {
        if (_currentScore >= _maxScore)
        {
            _currentScore = _maxScore;
        }
    }

    private static bool IsOnboardingActive()
    {
        return GameServices.TryGet<SaveService>(out var save)
            && !save.CurrentSave.meta.onboardingCompleted;
    }
}
