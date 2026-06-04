using System.Collections;
using UnityEngine;

public class DoorInteractable : BaseInteractable, ISaveable
{
    public enum OpenMode { Rotation, Lift }

    #region Fields

    [Header("Identity")]
    [SerializeField] private string _doorId;

    [Header("Behaviour")]
    [SerializeField] private OpenMode _openMode = OpenMode.Rotation;
    [Tooltip("If true the door can be opened and closed repeatedly. " +
             "If false it opens once and stays open forever.")]
    [SerializeField] private bool _isMutable = true;

    [Header("First Door")]
    [SerializeField] private Transform _doorA;
    [Tooltip("Target world-space rotation when door A is fully open (Rotation mode).")]
    [SerializeField] private Vector3 _openRotationEulerA;

    [Header("Second Door (optional)")]
    [Tooltip("Leave empty for a single door.")]
    [SerializeField] private Transform _doorB;
    [Tooltip("Target world-space rotation when door B is fully open (Rotation mode).")]
    [SerializeField] private Vector3 _openRotationEulerB;

    [Header("Lift Mode")]
    [Tooltip("How many units both doors travel upward when opened.")]
    [SerializeField] private float _liftHeight = 3f;

    [Header("Animation")]
    [SerializeField] private float _speed = 2f;

    private DoorState _stateA;
    private DoorState _stateB;

    private bool      _isOpen;
    private Coroutine _moveRoutine;

    #endregion

    #region Nested

    private struct DoorState
    {
        public Quaternion closedRotation;
        public Quaternion openRotation;
        public Vector3    closedPosition;
        public Vector3    openPosition;
    }

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (_doorA != null)
            _stateA = BuildState(_doorA, _openRotationEulerA);

        if (_doorB != null)
            _stateB = BuildState(_doorB, _openRotationEulerB);

        if (GameServices.TryGet<SaveService>(out var save))
            save.RegisterSaveable(this);
    }

    private void OnDestroy()
    {
        if (!GameServices.TryGet<SaveService>(out var save)) return;

        // Pre-capture before unregistering: OnDestroy fires BEFORE sceneUnloaded, so by
        // the time SaveService.CaptureAll() runs the door is already gone from _saveables.
        // Writing directly into CurrentSave here ensures the state survives that gap.
        CaptureToSave(save.CurrentSave);
        save.UnregisterSaveable(this);
    }

    #endregion

    #region IInteractable

    public override void OnInteract()
    {
        if (!_isMutable && _isOpen) return;

        base.OnInteract();

        _isOpen = !_isOpen;
        StartMove(_isOpen);
    }

    #endregion

    #region ISaveable

    public void CaptureToSave(SaveData data)
    {
        if (string.IsNullOrEmpty(_doorId)) return;

        var entries = data.doors.entries;
        DoorSaveEntry entry = null;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].doorId == _doorId)
            {
                entry = entries[i];
                break;
            }
        }

        if (entry == null)
        {
            entry = new DoorSaveEntry { doorId = _doorId };
            entries.Add(entry);
        }

        entry.isOpen = _isOpen;
    }

    public void ApplyFromSave(SaveData data)
    {
        if (string.IsNullOrEmpty(_doorId)) return;

        var entries = data.doors.entries;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].doorId != _doorId) continue;

            _isOpen = entries[i].isOpen;
            SnapToState(_isOpen);
            return;
        }
    }

    #endregion

    #region Movement

    private void StartMove(bool open)
    {
        if (_moveRoutine != null)
            StopCoroutine(_moveRoutine);

        _moveRoutine = StartCoroutine(MoveRoutine(open));
    }

    private IEnumerator MoveRoutine(bool open)
    {
        Quaternion fromRotA = _doorA != null ? _doorA.rotation : Quaternion.identity;
        Quaternion fromRotB = _doorB != null ? _doorB.rotation : Quaternion.identity;
        Vector3    fromPosA = _doorA != null ? _doorA.position : Vector3.zero;
        Vector3    fromPosB = _doorB != null ? _doorB.position : Vector3.zero;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * _speed;
            float s = Mathf.Clamp01(t);

            if (_openMode == OpenMode.Rotation)
            {
                if (_doorA != null)
                    _doorA.rotation = Quaternion.Lerp(fromRotA, open ? _stateA.openRotation : _stateA.closedRotation, s);

                if (_doorB != null)
                    _doorB.rotation = Quaternion.Lerp(fromRotB, open ? _stateB.openRotation : _stateB.closedRotation, s);
            }
            else
            {
                if (_doorA != null)
                    _doorA.position = Vector3.Lerp(fromPosA, open ? _stateA.openPosition : _stateA.closedPosition, s);

                if (_doorB != null)
                    _doorB.position = Vector3.Lerp(fromPosB, open ? _stateB.openPosition : _stateB.closedPosition, s);
            }

            yield return null;
        }

        ApplyFinalState(open);
        _moveRoutine = null;
    }

    private void SnapToState(bool open)
    {
        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }

        ApplyFinalState(open);
    }

    private void ApplyFinalState(bool open)
    {
        if (_openMode == OpenMode.Rotation)
        {
            if (_doorA != null)
                _doorA.rotation = open ? _stateA.openRotation : _stateA.closedRotation;

            if (_doorB != null)
                _doorB.rotation = open ? _stateB.openRotation : _stateB.closedRotation;
        }
        else
        {
            if (_doorA != null)
                _doorA.position = open ? _stateA.openPosition : _stateA.closedPosition;

            if (_doorB != null)
                _doorB.position = open ? _stateB.openPosition : _stateB.closedPosition;
        }
    }

    #endregion

    #region Helpers

    private DoorState BuildState(Transform door, Vector3 openEuler)
    {
        return new DoorState
        {
            closedRotation = door.rotation,
            openRotation   = Quaternion.Euler(openEuler),
            closedPosition = door.position,
            openPosition   = door.position + Vector3.up * _liftHeight,
        };
    }

    #endregion
}
