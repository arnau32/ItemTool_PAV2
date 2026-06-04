using UnityEngine;

// Displays the enemy's poise as a SpriteRenderer bar using the SlicedFillBar shader.
// Must be a child of EnemyHealthBar's _healthBarContainer so distance culling works.
//
// Visibility is controlled via _poiseBar.enabled (not SetActive) so material updates
// are never lost when the parent container is deactivated by distance culling.
// When the container reactivates, the renderer already has the correct fill value.
public class EnemyPoiseBar : MonoBehaviour
{
    #region Fields

    [Header("References")] [SerializeField]
    private SpriteRenderer _poiseBar;

    [Header("Color")] [SerializeField] private Color _poiseColor = new Color(0.4f, 0.6f, 1f, 1f);
    [SerializeField] private Color _brokenColor = new Color(0.8f, 0.2f, 0.2f, 1f);

    private EnemyPoiseSystem _poiseSystem;
    private Material _poiseMat;

    private float _lastFill = -1f;

    private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    #endregion

    #region Public API

    public void Initialize(EnemyPoiseSystem poiseSystem)
    {

        if (_poiseSystem != null)
        {
            _poiseSystem.OnPoiseChanged -= OnPoiseChanged;
        }

        if (poiseSystem != null)
        {
            poiseSystem.OnPoiseChanged -= OnPoiseChanged;
        }

        _poiseSystem = poiseSystem;

        if (_poiseBar == null)
        {
            Debug.LogError($"[EnemyPoiseBar] _poiseBar not assigned on {gameObject.name}.", this);
            return;
        }

        if (_poiseBar.sharedMaterial == null)
        {
            Debug.LogError($"[EnemyPoiseBar] _poiseBar has no material on {gameObject.name}.", this);
            return;
        }

        if (_poiseMat != null)
            Destroy(_poiseMat);

        _poiseMat = new Material(_poiseBar.sharedMaterial);
        _poiseBar.material = _poiseMat;

        _lastFill = -1f;

        _poiseSystem.OnPoiseChanged += OnPoiseChanged;

        // Start hidden — shows on first hit via OnPoiseChanged.
        _poiseBar.enabled = false;
    }

    #endregion

    #region Unity Callbacks

    private void OnDestroy()
    {
        if (_poiseSystem != null)
            _poiseSystem.OnPoiseChanged -= OnPoiseChanged;

        if (_poiseMat == null) return;

        if (Application.isPlaying)
        {
            Destroy(_poiseMat);
        }
        else
        {
            DestroyImmediate(_poiseMat);
        }
    }

    private void LateUpdate()
    {
        if (_poiseSystem == null || _poiseMat == null) return;
        if (_poiseSystem.IsBroken) return;

        float poise01 = _poiseSystem.Poise01;

        if (poise01 <= 0f)
        {
            _poiseBar.enabled = false;
            _lastFill = 0f;
            return;
        }

        if (!(Mathf.Abs(poise01 - _lastFill) > 0.0001f)) return;
        
        _poiseMat.SetFloat(FillAmountId, poise01);
        _lastFill = poise01;
    }

    #endregion

    #region Helpers

    private void OnPoiseChanged()
    {
        if (_poiseMat == null || _poiseSystem == null) return;

        float poise01 = _poiseSystem.Poise01;

        _poiseMat.SetFloat(FillAmountId, poise01);
        _poiseMat.SetColor(ColorId, _poiseSystem.IsBroken ? _brokenColor : _poiseColor);

        _lastFill = poise01;

        _poiseBar.enabled = poise01 > 0f || _poiseSystem.IsBroken;
    }

    #endregion
}