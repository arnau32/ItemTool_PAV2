using System;
using System.Collections;
using UnityEngine;

public class TutorialMenuHint : MonoBehaviour
{
    #region Fields

    [SerializeField] private CanvasGroup _background;
    [SerializeField] private CanvasGroup _mapHint;
    [SerializeField] private CanvasGroup _menuHint;
    [SerializeField] private float _fadeDuration = 0.4f;

    private Action<TabType> _tabOpenListener;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (_background != null) SetHintState(_background, false);
        if (_mapHint != null)    SetHintState(_mapHint, false);
        SetHintState(_menuHint, false);
    }

    private void OnEnable()
    {
        StartCoroutine(RunSequence());
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        if (_tabOpenListener != null && TabViewManager.Instance != null)
            TabViewManager.Instance.TabOpen -= _tabOpenListener;
        _tabOpenListener = null;

        if (_background != null) SetHintState(_background, false);
        if (_mapHint != null)    SetHintState(_mapHint, false);
        SetHintState(_menuHint, false);
    }

    #endregion

    #region Sequence

    private IEnumerator RunSequence()
    {
        if (_background != null)
        {
            SetHintState(_background, true);
            yield return Fade(_background, 0f, 1f);
        }

        bool mapDone  = _mapHint == null;
        bool menuDone = false;

        if (_mapHint != null)
            StartCoroutine(RunMapHint(() => mapDone = true));

        StartCoroutine(RunMenuHint(() => menuDone = true));

        while (!mapDone || !menuDone)
            yield return null;

        if (_background != null) yield return Fade(_background, 1f, 0f);
        enabled = false;
    }

    private IEnumerator RunMapHint(Action onDone)
    {
        SetHintState(_mapHint, true);
        yield return Fade(_mapHint, 0f, 1f);

        while (WorldMapController.Instance == null || !WorldMapController.Instance.IsOpen)
            yield return null;

        yield return FadeOut(_mapHint);
        onDone();
    }

    private IEnumerator RunMenuHint(Action onDone)
    {
        SetHintState(_menuHint, true);
        yield return Fade(_menuHint, 0f, 1f);

        bool menuOpened = false;
        _tabOpenListener = type => { if (type == TabType.Menu || type == TabType.Inventory) menuOpened = true; };
        TabViewManager.Instance.TabOpen += _tabOpenListener;

        while (!menuOpened)
            yield return null;

        TabViewManager.Instance.TabOpen -= _tabOpenListener;
        _tabOpenListener = null;

        yield return FadeOut(_menuHint);
        onDone();
    }

    #endregion

    #region Helpers

    private IEnumerator FadeOut(CanvasGroup cg)
    {
        yield return Fade(cg, 1f, 0f);
        SetHintState(cg, false);
    }

    private IEnumerator Fade(CanvasGroup cg, float from, float to)
    {
        float elapsed = 0f;
        cg.alpha = from;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / _fadeDuration);
            yield return null;
        }
        cg.alpha = to;
    }

    private static void SetHintState(CanvasGroup cg, bool active)
    {
        cg.alpha = 0f;
        cg.gameObject.SetActive(active);
    }

    #endregion
}
