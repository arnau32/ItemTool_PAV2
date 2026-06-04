using UnityEngine;
using UnityEngine.UI;
public class StaminaDebtFeedback : MonoBehaviour
{
    [Header("References")] [SerializeField]
    private StaminaSystem _staminaSystem;

    [Header("Follow Settings")] [Tooltip("Container GameObject with all stamina sprite renderers")] [SerializeField]
    private Transform _staminaUIContainer;

    [SerializeField] private Vector3 _offset = new Vector3(0f, 2.5f, 0f);
    [SerializeField] private bool _followPlayer = true;

    [Header("Sprite Renderers")] [SerializeField]
    private SpriteRenderer _staminaCurrentBar;

    [SerializeField] private SpriteRenderer _staminaGhostBar;
    [SerializeField] private SpriteRenderer _staminaRedBar;
    [SerializeField] private SpriteRenderer _staminaBackgroundBar;

    [Header("Ghost Bar")] [SerializeField] private float _ghostConsolidationDelay = 0.25f;
    [SerializeField] private float _ghostConsolidationSpeed = 3f;

    [Header("Pulse Animation")] [Tooltip("Enable pulsing red bar when in debt")] [SerializeField]
    private bool _enablePulse = true;

    [SerializeField] private float _pulseSpeed = 3f;
    [SerializeField] private float _minAlpha = 0.6f;
    [SerializeField] private float _maxAlpha = 1f;

    [Header("Color Shift")] [Tooltip("Change debt bar color based on debt amount")] [SerializeField]
    private bool _enableColorShift = true;

    [SerializeField] private Color _lowDebtColor = new Color(1f, 0.5f, 0f, 1f); // Orange
    [SerializeField] private Color _highDebtColor = new Color(1f, 0f, 0f, 1f); // Red
    [SerializeField, Range(0f, 1f)] private float _highDebtThreshold = 0.7f;

    [Header("Screen Vignette")] [Tooltip("Darken screen edges when in debt")] [SerializeField]
    private bool _enableVignette = false;

    [SerializeField] private Image _vignetteOverlay;
    [SerializeField] private float _vignetteMaxAlpha = 0.3f;
    [SerializeField] private float _vignetteFadeSpeed = 2f;

    [Header("Auto-Hide")] [Tooltip("Time in seconds at full stamina before the bar starts fading out.")] [SerializeField]
    private float _hideDelay = 2.0f;

    [Tooltip("How fast the bar fades out (seconds for full fade).")] [SerializeField]
    private float _hideFadeDuration = 0.5f;

    [Tooltip("How fast the bar fades back in when stamina is consumed (seconds for full fade).")] [SerializeField]
    private float _showFadeDuration = 0.15f;

    [Header("Feedback")] [SerializeField] private FeedbacksNagu.FeedbackContainer _staminaDebtFeedback;

    [Header("Companion Renderers")]
    [Tooltip("Extra SpriteRenderers (e.g. ability indicator) that follow the same show/hide as the stamina bars.")]
    [SerializeField] private SpriteRenderer[] _companionRenderers;

    private bool _wasInDebt;
    private float _pulseTime;
    private float _currentVignetteAlpha;

    private float _ghostValue01;
    private float _consolidationTimer;
    private float _lastStamina;

    private Material _currentBarMat;
    private Material _ghostBarMat;
    private Material _redBarMat;
    private Material _backgroundBarMat;

    // Auto-hide state
    private float _fullStaminaTimer; // Time since stamina has been at max
    private float _barVisibility; // 0 = hidden, 1 = fully visible
    private bool _barsHidden; // True when renderers are disabled (visibility reached 0)

    private static readonly int FillAmountID = Shader.PropertyToID("_FillAmount");
    private static readonly int InnerFillAmountID = Shader.PropertyToID("_InnerFillAmount");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    private const float EPSILON = 0.001f;

    private void Start()
    {
        if (_staminaSystem == null)
        {
            _staminaSystem = GetComponent<StaminaSystem>();
        }

        InitializeMaterials();

        _wasInDebt = false;
        _pulseTime = 0f;
        _currentVignetteAlpha = 0f;

        if (_staminaSystem != null)
        {
            _ghostValue01 = _staminaSystem.CurrentStamina01;
            _lastStamina = _staminaSystem.CurrentStamina;
        }

        _consolidationTimer = 0f;

        _barVisibility = 0f;
        _fullStaminaTimer = 0f;
        _barsHidden = true;
        SetRenderersEnabled(false);

        if (_vignetteOverlay == null) return;

        var color = _vignetteOverlay.color;
        color.a = 0f;
        _vignetteOverlay.color = color;
    }

