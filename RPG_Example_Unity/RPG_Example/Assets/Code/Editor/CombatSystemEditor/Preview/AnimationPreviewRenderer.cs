using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reusable component for rendering animation clip previews in editor windows.
/// Uses Unity's internal preview system to render a GameObject with an animation.
/// 
/// WHY: Provides visual feedback without requiring the Scene view to be visible.
/// Integrates cleanly into custom inspector layouts.
/// 
/// ENHANCEMENTS:
/// - Ground grid for spatial reference (helps visualize root motion)
/// - Motion trail showing character movement path
/// - Visual speed indicators (affected by speed curves)
/// - Forward direction indicator
/// - Frame information overlay
/// </summary>
internal sealed class AnimationPreviewRenderer : IDisposable
{
    #region Fields

    private PreviewRenderUtility _previewUtility;
    private GameObject _previewInstance;
    private Animator _previewAnimator;
    
    private Vector2 _drag = new Vector2(150f, -30f);
    private float _zoom = 1.0f;
    
    private const float ZOOM_MIN = 0.3f;
    private const float ZOOM_MAX = 3.0f;
    private const float DRAG_SENSITIVITY = 0.3f;

    // Grid settings
    private const float GRID_SIZE = 10f;
    private const int GRID_DIVISIONS = 20;
    private static readonly Color GRID_COLOR_MAJOR = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    private static readonly Color GRID_COLOR_MINOR = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    
    // Motion trail
    private readonly System.Collections.Generic.List<Vector3> _motionTrail = 
        new System.Collections.Generic.List<Vector3>(64);
    private const int MAX_TRAIL_POINTS = 60;
    
    // Root motion tracking
    private Vector3 _lastRootPosition;
    private bool _hasRecordedInitialPosition;
    
    // Input handling
    private bool _isDragging;

    #endregion

    #region Properties

    public bool IsInitialized => _previewUtility != null && _previewInstance != null;
    
    // Options that can be toggled from the editor
    public bool ShowGrid { get; set; } = true;
    public bool ShowMotionTrail { get; set; } = true;
    public bool ShowForwardDirection { get; set; } = true;
    public bool ShowVelocityIndicator { get; set; } = true;
    public bool ShowFrameInfo { get; set; } = true;

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the preview renderer with a source GameObject.
    /// Creates an isolated preview scene with a copy of the GameObject.
    /// 
    /// WHY: We need a separate instance to avoid affecting the actual scene GameObject.
    /// PreviewRenderUtility provides an isolated rendering environment.
    /// </summary>
    public void Initialize(GameObject sourceGameObject)
    {
        if (sourceGameObject == null) return;

        Cleanup();

        // Create preview utility with proper camera setup
        _previewUtility = new PreviewRenderUtility();
        _previewUtility.camera.fieldOfView = 30f;
        _previewUtility.camera.nearClipPlane = 0.3f;
        _previewUtility.camera.farClipPlane = 1000f;
        _previewUtility.camera.transform.position = new Vector3(0, 1, -5);
        _previewUtility.camera.transform.LookAt(Vector3.up);
        
        // Better background color for visibility
        _previewUtility.camera.backgroundColor = new Color(0.15f, 0.15f, 0.18f, 1f);
        _previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;

        // Clone the GameObject into the preview scene
        // WHY: We instantiate a copy to avoid any side effects on the original
        _previewInstance = UnityEngine.Object.Instantiate(sourceGameObject);
        _previewInstance.hideFlags = HideFlags.HideAndDontSave;
        _previewUtility.AddSingleGO(_previewInstance);

        _previewAnimator = _previewInstance.GetComponent<Animator>();
        
        // Disable unnecessary components for preview
        // WHY: We only want visual rendering, not gameplay logic
        var colliders = _previewInstance.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
            col.enabled = false;

        var rigidbodies = _previewInstance.GetComponentsInChildren<Rigidbody>();
        foreach (var rb in rigidbodies)
            rb.isKinematic = true;
            
        // Reset motion trail
        _motionTrail.Clear();
        _hasRecordedInitialPosition = false;
    }

    #endregion

    #region Rendering

