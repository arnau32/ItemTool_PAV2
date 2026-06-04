using UnityEngine;

public class MusicTrigger : MonoBehaviour
{
    [SerializeField] private Enums.MusicArea _area;
    private AudioService _audioService;
    
    private void Awake()
    {
        _audioService = GameServices.Get<AudioService>();
        _audioService.SetMusicArea(_area);
    }
}
