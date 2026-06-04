using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

// Shuffle-bag random selector for a list of EventReferences.
// Shuffle bag plays all N clips before repeating any, guaranteeing perceptual variety.
[System.Serializable]
public class RandomAudioClips
{
    [SerializeField] private List<EventReference> _clips = new();

    private readonly List<int> _remaining = new();

    public bool IsEmpty => _clips == null || _clips.Count == 0;

    // Plays a random clip via AudioService. Guaranteed no immediate repeat
    // unless there is only one clip in the list.
    public void PlayOneShot(AudioService audioService, Vector3 position)
    {
        if (IsEmpty) return;

        int index = NextIndex();
        audioService.PlayOneShot(_clips[index], position);
    }

    // Returns the next index using shuffle-bag logic.
    private int NextIndex()
    {
        // Refill when all clips have been played once.
        if (_remaining.Count == 0)
        {
            Refill();
        }

        // Pick random from remaining, remove it to avoid repeat this cycle.
        int slot  = Random.Range(0, _remaining.Count);
        int index = _remaining[slot];
        _remaining.RemoveAt(slot);

        return index;
    }

    private void Refill()
    {
        _remaining.Clear();
        for (int i = 0; i < _clips.Count; i++)
        {
            _remaining.Add(i);
        }
    }
}