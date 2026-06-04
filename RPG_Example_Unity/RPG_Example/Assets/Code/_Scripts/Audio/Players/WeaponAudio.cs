using System;
using UnityEngine;

public enum WeaponImpactType { Flesh, Block, Miss }

public class WeaponAudio : MonoBehaviour
{
    [SerializeField] private WeaponSounds _sounds;

    private AudioService _audioService;

    private void Awake()
    {
        _audioService = GameServices.Get<AudioService>();

        if (_sounds == null)
            Debug.LogError($"[WeaponAudio] WeaponSounds not assigned on {gameObject.name}.", this);
    }

    public void PlayImpact(WeaponImpactType type)
    {
        var sound = type switch
        {
            WeaponImpactType.Flesh => _sounds.HitFlesh,
            WeaponImpactType.Block => _sounds.HitBlock,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
        _audioService.PlayOneShot(sound, transform.position);
    }

    public void PlayEquip() => _audioService.PlayOneShot(_sounds.Equip, transform.position);
    public void PlayUnequip() => _audioService.PlayOneShot(_sounds.Unequip, transform.position);
}

