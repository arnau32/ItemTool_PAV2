using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CharacterHealthSystem))]
public class EnemyDamageStatsDisplay : MonoBehaviour
{
    #region Fields

    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private float _dpsWindow = 3f;

    private CharacterHealthSystem _health;

    private float _totalDamage;
    private float _maxDps;
    private float _windowTotal;

    private float _lastDisplayTotal;
    private float _lastDisplayDps;
    private float _lastDisplayMax;

    private readonly Queue<DamageEntry> _damageWindow = new Queue<DamageEntry>(64);
    private readonly StringBuilder _sb = new StringBuilder(128);

    private struct DamageEntry
    {
        public float Time;
        public float Damage;
    }

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _health = GetComponent<CharacterHealthSystem>();
    }

    private void OnEnable()
    {
        _health.OnDamageTaken += HandleDamageTaken;
        ResetStats();
    }

    private void OnDisable()
    {
        _health.OnDamageTaken -= HandleDamageTaken;
    }

    private void Update()
    {
        if (_label == null) return;

        PurgeStaleEntries();

        float currentDps = _windowTotal / _dpsWindow;
        if (currentDps > _maxDps) _maxDps = currentDps;

        float roundedTotal = Mathf.Round(_totalDamage * 10f) / 10f;
        float roundedDps   = Mathf.Round(currentDps * 10f) / 10f;
        float roundedMax   = Mathf.Round(_maxDps * 10f) / 10f;

        if (Mathf.Approximately(roundedTotal, _lastDisplayTotal) &&
            Mathf.Approximately(roundedDps,   _lastDisplayDps)   &&
            Mathf.Approximately(roundedMax,   _lastDisplayMax))
            return;

        _lastDisplayTotal = roundedTotal;
        _lastDisplayDps   = roundedDps;
        _lastDisplayMax   = roundedMax;

        _sb.Clear();
        _sb.Append("Daño recibido: ").Append(roundedTotal.ToString("F1"))
           .Append("\nDPS: ").Append(roundedDps.ToString("F1"))
           .Append("\nDPS Máximo: ").Append(roundedMax.ToString("F1"));

        _label.SetText(_sb);
    }

    #endregion

    #region Private

    private void HandleDamageTaken(float damage, Enums.HitType _, float __)
    {
        _totalDamage += damage;
        _windowTotal += damage;
        _damageWindow.Enqueue(new DamageEntry { Time = Time.time, Damage = damage });
    }

    private void PurgeStaleEntries()
    {
        float cutoff = Time.time - _dpsWindow;
        while (_damageWindow.Count > 0 && _damageWindow.Peek().Time < cutoff)
            _windowTotal -= _damageWindow.Dequeue().Damage;
    }

    [ContextMenu("Reset Stats")]
    private void ResetStats()
    {
        _totalDamage = 0f;
        _maxDps      = 0f;
        _windowTotal = 0f;
        _damageWindow.Clear();
        _lastDisplayTotal = -1f;
        _lastDisplayDps   = -1f;
        _lastDisplayMax   = -1f;
    }

    #endregion
}