    /// <summary>
    /// Renders the preview into a GUI rect and returns the rendered texture.
    /// Handles camera positioning, lighting, and user interaction (drag/zoom).
    /// 
    /// WHY: This is the core rendering loop. We update the preview based on
    /// the current animation state and render it to a texture for GUI display.
    /// 
    /// CRITICAL FIX: We check if AnimationMode is active before sampling.
    /// The preview renderer doesn't control AnimationMode - the parent editor does.
    /// </summary>
    public Texture RenderPreview(Rect rect, AnimationClip clip, float normalizedTime, 
        System.Func<float, float> speedCurveEval = null)
    {
        if (!IsInitialized) return null;

        // CRITICAL: Handle input BEFORE rendering
        // WHY: Input must be processed during the correct event phase
        HandlePreviewInput(rect);

        // Sample the animation at the current time
        // WHY: We manually sample instead of playing to have precise control
        // FIX: Only sample if AnimationMode is active. If not, just render last state.
        if (clip != null && _previewInstance != null && AnimationMode.InAnimationMode())
        {
            float time = Mathf.Clamp01(normalizedTime) * clip.length;
            
            // No need to call BeginSampling/EndSampling here
            // WHY: The parent editor (AttackWindowEditor) already manages AnimationMode
            // and calls SampleAnimationClip during its preview update cycle.
            // We just render the current state.
            AnimationMode.SampleAnimationClip(_previewInstance, clip, time);
            
            // Track root motion for trail
            UpdateMotionTrail();
        }

        // Position camera based on character bounds
        PositionCamera();

        // Setup lighting
        // WHY: Good lighting is essential for visibility. We use a 3-point lighting setup.
        _previewUtility.lights[0].intensity = 1.0f;
        _previewUtility.lights[0].transform.rotation = Quaternion.Euler(50f, 50f, 0f);
        _previewUtility.lights[1].intensity = 0.5f;
        _previewUtility.lights[1].transform.rotation = Quaternion.Euler(-20f, -30f, 0f);

        // Begin rendering
        _previewUtility.BeginPreview(rect, GUIStyle.none);

        // Draw ground grid BEFORE rendering the character
        // WHY: We want the grid behind the character for depth perception
        if (ShowGrid)
        {
            DrawGroundGrid();
        }

        // Render the preview scene
        // WHY: This actually draws the GameObject with the current animation state
        _previewUtility.camera.Render();
        
        // Draw overlays AFTER rendering (using Handles in camera space)
        if (ShowMotionTrail && _motionTrail.Count > 1)
        {
            DrawMotionTrail();
        }
        
        if (ShowForwardDirection)
        {
            DrawForwardDirectionIndicator();
        }
        
        if (ShowVelocityIndicator && speedCurveEval != null)
        {
            float currentSpeed = speedCurveEval(normalizedTime);
            DrawVelocityIndicator(currentSpeed);
        }
        
        // Draw frame info overlay
        if (ShowFrameInfo && clip != null)
        {
            DrawFrameInfo(clip, normalizedTime);
        }

        // End and return the rendered texture
        return _previewUtility.EndPreview();
    }

    #endregion

    #region Visual Helpers

    /// <summary>
    /// Draws a ground grid to help visualize root motion and spatial positioning.
    /// 
    /// WHY: Without spatial reference, it's hard to see how much the character
    /// moves during an animation. The grid provides visual context.
    /// </summary>
    private void DrawGroundGrid()
    {
        Handles.matrix = Matrix4x4.identity;
        
        float halfSize = GRID_SIZE * 0.5f;
        float step = GRID_SIZE / GRID_DIVISIONS;
        
        // Draw grid lines
        // WHY: We use Handles instead of GL because Handles work correctly
        // with PreviewRenderUtility's camera setup
        for (int i = 0; i <= GRID_DIVISIONS; i++)
        {
            float pos = -halfSize + (i * step);
            
            // Determine if this is a major line (every 5th line)
            bool isMajor = (i % 5 == 0);
            Handles.color = isMajor ? GRID_COLOR_MAJOR : GRID_COLOR_MINOR;
            
            // Horizontal lines (along Z axis)
            Handles.DrawLine(
                new Vector3(-halfSize, 0f, pos),
                new Vector3(halfSize, 0f, pos)
            );
            
            // Vertical lines (along X axis)
            Handles.DrawLine(
                new Vector3(pos, 0f, -halfSize),
                new Vector3(pos, 0f, halfSize)
            );
        }
        
        // Draw origin marker (thicker cross at 0,0)
        // WHY: Helps identify the starting position of the animation
        Handles.color = new Color(0.5f, 0.8f, 1f, 0.9f);
        float markerSize = step * 2f;
        Handles.DrawLine(
            new Vector3(-markerSize, 0.01f, 0f),
            new Vector3(markerSize, 0.01f, 0f),
            3f
        );
        Handles.DrawLine(
            new Vector3(0f, 0.01f, -markerSize),
            new Vector3(0f, 0.01f, markerSize),
            3f
        );
    }

