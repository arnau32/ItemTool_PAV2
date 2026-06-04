using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class OcclusionGroup : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Serializable]
    private struct MaterialSlot
    {
        public Renderer Renderer;
        public int MaterialIndex;
        public int ColorPropertyId;
        public Color OriginalColor;
    }

    [Header("Setup")]
    [SerializeField] private Renderer[] _renderers;

    [Header("Fade Settings")]
    [SerializeField] private bool _useServiceDefaults = false;
    [SerializeField, Range(0f, 1f)] private float _hiddenAlpha = 0f;
    [SerializeField, Min(0.01f)] private float _fadeOutSpeed = 2f;
    [SerializeField, Min(0.01f)] private float _fadeInSpeed = 2f;

    [Header("Debug")]
    [SerializeField, Range(0f, 1f)] private float _currentAlpha = 1f;
    [SerializeField] private bool _previewInEditMode = true;

    private readonly List<MaterialSlot> _materialSlots = new(16);

    private MaterialPropertyBlock _propertyBlock;

    private OcclusionReason _activeReasons = OcclusionReason.None;
    private float _targetAlpha = 1f;
    private bool _isInitialized;

    public bool IsOccluded => _activeReasons != OcclusionReason.None;
    public float CurrentAlpha => _currentAlpha;

    private void Reset()
    {
        AutoCollectRenderers();
    }

    private void Awake()
    {
        EnsurePropertyBlock();
        InitializeIfNeeded();
        ApplyAlpha(_currentAlpha);
        enabled = false;
    }

    private void OnEnable()
    {
        EnsurePropertyBlock();

        if (!_isInitialized)
        {
            InitializeIfNeeded();
        }
    }

    private void OnValidate()
    {
        if (_renderers == null || _renderers.Length == 0)
            AutoCollectRenderers();

        if (Application.isPlaying || !_previewInEditMode) return;
        
        EnsurePropertyBlock();
        InitializeIfNeeded();
        ApplyAlpha(_currentAlpha);
    }

    private void Update()
    {
        float hiddenAlpha = ResolveHiddenAlpha();
        float fadeOutSpeed = ResolveFadeOutSpeed();
        float fadeInSpeed = ResolveFadeInSpeed();

        _targetAlpha = IsOccluded ? hiddenAlpha : 1f;

        if (Mathf.Approximately(_currentAlpha, _targetAlpha))
        {
            _currentAlpha = _targetAlpha;
            ApplyAlpha(_currentAlpha);
            enabled = false;
            return;
        }

        float speed = _targetAlpha < _currentAlpha ? fadeOutSpeed : fadeInSpeed;
        _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, speed * Time.deltaTime);
        ApplyAlpha(_currentAlpha);

        if (Mathf.Approximately(_currentAlpha, _targetAlpha))
        {
            enabled = false;
        }
    }

    public void SetReason(OcclusionReason reason, bool enabledState)
    {
        OcclusionReason before = _activeReasons;

        if (enabledState)
        {
            _activeReasons |= reason;
        }
        else
        {
            _activeReasons &= ~reason;
        }

        if (before != _activeReasons)
        {
            enabled = true;
        }
    }

    public void ClearAllReasons()
    {
        if (_activeReasons == OcclusionReason.None) return;

        _activeReasons = OcclusionReason.None;
        enabled = true;
    }

    public void ForceVisibleImmediate()
    {
        _activeReasons = OcclusionReason.None;
        _currentAlpha = 1f;
        _targetAlpha = 1f;
        ApplyAlpha(1f);
        enabled = false;
    }

    [ContextMenu("Auto Collect Renderers")]
    public void AutoCollectRenderers()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void EnsurePropertyBlock()
    {
        _propertyBlock ??= new MaterialPropertyBlock();
    }

    private void InitializeIfNeeded()
    {
        _materialSlots.Clear();

        if (_renderers == null || _renderers.Length == 0)
        {
            AutoCollectRenderers();
        }

        foreach (var renderer in _renderers)
        {
            if (renderer == null) continue;

            Material[] sharedMaterials = renderer.sharedMaterials;
            
            if (sharedMaterials == null || sharedMaterials.Length == 0) continue;

            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                Material material = sharedMaterials[i];
                
                if (material == null) continue;

                int colorPropertyId = 0;
                Color originalColor = Color.white;

                if (material.HasProperty(BaseColorId))
                {
                    colorPropertyId = BaseColorId;
                    originalColor = material.GetColor(BaseColorId);
                }
                else if (material.HasProperty(ColorId))
                {
                    colorPropertyId = ColorId;
                    originalColor = material.GetColor(ColorId);
                }
                else
                {
                    continue;
                }

                _materialSlots.Add(new MaterialSlot
                {
                    Renderer = renderer,
                    MaterialIndex = i,
                    ColorPropertyId = colorPropertyId,
                    OriginalColor = originalColor
                });
            }
        }

        _isInitialized = true;
    }

    private float ResolveHiddenAlpha()
    {
        if (!_useServiceDefaults || !GameServices.TryGet<OcclusionService>(out var service)) return _hiddenAlpha;

        return service.DefaultHiddenAlpha;
    }

    private float ResolveFadeOutSpeed()
    {
        if (!_useServiceDefaults || !GameServices.TryGet<OcclusionService>(out var service)) return _fadeOutSpeed;

        return service.DefaultFadeOutSpeed;
    }

    private float ResolveFadeInSpeed()
    {
        if (!_useServiceDefaults || !GameServices.TryGet<OcclusionService>(out var service)) return _fadeInSpeed;

        return service.DefaultFadeInSpeed;
    }

    private void ApplyAlpha(float alpha)
    {
        if (!_isInitialized) return;

        EnsurePropertyBlock();

        foreach (var slot in _materialSlots)
        {
            if (slot.Renderer == null) continue;

            Color color = slot.OriginalColor;
            color.a = alpha;

            slot.Renderer.GetPropertyBlock(_propertyBlock, slot.MaterialIndex);
            _propertyBlock.SetColor(slot.ColorPropertyId, color);
            slot.Renderer.SetPropertyBlock(_propertyBlock, slot.MaterialIndex);
        }
    }
}