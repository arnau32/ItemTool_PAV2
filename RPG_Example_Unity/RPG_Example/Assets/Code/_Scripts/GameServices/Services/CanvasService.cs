using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CanvasService : MonoBehaviour, IGameServices, IShutdownable
{
    [Header("Canvas Rotation Settings")]
    [SerializeField] private float maxDistance = 50f;
    [SerializeField] private float mediumDistance = 25f;
    
    private CanvasRotation _canvasRotation = new CanvasRotation();

    #region Unity Callbacks

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private void Start()
    {
        Initialize();
    }
    private void LateUpdate()
    {
        _canvasRotation.TickLateUpdate();
    }
    public void Shutdown()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    #endregion

    #region Public API

    public void RegisterRotationCanvas(CanvasFaceCamera canvas) => _canvasRotation.Register(canvas);
    public void UnregisterRotationCanvas(CanvasFaceCamera canvas) => _canvasRotation.Unregister(canvas);

    #endregion

    private void Initialize()
    {
        if (Camera.main == null)
        {
            Debug.LogWarning("[CanvasService] No main camera found. Retrying next frame...");
            return;
        }
        var settings = new CanvasRotation.Settings
        {
            maxDistance = maxDistance,
            mediumDistance = mediumDistance
        };
        
        if (Camera.main != null)
        {
            _canvasRotation.Initialize(Camera.main.transform, settings);
        }
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Initialize();
    
}