    /// <summary>
    /// Tracks and displays the character's movement path during the animation.
    /// 
    /// WHY: Shows root motion trajectory, making it easy to see if the character
    /// moves forward, backward, or in a curve during the animation.
    /// </summary>
    private void UpdateMotionTrail()
    {
        if (_previewInstance == null) return;
        
        Vector3 currentPos = _previewInstance.transform.position;
        
        if (!_hasRecordedInitialPosition)
        {
            _lastRootPosition = currentPos;
            _hasRecordedInitialPosition = true;
            _motionTrail.Clear();
            _motionTrail.Add(currentPos);
            return;
        }
        
        // Only add point if moved significantly
        // WHY: Prevents trail from becoming cluttered with tiny movements
        float distanceMoved = Vector3.Distance(currentPos, _lastRootPosition);
        if (distanceMoved > 0.01f)
        {
            _motionTrail.Add(currentPos);
            _lastRootPosition = currentPos;
            
            // Keep trail at reasonable length
            if (_motionTrail.Count > MAX_TRAIL_POINTS)
            {
                _motionTrail.RemoveAt(0);
            }
        }
    }

    private void DrawMotionTrail()
    {
        if (_motionTrail.Count < 2) return;
        
        Handles.matrix = Matrix4x4.identity;
        
        // Draw trail as connected line segments with gradient
        // WHY: Gradient from transparent to opaque shows direction of movement
        for (int i = 0; i < _motionTrail.Count - 1; i++)
        {
            float alpha = Mathf.Lerp(0.1f, 0.8f, (float)i / (_motionTrail.Count - 1));
            Handles.color = new Color(0.2f, 1f, 0.4f, alpha);
            
            Vector3 start = _motionTrail[i];
            Vector3 end = _motionTrail[i + 1];
            
            // Draw slightly above ground to prevent z-fighting with grid
            start.y += 0.02f;
            end.y += 0.02f;
            
            Handles.DrawLine(start, end, 2f);
        }
        
        // Draw dots at recorded positions
        // WHY: Makes the trail more visible and shows sample density
        Handles.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        foreach (var point in _motionTrail)
        {
            Vector3 p = point;
            p.y += 0.02f;
            Handles.DrawSolidDisc(p, Vector3.up, 0.02f);
        }
    }

    /// <summary>
    /// Draws an arrow showing the character's forward direction.
    /// 
    /// WHY: Essential for seeing rotation during animations, especially for
    /// attacks that involve turning or directional movement.
    /// </summary>
    private void DrawForwardDirectionIndicator()
    {
        if (_previewInstance == null) return;
        
        Vector3 position = _previewInstance.transform.position;
        Vector3 forward = _previewInstance.transform.forward;
        
        // Draw arrow from character position
        Vector3 arrowStart = position + Vector3.up * 0.05f;
        Vector3 arrowEnd = arrowStart + forward * 0.8f;
        
        Handles.matrix = Matrix4x4.identity;
        Handles.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        
        // Main arrow line
        Handles.DrawLine(arrowStart, arrowEnd, 4f);
        
        // Arrow head
        // WHY: Without the arrow head, it's unclear which direction is "forward"
        Vector3 right = Vector3.Cross(forward, Vector3.up).normalized;
        Vector3 arrowTip1 = arrowEnd - forward * 0.15f + right * 0.1f;
        Vector3 arrowTip2 = arrowEnd - forward * 0.15f - right * 0.1f;
        
        Handles.DrawLine(arrowEnd, arrowTip1, 4f);
        Handles.DrawLine(arrowEnd, arrowTip2, 4f);
    }

