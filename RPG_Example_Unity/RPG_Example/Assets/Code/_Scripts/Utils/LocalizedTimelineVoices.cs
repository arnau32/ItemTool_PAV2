using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DefaultExecutionOrder(-100)]
public class LocalizedTimelineVoices : MonoBehaviour
{
    [SerializeField] private PlayableDirector _director;
    [SerializeField] private int _englishTrackIndex;
    [SerializeField] private int _spanishTrackIndex;

    private TrackAsset _mutedTrack;

    private void Awake()
    {
        _director.playOnAwake = false;
    }

    private IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;

        var timeline = _director.playableAsset as TimelineAsset;
        if (timeline == null) yield break;

        // Read from SettingsService (loaded synchronously from disk in Awake) to avoid the race
        // condition where LocalizationSettings.SelectedLocale is still "en" while SettingsService
        // is mid-async-applying the saved language.
        var settingsService = GameServices.Get<SettingsService>();
        bool isSpanish = settingsService == null || settingsService.Language != GameLanguage.Spanish;

        var tracks = timeline.GetOutputTracks().ToArray();

#if UNITY_EDITOR
        for (int i = 0; i < tracks.Length; i++)
            Debug.Log($"[LocalizedTimelineVoices] [{i}] {tracks[i].name} | language={settingsService?.Language}");
#endif

        // Mute the track we do NOT want: Spanish → mute English, English/Catalan → mute Spanish.
        int trackToMute = isSpanish ? _englishTrackIndex : _spanishTrackIndex;

        if (trackToMute >= 0 && trackToMute < tracks.Length)
        {
            _mutedTrack       = tracks[trackToMute];
            _mutedTrack.muted = true;
        }

        _director.Play();
    }

    private void OnDestroy()
    {
        if (_mutedTrack != null)
            _mutedTrack.muted = false;
    }
}