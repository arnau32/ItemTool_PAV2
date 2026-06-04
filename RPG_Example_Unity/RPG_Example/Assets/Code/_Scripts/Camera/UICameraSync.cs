using UnityEngine;

/// Keeps a UI overlay camera in sync with a source (main) camera's projection
/// settings so that FOV kicks, lens shifts, and other feedback-driven changes
/// don't create a visual mismatch between the world and the UI layer.
///
/// Attach to the UI camera GameObject.
/// Only needed when the UI Canvas is in Screen Space - Camera mode.
/// For flat HUDs, prefer Screen Space - Overlay (no second camera required).
[RequireComponent(typeof(Camera))]
public class UICameraSync : MonoBehaviour
{
    [SerializeField] private Camera _sourceCamera;

    private Camera _uiCamera;

    private void Awake()
    {
        _uiCamera = GetComponent<Camera>();

        if (_sourceCamera == null && transform.parent != null)
            _sourceCamera = transform.parent.GetComponentInParent<Camera>();
    }

    private void LateUpdate()
    {
        if (_sourceCamera == null || _uiCamera == null) return;

        _uiCamera.fieldOfView         = _sourceCamera.fieldOfView;
        _uiCamera.orthographicSize     = _sourceCamera.orthographicSize;
        _uiCamera.lensShift            = _sourceCamera.lensShift;
        _uiCamera.focalLength          = _sourceCamera.focalLength;
        _uiCamera.usePhysicalProperties = _sourceCamera.usePhysicalProperties;
    }
}