    /// <summary>
    /// Displays current animation speed as a visual indicator.
    /// 
    /// WHY: When you modify speed curves, you want immediate visual feedback
    /// showing how fast the animation is playing at the current time.
    /// </summary>
    private void DrawVelocityIndicator(float currentSpeed)
    {
        if (_previewInstance == null) return;
        
        Vector3 position = _previewInstance.transform.position;
        
        // Draw speed indicator as a vertical bar
        // WHY: Height represents speed - taller = faster
        float indicatorHeight = Mathf.Clamp(currentSpeed * 0.5f, 0.1f, 3f);
        Vector3 barBottom = position + Vector3.right * 1.2f;
        Vector3 barTop = barBottom + Vector3.up * indicatorHeight;
        
        Handles.matrix = Matrix4x4.identity;
        
        // Color gradient: blue (slow) -> green (normal) -> red (fast)
        Color barColor;
        if (currentSpeed < 0.7f)
            barColor = Color.Lerp(Color.blue, Color.green, currentSpeed / 0.7f);
        else if (currentSpeed < 1.3f)
            barColor = Color.green;
        else
            barColor = Color.Lerp(Color.green, Color.red, (currentSpeed - 1.3f) / 1.7f);
            
        barColor.a = 0.8f;
        Handles.color = barColor;
        
        // Draw the speed bar
        Handles.DrawLine(barBottom, barTop, 8f);
        
        // Draw reference line at speed = 1.0
        Vector3 refLineStart = barBottom + Vector3.right * -0.15f + Vector3.up * 0.5f;
        Vector3 refLineEnd = barBottom + Vector3.right * 0.15f + Vector3.up * 0.5f;
        Handles.color = new Color(1f, 1f, 1f, 0.5f);
        Handles.DrawDottedLine(refLineStart, refLineEnd, 2f);
    }

    /// <summary>
    /// Draws frame information overlay in the top-left corner.
    /// 
    /// WHY: Frame-accurate animation timing is essential for game feel.
    /// Designers need to know exact frame counts for tuning.
    /// </summary>
    private void DrawFrameInfo(AnimationClip clip, float normalizedTime)
    {
        if (clip == null) return;
        
        int currentFrame = Mathf.RoundToInt(normalizedTime * clip.length * clip.frameRate);
        int totalFrames = Mathf.RoundToInt(clip.length * clip.frameRate);
        float currentTime = normalizedTime * clip.length;
        
        // Draw semi-transparent background for readability
        Handles.BeginGUI();
        
        string frameText = $"Frame: {currentFrame} / {totalFrames}";
        string timeText = $"Time: {currentTime:F3}s / {clip.length:F3}s";
        string speedText = $"FPS: {clip.frameRate:F0}";
        
        // Calculate text size
        GUIStyle labelStyle = new GUIStyle(EditorStyles.whiteLargeLabel);
        labelStyle.fontSize = 11;
        labelStyle.normal.textColor = Color.white;
        
        Vector2 frameSize = labelStyle.CalcSize(new GUIContent(frameText));
        Vector2 timeSize = labelStyle.CalcSize(new GUIContent(timeText));
        Vector2 speedSize = labelStyle.CalcSize(new GUIContent(speedText));
        
        float maxWidth = Mathf.Max(frameSize.x, timeSize.x, speedSize.x);
        float totalHeight = frameSize.y + timeSize.y + speedSize.y + 12f;
        
        // Background
        Rect bgRect = new Rect(8, 8, maxWidth + 16f, totalHeight);
        EditorGUI.DrawRect(bgRect, new Color(0f, 0f, 0f, 0.7f));
        
        // Text labels
        Rect frameRect = new Rect(12, 10, frameSize.x, frameSize.y);
        Rect timeRect = new Rect(12, 10 + frameSize.y + 2f, timeSize.x, timeSize.y);
        Rect speedRect = new Rect(12, 10 + frameSize.y + timeSize.y + 6f, speedSize.x, speedSize.y);
        
        GUI.Label(frameRect, frameText, labelStyle);
        GUI.Label(timeRect, timeText, labelStyle);
        GUI.Label(speedRect, speedText, labelStyle);
        
        Handles.EndGUI();
    }

    #endregion

    #region Camera Control

