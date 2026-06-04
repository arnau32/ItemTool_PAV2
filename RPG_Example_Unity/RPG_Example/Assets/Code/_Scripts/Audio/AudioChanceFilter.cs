using UnityEngine;

/// <summary>
/// Probabilistic gate for optional audio cues.
/// Combines a random chance check with a minimum cooldown to prevent
/// both annoying repetition and complete silence.
///
/// Designed for voices/grunts that should fire "sometimes but not always":
///   - Dodge grunts
///   - Hit reaction voices
///   - Idle ambient vocalizations
///   - Light attack effort sounds
///
/// Usage:
///   // Field — one per sound type.
///   private AudioChanceFilter _dodgeVoiceFilter = new AudioChanceFilter(chance: 0.4f, cooldown: 1.2f);
///
///   // Call site.
///   if (_dodgeVoiceFilter.Roll())
///       _audioService.PlayOneShot(_sounds.DodgeVoice, position);
/// </summary>
[System.Serializable]
public struct AudioChanceFilter
{
    [Tooltip("Probability (0–1) that the sound fires when Roll() is called. 0 = never, 1 = always. Typical voice grunt: 0.3–0.5.")]
    [Range(0f, 1f)]
    public float chance;

    [Tooltip("Minimum seconds that must pass before this filter can fire again. Prevents the same grunt playing twice in quick succession.")]
    [Min(0f)]
    public float cooldownSeconds;

    private float _lastFireTime;

    public AudioChanceFilter(float chance, float cooldown)
    {
        this.chance = Mathf.Clamp01(chance);
        cooldownSeconds = Mathf.Max(0f, cooldown);
        _lastFireTime = float.NegativeInfinity;
    }

    /// Returns true if the sound should play this time.
    /// Internally checks cooldown then chance. Updates internal state on success.
    public bool Roll()
    {
        if (Time.time - _lastFireTime < cooldownSeconds) return false;
        if (Random.value > chance) return false;

        _lastFireTime = Time.time;
        return true;
    }

    /// Same as Roll() but allows overriding the chance for special cases.
    /// Example: perfect parry always fires the voice, normal parry uses the default chance.
    ///   if (_parryVoiceFilter.Roll(isGuaranteed ? 1f : 0.4f)) PlayVoice();
    public bool Roll(float overrideChance)
    {
        if (Time.time - _lastFireTime < cooldownSeconds) return false;
        if (Random.value > Mathf.Clamp01(overrideChance)) return false;

        _lastFireTime = Time.time;
        return true;
    }

    /// Resets the cooldown so the next Roll() is never blocked by it.
    /// Useful after a long silence (e.g. player respawn, cutscene end).
    public void ResetCooldown() => _lastFireTime = float.NegativeInfinity;
}