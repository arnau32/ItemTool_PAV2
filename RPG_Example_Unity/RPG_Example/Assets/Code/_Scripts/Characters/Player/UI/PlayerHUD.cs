using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[System.Serializable]
public class PlayerHUD : CharacterHUD
{
    [Header("Skill")]
    [SerializeField] private Slider _skill;

    [Header("Skill Colors")]
    [SerializeField] private Color _normalColor = new(0.5f, 0.5f, 0.15f);
    [SerializeField] private Color _readyColor = new(1f, 1f, 0f);

    [Header("Skill Transition")]
    [SerializeField] private float _transitionSpeed = 3f;
    [SerializeField] [Range(0f, 1f)] private float _chargeMaxFill = 0.85f;

    private Coroutine _sliderCoroutine;
    [SerializeField] RawImage _sliderFill_1;
    [SerializeField] RawImage _sliderFill_2;
    private MonoBehaviour _coroutineHost;

    public void Init(MonoBehaviour host)
    {
        _coroutineHost = host;
    }

    public void UpdateSkill(float ratio, bool canUseAbility)
    {
        if (_skill == null || _coroutineHost == null) return;

        if (_sliderCoroutine != null)
            _coroutineHost.StopCoroutine(_sliderCoroutine);

        float target = canUseAbility ? 1f : ratio * _chargeMaxFill;
        _sliderCoroutine = _coroutineHost.StartCoroutine(AnimateSlider(target));

        _sliderFill_1.color = canUseAbility ? _readyColor : _normalColor;
        _sliderFill_2.color = canUseAbility ? _readyColor : _normalColor;
    }

    // Instant reset when equipping a new weapon — no animation so the bar
    // doesn't misleadingly show residual charge from the previous weapon.
    public void ResetSkill()
    {
        if (_skill == null) return;

        if (_sliderCoroutine != null && _coroutineHost != null)
        {
            _coroutineHost.StopCoroutine(_sliderCoroutine);
            _sliderCoroutine = null;
        }

        _skill.value = 0f;
        if (_sliderFill_1 != null) _sliderFill_1.color = _normalColor;
        if (_sliderFill_2 != null) _sliderFill_2.color = _normalColor;
    }

    private IEnumerator AnimateSlider(float targetValue)
    {
        while (!Mathf.Approximately(_skill.value, targetValue))
        {
            _skill.value = Mathf.MoveTowards(
                _skill.value,
                targetValue,
                _transitionSpeed * Time.deltaTime
            );
            yield return null;
        }
        _skill.value = targetValue;
    }
}