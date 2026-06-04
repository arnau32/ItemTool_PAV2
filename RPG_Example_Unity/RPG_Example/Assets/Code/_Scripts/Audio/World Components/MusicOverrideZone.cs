using FMODUnity;
using UnityEngine;

// Plays a completely separate FMOD event while the player is inside the zone.
// On exit, fades back to whatever music was playing before.
//
// Use this for locations that need their own track (Viewer areas, boss rooms, etc.)
// For music that shares a single FMOD event with a MusicState parameter, use MusicZone instead.
//
// Setup:
//   1. Add a BoxCollider (isTrigger = true) to this GameObject.
//   2. Assign the FMOD event you want to play in Override Music.
//   3. Tune fade in/out durations in the Inspector.
public class MusicOverrideZone : TriggerPlayer
{
    [SerializeField] private EventReference _overrideMusic;
    [SerializeField] private float          _fadeInDuration  = 1f;
    [SerializeField] private float          _fadeOutDuration = 1.5f;

    private AudioService _audioService;
    private bool         _playerInZone;

    private void Awake()
    {
        _audioService = GameServices.Get<AudioService>();
    }

    protected override void OnPlayerTriggerEnter(Collider other)
    {
        if (_playerInZone) return;
        _playerInZone = true;
        _audioService.PlayMusicOverride(_overrideMusic, _fadeInDuration);
    }

    protected override void OnPlayerTriggerExit(Collider other)
    {
        if (!_playerInZone) return;
        _playerInZone = false;
        _audioService.StopMusicOverride(_fadeOutDuration);
    }
}
