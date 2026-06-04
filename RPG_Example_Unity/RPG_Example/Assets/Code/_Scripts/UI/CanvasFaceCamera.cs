using UnityEngine;

public class CanvasFaceCamera : MonoBehaviour
{
    private Transform _parentTransform;
    private int _frameSkipCounter;
    
    private void Awake()
    {
        _parentTransform = transform.parent;
    }

    private void OnEnable()
    {
        if (GameServices.TryGet<CanvasService>(out var service))
        {
            service.RegisterRotationCanvas(this);
        }
    }

    private void OnDisable()
    {
        if (GameServices.TryGet<CanvasService>(out var service))
        {
            service.UnregisterRotationCanvas(this);
        }
    }

    public void UpdateRotation(Quaternion cameraRotation, Vector3 cameraPosition, float sqrMaxDistance, float sqrMediumDistance)
    {
        // Distance culling
        float sqrDistance = (transform.position - cameraPosition).sqrMagnitude;
        
        if (sqrDistance > sqrMaxDistance) return;
        
        // Far canvas will update with less fqcy
        if (sqrDistance > sqrMediumDistance)
        {
            _frameSkipCounter++;
            if (_frameSkipCounter < 2) return;
            _frameSkipCounter = 0;
        }

        if (_parentTransform != null)
        {
            transform.localRotation = Quaternion.Inverse(_parentTransform.rotation) * cameraRotation;
        }
        else
        {
            transform.rotation = cameraRotation;
        }
    }
}
