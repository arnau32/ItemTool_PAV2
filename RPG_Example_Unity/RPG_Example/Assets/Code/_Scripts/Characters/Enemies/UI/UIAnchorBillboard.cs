using UnityEngine;

/// <summary>
/// Makes this transform always face the camera.
/// Designed for FIXED isometric cameras.
/// Attach to UIAnchor (child of enemy).
/// </summary>
public class UIAnchorBillboard : MonoBehaviour
{
    [SerializeField] private Camera _mainCamera;

    private void Awake()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }
    }

    private void LateUpdate()
    {
        if (_mainCamera == null) return;

        // Copy camera rotation directly (true billboard)
        transform.rotation = _mainCamera.transform.rotation;
    }
}