    /// <summary>
    /// Handles mouse input for rotating and zooming the preview camera.
    /// 
    /// WHY: User control is essential for inspecting animations from different angles.
    /// We use standard Unity editor conventions (drag to rotate, scroll to zoom).
    /// 
    /// CRITICAL FIX: Proper control ID management with FocusType.Passive and rect parameter.
    /// This ensures Unity's GUI system can properly track our control across frames.
    /// </summary>
    private void HandlePreviewInput(Rect rect)
    {
        Event e = Event.current;
        
        // CRITICAL: Get control ID with consistent hash AND pass rect
        // WHY: The rect parameter tells Unity where our control is, enabling proper hit testing
        int controlID = GUIUtility.GetControlID("AnimPreview".GetHashCode(), FocusType.Passive, rect);
        
        EventType eventType = e.GetTypeForControl(controlID);

        switch (eventType)
        {
            case EventType.MouseDown:
                if (e.button == 0 && rect.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = controlID;
                    _isDragging = true;
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlID && _isDragging)
                {
                    // Rotate camera with mouse drag
                    _drag.x += e.delta.x * DRAG_SENSITIVITY;
                    _drag.y -= e.delta.y * DRAG_SENSITIVITY;
                    _drag.y = Mathf.Clamp(_drag.y, -90f, 90f);
                    
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlID)
                {
                    GUIUtility.hotControl = 0;
                    _isDragging = false;
                    e.Use();
                }
                break;

            case EventType.ScrollWheel:
                if (rect.Contains(e.mousePosition))
                {
                    // Zoom with scroll wheel
                    _zoom -= e.delta.y * 0.05f;
                    _zoom = Mathf.Clamp(_zoom, ZOOM_MIN, ZOOM_MAX);
                    
                    e.Use();
                }
                break;
        }
    }

    /// <summary>
    /// Positions the camera based on character bounds and user controls.
    /// 
    /// WHY: Automatic framing ensures the character is always visible.
    /// We calculate bounds to properly frame the GameObject regardless of size.
    /// </summary>
    private void PositionCamera()
    {
        if (_previewInstance == null) return;

        // Calculate bounds of all renderers
        // WHY: We need to know the size to properly frame the character
        Bounds bounds = new Bounds(_previewInstance.transform.position, Vector3.zero);
        var renderers = _previewInstance.GetComponentsInChildren<Renderer>();
        
        bool hasBounds = false;
        foreach (var r in renderers)
        {
            if (r.enabled)
            {
                bounds.Encapsulate(r.bounds);
                hasBounds = true;
            }
        }

        // Fallback if no renderers found
        if (!hasBounds)
        {
            bounds = new Bounds(_previewInstance.transform.position, Vector3.one);
        }

        // Position camera based on bounds, zoom, and rotation
        Vector3 center = bounds.center;
        float distance = Mathf.Max(bounds.size.magnitude * 1.5f, 0.5f) / _zoom;

        Quaternion rotation = Quaternion.Euler(_drag.y, _drag.x, 0f);
        Vector3 direction = rotation * Vector3.forward;

        _previewUtility.camera.transform.position = center - direction * distance;
        _previewUtility.camera.transform.LookAt(center);
    }

    #endregion

    #region Public Controls

    /// <summary>
    /// Resets the motion trail. Call this when scrubbing to a new time.
    /// 
    /// WHY: When jumping to a different time in the animation, the trail
    /// should reset to avoid showing discontinuous paths.
    /// </summary>
    public void ResetMotionTrail()
    {
        _motionTrail.Clear();
        _hasRecordedInitialPosition = false;
    }

    /// <summary>
    /// Resets camera to default position and zoom.
    /// </summary>
    public void ResetCamera()
    {
        _drag = new Vector2(150f, -30f);
        _zoom = 1.0f;
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Cleans up preview resources. Must be called when done.
    /// 
    /// WHY: PreviewRenderUtility allocates native resources that must be
    /// manually freed. Failing to cleanup causes memory leaks.
    /// </summary>
    public void Cleanup()
    {
        if (_previewInstance != null)
        {
            try
            {
                UnityEngine.Object.DestroyImmediate(_previewInstance);
            }
            catch (ObjectDisposedException)
            {
                // MagicaCloth2 bug: its OnDestroy calls Remove() on a NativeParallelHashMap
                // owned by the global MagicaCloth manager, which is already deallocated at this
                // point in the editor lifecycle. The object is destroyed regardless.
            }
            finally
            {
                _previewInstance = null;
            }
        }

        if (_previewUtility != null)
        {
            _previewUtility.Cleanup();
            _previewUtility = null;
        }
        
        _motionTrail.Clear();
        _hasRecordedInitialPosition = false;
        _isDragging = false;
    }
    public void HandleInput(Rect rect)
    {
        HandlePreviewInput(rect);
    }
    public void Dispose()
    {
        Cleanup();
    }

    #endregion
}
