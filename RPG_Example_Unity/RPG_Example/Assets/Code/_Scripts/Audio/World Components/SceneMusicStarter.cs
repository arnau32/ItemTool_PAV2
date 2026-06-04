using UnityEngine;

// Drives the global music state when a scene loads.
//
// NORMAL MODE (ForceRestart = false):
//   Calls AudioService.SetMusicArea() — FMOD handles the crossfade internally.
//
// FORCE RESTART MODE (ForceRestart = true):
//   Stops the current music instance and restarts it at the new area.
//   If FadeOutDuration / FadeInDuration > 0, the swap is crossfaded;
//   otherwise it is an immediate hard stop. Use for scene transitions where
//   the previous music was not driven by AudioService (e.g. OnBoarding → Base).

public class SceneMusicStarter : MonoBehaviour
{
    [Tooltip("Music area to activate when this scene starts.")]
    [SerializeField] private Enums.MusicArea _area;

    [Tooltip("If true, stops the current music instance and restarts it from scratch " +
             "at the new area. Use for hard scene transitions (OnBoarding → Base) where " +
             "a clean break is needed instead of a crossfade.\n\n" +
             "If false, only the MusicState parameter is changed and FMOD crossfades " +
             "internally — use for zones within the same continuous music event.")]
    [SerializeField] private bool _forceRestart = false;

    [Tooltip("Seconds to fade out the old track before the swap. Only used when ForceRestart = true.")]
    [SerializeField] private float _fadeOutDuration = 1f;

    [Tooltip("Seconds to fade in the new track after the swap. Only used when ForceRestart = true.")]
    [SerializeField] private float _fadeInDuration  = 1f;

    [SerializeField] private float _delaySeconds = 0f;

    private AudioService _audio;

    private void Start()
    {
        Cursor.visible = false;

        if (!GameServices.TryGet<AudioService>(out _audio))
        {
            Debug.LogWarning("[SceneMusicStarter] AudioService not found. Make sure GameBootstrap has run before this scene loads.", this);
            return;
        }

        if (_delaySeconds <= 0f)
        {
            ApplyMusic();
            return;
        }

        Invoke(nameof(ApplyMusic), _delaySeconds);
    }

    private void ApplyMusic()
    {
        _audio.StartBackgroundAudio();

        if (_forceRestart)
        {
            if (_fadeOutDuration > 0f || _fadeInDuration > 0f)
                _audio.RestartMusicAtAreaFaded(_area, _fadeOutDuration, _fadeInDuration);
            else
                _audio.RestartMusicAtArea(_area);
        }
        else
        {
            _audio.SetMusicArea(_area);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Preview: Set Music Now")]
    private void PreviewSetMusic()
    {
        if (!Application.isPlaying) return;
        if (!GameServices.TryGet(out _audio)) return;
        ApplyMusic();
    }
#endif
}