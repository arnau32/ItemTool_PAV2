using System.Collections;
using UnityEngine;

public class ExtractionAnnouncer : MonoBehaviour
{
    #region Fields

    [SerializeField] private CanvasGroup[] _groups;
    [SerializeField] private float _displayDuration = 2f;
    [SerializeField] private float _fadeDuration = 1f;

    private Coroutine _routine;

    #endregion

    #region Unity Callbacks

    private void OnEnable()
    {
        SetAlpha(1f);

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FadeOutRoutine());
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    #endregion

    #region Private

    private IEnumerator FadeOutRoutine()
    {
        yield return new WaitForSeconds(_displayDuration);

        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(1f - Mathf.Clamp01(elapsed / _fadeDuration));
            yield return null;
        }

        SetAlpha(0f);
        gameObject.SetActive(false);
    }

    private void SetAlpha(float alpha)
    {
        for (int i = 0; i < _groups.Length; i++)
        {
            if (_groups[i] != null) _groups[i].alpha = alpha;
        }
    }

    #endregion
}
