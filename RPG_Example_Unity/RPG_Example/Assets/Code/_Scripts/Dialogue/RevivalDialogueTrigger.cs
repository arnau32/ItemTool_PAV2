using System.Collections;
using FMODUnity;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

// Fires Aldwyn's revival dialogue the first time the player returns to the base after dying.
// Also plays an optional VFX and audio at the player's position on arrival.
// Place this component anywhere in the Village scene.
// Assign _aldwynGraph (Aldwyn.asset) in the inspector — the node ID is already set by default.
public class RevivalDialogueTrigger : MonoBehaviour
{
    #region Fields

    [SerializeField] private DialogueGraph _aldwynGraph;

    [Tooltip("Node ID of Revivir_0 inside the Aldwyn dialogue graph.")]
    [SerializeField] private string _revivalNodeId = "82951e87";

    [Header("Feedback")]
    [Tooltip("Particle system prefab spawned at the player's position on revival. Optional.")]
    [SerializeField] private ParticleSystem _revivalVFXPrefab;

    [Tooltip("Seconds before the VFX stops emitting. 0 = particle system runs its own duration.")]
    [SerializeField] private float _vfxDuration = 3f;

    [Tooltip("FMOD event played at the player's position on revival. Optional.")]
    [SerializeField] private EventReference _revivalSound;

    [Tooltip("Seconds before the audio fades out and stops. 0 = FMOD event plays its full duration (fire-and-forget).")]
    [SerializeField] private float _audioDuration = 3f;

    [Header("Timing")]
    [Tooltip("Seconds after scene load before playing FX and showing the dialogue.")]
    [SerializeField] private float _delay = 1f;

    private PlayerController _cachedPlayer;

    #endregion

    #region Unity Callbacks

    private IEnumerator Start()
    {
        if (!RespawnData.CameFromDeath) yield break;
        if (_aldwynGraph == null)
        {
            Debug.LogWarning("[RevivalDialogueTrigger] _aldwynGraph is not assigned in the inspector.", this);
            yield break;
        }

        // Wait one frame so GameServices and SaveService finish initializing
        // before reading CurrentSave. Without this, TryGet can return false
        // when the Village scene loads immediately after a death.
        yield return null;

        if (!GameServices.TryGet<SaveService>(out var save)) yield break;
        if (save.CurrentSave.meta.hasSeenRevivalDialogue) yield break;

        RespawnData.CameFromDeath = false;

        yield return new WaitForSeconds(_delay);
        yield return new WaitUntil(() => UIManager.Instance != null && UIManager.Instance.AllUIInitFinish);

        TriggerRevival();
    }

    #endregion

    #region Private

    private void TriggerRevival()
    {
        if (!GameServices.TryGet<SaveService>(out var save)) return;

        save.CurrentSave.meta.hasSeenRevivalDialogue = true;

        _cachedPlayer = FindAnyObjectByType<PlayerController>();

        PlayFeedback();

        _cachedPlayer?.Context?.PlayableController?.EnterDialogue();
        GameServices.Get<InputService>().OnUIOpen();
        DialogueManager.Instance.OnDialogueEnd += HandleDialogueEnded;
        DialogueManager.Instance.StartDialogueFromNode(_aldwynGraph, _revivalNodeId);
    }

    private void PlayFeedback()
    {
        Vector3 position = _cachedPlayer != null
            ? _cachedPlayer.transform.position
            : transform.position;

        PlayVFX(position);
        PlayAudio(position);
    }

    private void PlayVFX(Vector3 position)
    {
        if (_revivalVFXPrefab == null) return;
        if (!GameServices.TryGet<VfxPoolService>(out var vfxPool)) return;

        ParticleSystem ps = vfxPool.Spawn(_revivalVFXPrefab, position, Quaternion.identity);

        if (_vfxDuration > 0f && ps != null)
            WaitExtension.Wait(_vfxDuration, () => ps.Stop(true, ParticleSystemStopBehavior.StopEmitting));
    }

    private void PlayAudio(Vector3 position)
    {
        if (_revivalSound.IsNull) return;

        if (_audioDuration > 0f)
        {
            FMOD.Studio.EventInstance instance = RuntimeManager.CreateInstance(_revivalSound);
            instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
            instance.start();

            WaitExtension.Wait(_audioDuration, () =>
            {
                instance.stop(STOP_MODE.ALLOWFADEOUT);
                instance.release();
            });
        }
        else if (GameServices.TryGet<AudioService>(out var audio))
        {
            audio.PlayOneShot(_revivalSound, position);
        }
    }

    private void HandleDialogueEnded()
    {
        DialogueManager.Instance.OnDialogueEnd -= HandleDialogueEnded;
        _cachedPlayer?.Context?.PlayableController?.ExitDialogue();
        _cachedPlayer = null;
    }

    #endregion
}
