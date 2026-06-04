using System.Collections.Generic;
using UnityEngine;

public class CanvasRotation
{
    [System.Serializable]
    public struct Settings
    {
        public float maxDistance;
        public float mediumDistance;
        
        public static Settings Default => new Settings { maxDistance = 50f, mediumDistance = 25f };
    }
    
    private Settings _settings;
    private Transform _cameraTransform;
    private Quaternion _cameraRotation;
    private Vector3 _cameraPosition;
    
    private readonly List<CanvasFaceCamera> _activeCanvas = new List<CanvasFaceCamera>(128);
    
    private float _sqrMaxDistance;
    private float _sqrMediumDistance;

    public void Initialize(Transform camTransform, Settings? settings = null)
    {
        _cameraTransform = camTransform;
        _settings = settings ?? Settings.Default;
        
        _sqrMaxDistance = _settings.maxDistance * _settings.maxDistance;
        _sqrMediumDistance = _settings.mediumDistance * _settings.mediumDistance;
    }

    public void TickLateUpdate()
    {
        if (_cameraTransform == null) return;

        _cameraRotation = _cameraTransform.rotation;
        _cameraPosition = _cameraTransform.position;
        
        // Backwards iteration to delete safety
        for (var i = _activeCanvas.Count - 1; i >= 0; i--)
        {
            var canvas = _activeCanvas[i];
            
            // Null check & clean
            if (canvas == null)
            {
                _activeCanvas.RemoveAt(i);
                continue;
            }
            
            canvas.UpdateRotation(_cameraRotation, _cameraPosition, _sqrMaxDistance, _sqrMediumDistance);
        }
    }

    public void Register(CanvasFaceCamera canvas)
    {
        if (canvas == null || _activeCanvas.Contains(canvas)) return;
        _activeCanvas.Add(canvas);
    }

    public void Unregister(CanvasFaceCamera canvas)
    {
        if (canvas == null) return;
        _activeCanvas.Remove(canvas);
    }
    
    public int GetActiveCount() => _activeCanvas.Count;
}