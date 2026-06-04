using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(Animator))]
public class CinematicRootMotionAnchor : MonoBehaviour
{
    #region Fields

    [SerializeField] private bool _applyX = true;
    [SerializeField] private bool _applyY = false;
    [SerializeField] private bool _applyZ = true;
    [SerializeField] private bool _applyRotation = true;

    private Animator _animator;

    private Vector3 _anchorPosition;
    private Quaternion _anchorRotation;
    private Vector3 _animStartPosition;
    private Quaternion _animStartRotation;
    private Quaternion _facingOffset;

    private bool _anchored;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        _anchored = false;
    }

    private void OnAnimatorMove()
    {
        if (_animator == null) return;

        if (!_anchored)
        {
            _anchorPosition = transform.position;
            _anchorRotation = transform.rotation;
            _animStartPosition = _animator.rootPosition;
            _animStartRotation = _animator.rootRotation;
            // How much to rotate the animation trajectory to match the character's facing
            _facingOffset = _anchorRotation * Quaternion.Inverse(_animStartRotation);
            _anchored = true;
        }

        // Re-orient the animation delta from its starting pose into the character's facing direction
        Vector3 animDelta = _animator.rootPosition - _animStartPosition;
        Vector3 oriented = _facingOffset * animDelta;
        Vector3 target = _anchorPosition + oriented;
        Vector3 current = transform.position;

        transform.position = new Vector3(
            _applyX ? target.x : current.x,
            _applyY ? target.y : current.y,
            _applyZ ? target.z : current.z
        );

        if (_applyRotation)
        {
            Quaternion animRotDelta = _animator.rootRotation * Quaternion.Inverse(_animStartRotation);
            transform.rotation = animRotDelta * _anchorRotation;
        }
    }

    #endregion

    #region Public API

    [ContextMenu("Reset Anchor")]
    public void ResetAnchor()
    {
        _anchored = false;
    }

    #endregion
}

#if UNITY_EDITOR
[CustomEditor(typeof(CinematicRootMotionAnchor))]
public class CinematicRootMotionAnchorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        if (GUILayout.Button("Reset Anchor"))
            ((CinematicRootMotionAnchor)target).ResetAnchor();
    }
}
#endif