    private void OnEnable()
    {
        if (_staminaSystem != null)
        {
            _staminaSystem.OnStaminaModifies += OnStaminaChanged;
        }
    }

    private void OnDisable()
    {
        if (_staminaSystem != null)
        {
            _staminaSystem.OnStaminaModifies -= OnStaminaChanged;
        }
    }

    private void OnDestroy()
    {
        DestroyMaterialInstance(ref _currentBarMat);
        DestroyMaterialInstance(ref _ghostBarMat);
        DestroyMaterialInstance(ref _redBarMat);
        DestroyMaterialInstance(ref _backgroundBarMat);
    }

    private void InitializeMaterials()
    {
        if (_staminaCurrentBar != null) _currentBarMat = CreateMaterialInstance(_staminaCurrentBar);

        if (_staminaGhostBar != null) _ghostBarMat = CreateMaterialInstance(_staminaGhostBar);

        if (_staminaRedBar != null) _redBarMat = CreateMaterialInstance(_staminaRedBar);

        if (_staminaBackgroundBar != null) _backgroundBarMat = CreateMaterialInstance(_staminaBackgroundBar);
    }

    private Material CreateMaterialInstance(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sharedMaterial == null) return null;

        var instance = new Material(renderer.sharedMaterial);
        renderer.material = instance;
        return instance;
    }

    private void DestroyMaterialInstance(ref Material mat)
    {
        if (mat == null) return;

        if (Application.isPlaying)
        {
            Destroy(mat);
        }
        else
        {
            DestroyImmediate(mat);
        }

        mat = null;
    }

    private void OnStaminaChanged()
    {
        if (_staminaSystem == null) return;

        var current = _staminaSystem.CurrentStamina;
        var delta = current - _lastStamina;

        if (delta < -EPSILON)
        {
            // Stamina consumed — show bar immediately and reset hide timer
            _fullStaminaTimer = 0f;
            ShowBarsImmediate();
            _consolidationTimer = 0f;
        }
        else if (delta > EPSILON)
        {
            _ghostValue01 = _staminaSystem.CurrentStamina01;
            _consolidationTimer = 0f;
        }

        _lastStamina = current;
    }

    private void Update()
    {
        if (_staminaSystem == null) return;

        var isInDebt = _staminaSystem.IsInDebt;
        var debtRatio = _staminaSystem.CurrentDebt01;

        // Detect debt state changes
        if (isInDebt && !_wasInDebt)
        {
            OnEnterDebt();
        }
        else if (!isInDebt && _wasInDebt)
        {
            OnExitDebt();
        }

        _wasInDebt = isInDebt;

        // Auto-hide logic
        UpdateAutoHide(Time.deltaTime);

        // Skip visual updates when fully hidden (saves material SetFloat calls)
        if (_barsHidden) return;

        UpdateGhostBarConsolidation(Time.deltaTime);
        UpdateCurrentBar();
        UpdateGhostBar();

        // Update effects while in debt
        if (isInDebt)
        {
            UpdatePulseEffect(debtRatio);
            UpdateColorShift(debtRatio);
            UpdateVignette(debtRatio);
            UpdateDebtBar(debtRatio);

            // Check for high debt threshold
            if (debtRatio >= _highDebtThreshold)
            {
                OnHighDebt();
            }
        }
        else
        {
            if (_staminaRedBar != null && _staminaRedBar.enabled)
                _staminaRedBar.enabled = false;

            // Fade out effects
            FadeOutVignette();
        }
    }

    private void LateUpdate()
    {
        if (!_followPlayer || _barsHidden) return;

        _staminaUIContainer.position = transform.position + _offset;
    }

    #region Auto-Hide

    private void UpdateAutoHide(float dt)
    {
        var isFull = _staminaSystem.CurrentStamina01 >= 1f - EPSILON;

        if (isFull && !_staminaSystem.IsDraining)
        {
            _fullStaminaTimer += dt;

            // After delay, start fading out
            if (!(_fullStaminaTimer >= _hideDelay)) return;

            var fadeSpeed = _hideFadeDuration > EPSILON ? dt / _hideFadeDuration : 1f;
            _barVisibility = Mathf.Max(0f, _barVisibility - fadeSpeed);

            ApplyBarVisibility(_barVisibility);

            // Once fully faded, disable renderers to save draw calls
            if (!(_barVisibility <= 0f) || _barsHidden) return;

            SetRenderersEnabled(false);
            _barsHidden = true;
        }
        else
        {
            _fullStaminaTimer = 0f;

            // Fade in if not fully visible
            if (!(_barVisibility < 1f)) return;

            var fadeSpeed = _showFadeDuration > EPSILON ? dt / _showFadeDuration : 1f;
            _barVisibility = Mathf.Min(1f, _barVisibility + fadeSpeed);
            ApplyBarVisibility(_barVisibility);
        }
    }

    /// <summary>
    /// Instantly make bars fully visible (called when stamina is consumed).
    /// </summary>
    private void ShowBarsImmediate()
    {
        if (_barsHidden)
        {
            SetRenderersEnabled(true);
            _barsHidden = false;
        }

        _barVisibility = 1f;
        ApplyBarVisibility(1f);
    }

    /// <summary>
    /// Applies visibility alpha to all bar materials at once.
    /// Modifies the alpha channel of each material's _Color property.
    /// </summary>
    private void ApplyBarVisibility(float visibility)
    {
        ApplyMaterialAlpha(_currentBarMat, visibility);
        ApplyMaterialAlpha(_ghostBarMat, visibility);
        ApplyMaterialAlpha(_backgroundBarMat, visibility);
        // Red bar alpha is managed by the pulse effect — only scale it
        // when NOT in debt (in debt, pulse controls alpha directly).
        if (!_staminaSystem.IsInDebt)
            ApplyMaterialAlpha(_redBarMat, visibility);

        if (_companionRenderers == null) return;
        for (int i = 0; i < _companionRenderers.Length; i++)
        {
            var sr = _companionRenderers[i];
            if (sr == null || !sr.enabled) continue;
            var c = sr.color;
            c.a = visibility;
            sr.color = c;
        }
    }

    private static void ApplyMaterialAlpha(Material mat, float alpha)
    {
        if (mat == null) return;

        Color c = mat.GetColor(ColorID);
        c.a = alpha;
        mat.SetColor(ColorID, c);
    }

    private void SetRenderersEnabled(bool enabled)
    {
        if (_staminaCurrentBar != null) _staminaCurrentBar.enabled = enabled;
        if (_staminaGhostBar != null) _staminaGhostBar.enabled = enabled;
        if (_staminaRedBar != null) _staminaRedBar.enabled = enabled;
        if (_staminaBackgroundBar != null) _staminaBackgroundBar.enabled = enabled;

        if (_companionRenderers == null) return;
        for (int i = 0; i < _companionRenderers.Length; i++)
        {
            if (_companionRenderers[i] != null)
                _companionRenderers[i].enabled = enabled;
        }
    }

    #endregion

    #region Stamina Bar Updates

    private void UpdateGhostBarConsolidation(float dt)
    {
        if (_staminaSystem == null) return;

        var currentStamina01 = _staminaSystem.CurrentStamina01;

        if (Mathf.Abs(_ghostValue01 - currentStamina01) < EPSILON)
        {
            _ghostValue01 = currentStamina01;
            return;
        }

        _consolidationTimer += dt;

        if (_consolidationTimer < _ghostConsolidationDelay) return;

        var consolidationAmount = _ghostConsolidationSpeed * dt;
        _ghostValue01 = Mathf.MoveTowards(_ghostValue01, currentStamina01, consolidationAmount);
    }

    private void UpdateCurrentBar()
    {
        if (_currentBarMat == null || _staminaSystem == null) return;

        var currentStamina01 = _staminaSystem.CurrentStamina01;
        UpdateBarFill(_currentBarMat, currentStamina01);
    }

    private void UpdateGhostBar()
    {
        if (_ghostBarMat == null || _staminaGhostBar == null || _staminaSystem == null) return;

        var currentStamina01 = _staminaSystem.CurrentStamina01;

        var shouldShowGhost = _ghostValue01 > currentStamina01 + EPSILON;

        if (shouldShowGhost)
        {
            // Ghost shows as a "ring" from current to ghost (the cost area)
            _ghostBarMat.SetFloat(FillAmountID, _ghostValue01); // Outer edge (where it was)
            _ghostBarMat.SetFloat(InnerFillAmountID, currentStamina01); // Inner edge (where it is now)
        }

        if (_staminaGhostBar.enabled != shouldShowGhost)
        {
            _staminaGhostBar.enabled = shouldShowGhost;
        }
    }

    private void UpdateDebtBar(float debtRatio)
    {
        if (_redBarMat == null || _staminaRedBar == null) return;

        UpdateBarFill(_redBarMat, debtRatio);

        if (!_staminaRedBar.enabled)
        {
            _staminaRedBar.enabled = true;
        }
    }

    private static void UpdateBarFill(Material barMaterial, float fillAmount01)
    {
        if (barMaterial == null) return;

        fillAmount01 = Mathf.Clamp01(fillAmount01);
        float currentFill = barMaterial.GetFloat(FillAmountID);

        if (Mathf.Abs(currentFill - fillAmount01) > EPSILON)
        {
            barMaterial.SetFloat(FillAmountID, fillAmount01);
        }
    }

    #endregion

    #region Debt Events

    private void OnEnterDebt()
    {
    }

    private void OnExitDebt()
    {
        if (_redBarMat == null) return;

        Color c = _redBarMat.GetColor(ColorID);
        c.a = 1f;
        _redBarMat.SetColor(ColorID, c);
    }

    private void OnHighDebt()
    {
        //TODO
        // Reset flag when debt goes below threshold
        if (_staminaSystem.CurrentDebt01 < _highDebtThreshold - 0.1f)
        {
        }
    }

    #endregion

    #region Effects

    private void UpdatePulseEffect(float debtRatio)
    {
        if (!_enablePulse || _redBarMat == null) return;

        _pulseTime += Time.deltaTime * _pulseSpeed;
        float pulse = Mathf.Lerp(_minAlpha, _maxAlpha, (Mathf.Sin(_pulseTime) + 1f) * 0.5f);

        // Pulse faster as debt increases
        float speedMultiplier = 1f + (debtRatio * 2f);
        _pulseTime += Time.deltaTime * speedMultiplier;

        Color c = _redBarMat.GetColor(ColorID);
        c.a = pulse;
        _redBarMat.SetColor(ColorID, c);
    }

    private void UpdateColorShift(float debtRatio)
    {
        if (!_enableColorShift || _redBarMat == null) return;

        Color targetColor = Color.Lerp(_lowDebtColor, _highDebtColor, debtRatio);

        Color currentColor = _redBarMat.GetColor(ColorID);
        currentColor.r = targetColor.r;
        currentColor.g = targetColor.g;
        currentColor.b = targetColor.b;

        _redBarMat.SetColor(ColorID, currentColor);
    }

    private void UpdateVignette(float debtRatio)
    {
        if (!_enableVignette || _vignetteOverlay == null) return;

        float targetAlpha = debtRatio * _vignetteMaxAlpha;
        _currentVignetteAlpha = Mathf.Lerp(_currentVignetteAlpha, targetAlpha, Time.deltaTime * _vignetteFadeSpeed);

        Color c = _vignetteOverlay.color;
        c.a = _currentVignetteAlpha;
        _vignetteOverlay.color = c;
    }

    private void FadeOutVignette()
    {
        if (!_enableVignette || _vignetteOverlay == null) return;

        _currentVignetteAlpha = Mathf.Lerp(_currentVignetteAlpha, 0f, Time.deltaTime * _vignetteFadeSpeed);

        Color c = _vignetteOverlay.color;
        c.a = _currentVignetteAlpha;
        _vignetteOverlay.color = c;
    }

    #endregion

#if UNITY_EDITOR

    #region Debug

    [Header("Debug")] [SerializeField] private bool _showDebugInfo = false;

    private void OnGUI()
    {
        if (!_showDebugInfo || _staminaSystem == null) return;

        GUILayout.BeginArea(new Rect(10, 100, 300, 250));
        GUILayout.Box("Stamina Debt Feedback Debug");
        GUILayout.Label($"Is In Debt: {_staminaSystem.IsInDebt}");
        GUILayout.Label($"Current Debt: {_staminaSystem.CurrentDebt:F2}");
        GUILayout.Label($"Debt Ratio: {_staminaSystem.CurrentDebt01:F2}");
        GUILayout.Label($"Current Stamina: {_staminaSystem.CurrentStamina:F2}");
        GUILayout.Label($"Ghost Value: {_ghostValue01:F2}");
        GUILayout.Label($"Consolidation Timer: {_consolidationTimer:F2}");
        GUILayout.Label($"Pulse Time: {_pulseTime:F2}");
        GUILayout.Label($"Bar Visibility: {_barVisibility:F2}");
        GUILayout.Label($"Full Timer: {_fullStaminaTimer:F2}");
        GUILayout.EndArea();
    }

    #endregion

#endif
}