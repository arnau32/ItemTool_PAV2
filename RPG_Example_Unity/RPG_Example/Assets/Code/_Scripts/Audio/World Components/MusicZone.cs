using UnityEngine;

// Attach to any zone trigger to change the global music state.
//
//   MusicZone    → changes the global _musicEventInstance parameter (one instance, whole game)
//   ZoneAmbience → starts/stops zone-specific ambience loops (many instances, per-zone)
//
// Example setup for a dungeon zone:
//   GameObject: "DungeonZone"
//   - BoxCollider (isTrigger=true)
//   - MusicZone (area = MusicArea.Dungeon)
//   - ZoneAmbience (event = event:/Ambience/Dungeon_Drips)
//
// When player enters:
//   MusicZone    → AudioService.SetMusicArea(Dungeon) → FMOD crossfades from Overworld to Dungeon
//   ZoneAmbience → Starts dripping water loop specific to this dungeon
public class MusicZone : TriggerPlayer
{
    [SerializeField] private Enums.MusicArea _area;

    [Header("On Exit")]
    [SerializeField] private bool            _revertOnExit;
    [SerializeField] private Enums.MusicArea _revertArea;

    private AudioService _audioService;
    private bool _playerInZone;

    private void Awake()
    {
        _audioService = GameServices.Get<AudioService>();
    }

    protected override void OnPlayerTriggerEnter(Collider other)
    {
        if (_playerInZone) return;
        _playerInZone = true;
        _audioService.SetMusicArea(_area);
    }

    protected override void OnPlayerTriggerExit(Collider other)
    {
        if (!_playerInZone) return;
        _playerInZone = false;

        if (_revertOnExit)
            _audioService.SetMusicArea(_revertArea);
    }

    public void ForceSetMusic() => _audioService.SetMusicArea(_area);
}