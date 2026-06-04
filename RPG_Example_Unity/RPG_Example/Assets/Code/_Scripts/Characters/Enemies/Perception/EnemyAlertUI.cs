using UnityEngine;

public class EnemyAlertUI : MonoBehaviour
{
    #region Fields

    [Header("Icons")] [Tooltip("GameObject containing the '?' sprite (Suspicious / Alert).")] [SerializeField]
    private GameObject _suspiciousIcon;

    [Tooltip("SpriteRenderer inside the '?' icon that uses the SlicedFillBar material.")] [SerializeField]
    private SpriteRenderer _suspiciousFillRenderer;

    [Tooltip("GameObject containing the '!' sprite (Combat).")] [SerializeField]
    private GameObject _combatIcon;

    [Header("Combat Icon Timeout")] [Tooltip("Seconds the '!' stays visible after entering Combat before auto-hiding.")] [SerializeField, Min(0f)]
    private float _combatIconDuration = 1.75f;

    [SerializeField] private EnemyPerception _perception;

    private Enums.AlertLevel _lastLevel = Enums.AlertLevel.Unaware;

    private float _combatIconTimer;
    private bool _combatIconVisible;

    private MaterialPropertyBlock _mpb;
    private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _combatIcon.SetActive(false);
        SetIconsForLevel(Enums.AlertLevel.Unaware);
    }

    private void OnEnable()
    {
        if (_perception != null)
        {
            _perception.OnAlertLevelChanged += HandleAlertLevelChanged;
        }

        SetIconsForLevel(_perception != null ? _perception.AlertLevel : Enums.AlertLevel.Unaware);
    }

    private void OnDisable()
    {
        if (_perception != null)
        {
            _perception.OnAlertLevelChanged -= HandleAlertLevelChanged;
        }

        SetIconsForLevel(Enums.AlertLevel.Unaware);
    }

    private void Update()
    {
        TickCombatIconTimer();
        TickFillBar();
    }

    #endregion

    #region Helpers

    private void TickCombatIconTimer()
    {
        if (!_combatIconVisible) return;

        _combatIconTimer -= Time.deltaTime;

        if (_combatIconTimer <= 0f)
        {
            _combatIconVisible = false;

            if (_combatIcon != null)
                _combatIcon.SetActive(false);
        }
    }

    private void TickFillBar()
    {
        if (_suspiciousFillRenderer == null || _perception == null) return;
        if (_lastLevel != Enums.AlertLevel.Suspicious && _lastLevel != Enums.AlertLevel.Alert) return;

        SetFill(GetFill01());
    }

    private void HandleAlertLevelChanged(Enums.AlertLevel previous, Enums.AlertLevel current)
    {
        _lastLevel = current;

        if (current == Enums.AlertLevel.Combat)
        {
            // Snap fill to full for one frame before '?' is hidden — no visual pop.
            SetFill(1f);

            // Show '!' and start the visibility timer.
            ShowCombatIcon();
        }
        else
        {
            // Level dropped out of combat — hide '!' immediately and reset the timer
            // so it shows again at full duration if combat is re-entered.
            HideCombatIcon();
            SetIconsForLevel(current);
        }
    }

    private void ShowCombatIcon()
    {
        _combatIconVisible = true;
        _combatIconTimer = _combatIconDuration;

        if (_suspiciousIcon != null) _suspiciousIcon.SetActive(false);
        if (_combatIcon != null) _combatIcon.SetActive(true);
    }

    private void HideCombatIcon()
    {
        _combatIconVisible = false;
        _combatIconTimer = 0f;

        if (_combatIcon != null) _combatIcon.SetActive(false);
    }

    private void SetIconsForLevel(Enums.AlertLevel level)
    {
        bool showSuspicious = level == Enums.AlertLevel.Suspicious || level == Enums.AlertLevel.Alert;

        if (_suspiciousIcon != null && _suspiciousIcon.activeSelf != showSuspicious)
            _suspiciousIcon.SetActive(showSuspicious);

        // Combat icon is managed exclusively by ShowCombatIcon/HideCombatIcon + timer.
        // Never touch it here to avoid overriding the timer mid-countdown.

        if (!showSuspicious)
            SetFill(0f);
    }

    private void SetFill(float fill01)
    {
        if (_suspiciousFillRenderer == null) return;

        _suspiciousFillRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(FillAmountId, fill01);
        _suspiciousFillRenderer.SetPropertyBlock(_mpb);
    }

    // Normalizes alertValue: 0 at suspiciousThreshold, 1 at combatThreshold.
    private float GetFill01()
    {
        float suspicious = _perception.SuspiciousThreshold;
        float combat = _perception.CombatThreshold;
        float range = combat - suspicious;

        if (range <= 0f) return 1f;

        return Mathf.Clamp01((_perception.AlertValue - suspicious) / range);
    }

    #endregion
}