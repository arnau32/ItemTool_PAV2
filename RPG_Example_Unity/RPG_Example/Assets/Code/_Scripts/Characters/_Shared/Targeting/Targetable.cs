using System;
using System.Collections.Generic;
using UnityEngine;

public class Targetable : MonoBehaviour
{
    public Transform aimPoint;

    [Range(0f, 1f)] public float StickinessBoost = 0.12f;

    // Dynamic weight. This should be updated in AI, for example:
    // +0.6f if its in attacking window, +0.2f if its elite, +0.2f if you are his aggro
    [Range(0f, 1.2f)] public float ThreatWeight = 0f;

    [Header("Lock On UI")]
    [SerializeField] private GameObject _lockOnCanvasObject;

    public static readonly HashSet<Targetable> All = new();

    // Fired when this target becomes invalid for lock-on (death, disable, etc)
    public event Action<Targetable> OnInvalidated;

    private IHealth _health;
    [SerializeField] private CharacterHealthSystem _healthSystem;
    private bool _isValid = true;

    public bool IsValid => _isValid;
    public bool IsAlive => _health == null || _health.IsAlive;

    void OnEnable()
    {
        All.Add(this);

        _isValid = true;

        CacheHealthIfNeeded();
        BindHealth();
    }

    void OnDisable()
    {
        UnbindHealth();
        All.Remove(this);

        Invalidate();
    }

    private void Awake()
    {
        CacheHealthIfNeeded();

        if (_lockOnCanvasObject != null)
        {
            _lockOnCanvasObject.SetActive(false);
        }
    }

    public Vector3 GetWorldAim() => aimPoint ? aimPoint.position : transform.position + Vector3.up * 1.5f;

    public void SetLocked(bool locked)
    {
        if (_lockOnCanvasObject == null) return;

        if (_lockOnCanvasObject.activeSelf == locked) return;

        _lockOnCanvasObject.SetActive(locked);
    }

    private void CacheHealthIfNeeded()
    {
        _health = _healthSystem;
    }

    private void BindHealth()
    {
        if (_healthSystem == null) return;

        _healthSystem.OnDeath -= HandleDeath;
        _healthSystem.OnDeath += HandleDeath;
    }

    private void UnbindHealth()
    {
        if (_healthSystem == null) return;

        _healthSystem.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        Invalidate();
    }

    private void Invalidate()
    {
        if (!_isValid) return;

        _isValid = false;

        SetLocked(false);

        OnInvalidated?.Invoke(this);

        All.Remove(this);
    }
}
