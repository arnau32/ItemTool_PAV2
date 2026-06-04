using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

public class DiscoveryPopupUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform popupTransform;
    [SerializeField] private LocalizeStringEvent localizeStringEvent;

    [Header("Timing")]
    [SerializeField] private float fadeInTime = 0.35f;
    [SerializeField] private float visibleTime = 2.5f;
    [SerializeField] private float fadeOutTime = 0.45f;

    [Header("Animation")]
    [SerializeField] private float startYOffset = -25f;

    private Coroutine currentRoutine;
    private Vector2 originalAnchoredPosition;
    private bool _isExiting;

    private void Awake()
    {
        if (popupTransform != null)
            originalAnchoredPosition = popupTransform.anchoredPosition;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (root != null)
            root.SetActive(false);
    }

    private void OnEnable()
    {
        WorldMapController.OnPOIPopupRequested += HandlePOIPopupRequested;
    }

    private void OnDisable()
    {
        WorldMapController.OnPOIPopupRequested -= HandlePOIPopupRequested;
    }

    private void HandlePOIPopupRequested(MapPOIData poi)
    {
        if (poi == null || _isExiting)
            return;

        Show(poi.localizedName);
    }

    public void Show(LocalizedString localizedString)
    {
        if (localizedString == null || localizedString.IsEmpty)
            return;

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(ShowRoutine(localizedString));
    }

    private IEnumerator ShowRoutine(LocalizedString localizedString)
    {
        _isExiting = false;
        SetLocalizedString(localizedString);

        if (root != null)
            root.SetActive(true);

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (popupTransform != null)
            popupTransform.anchoredPosition = originalAnchoredPosition + new Vector2(0f, startYOffset);

        yield return AnimateIn();

        yield return new WaitForSecondsRealtime(visibleTime);

        _isExiting = true;
        yield return AnimateOut();
        _isExiting = false;

        if (root != null)
            root.SetActive(false);

        currentRoutine = null;
    }

    private void SetLocalizedString(LocalizedString localizedString)
    {
        if (localizeStringEvent == null)
            return;

        localizeStringEvent.StringReference.TableReference = localizedString.TableReference;
        localizeStringEvent.StringReference.TableEntryReference = localizedString.TableEntryReference;
        localizeStringEvent.StringReference.Arguments = localizedString.Arguments;
        localizeStringEvent.RefreshString();
    }

    private IEnumerator AnimateIn()
    {
        if (canvasGroup == null || popupTransform == null)
            yield break;

        float elapsed = 0f;

        Vector2 startPos = originalAnchoredPosition + new Vector2(0f, startYOffset);
        Vector2 endPos = originalAnchoredPosition;

        while (elapsed < fadeInTime)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / fadeInTime);
            float eased = EaseOutCubic(t);

            canvasGroup.alpha = eased;
            popupTransform.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, eased);

            yield return null;
        }

        canvasGroup.alpha = 1f;
        popupTransform.anchoredPosition = endPos;
    }

    private IEnumerator AnimateOut()
    {
        if (canvasGroup == null || popupTransform == null)
            yield break;

        float elapsed = 0f;

        Vector2 startPos = originalAnchoredPosition;
        Vector2 endPos = originalAnchoredPosition + new Vector2(0f, startYOffset);

        while (elapsed < fadeOutTime)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / fadeOutTime);
            float eased = EaseInCubic(t);

            canvasGroup.alpha = 1f - eased;
            popupTransform.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, eased);

            yield return null;
        }

        canvasGroup.alpha = 0f;
        popupTransform.anchoredPosition = endPos;
    }

    private static float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseInCubic(float t)
    {
        return t * t * t;
    }
}