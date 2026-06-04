using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using System;

[Serializable]
public class BackgroundSpriteGroup
{
    public Sprite[] Sprites;
}

public class LoadingScreenView : MonoBehaviour
{
    #region Fields

    [SerializeField] private Canvas _canvas;
    [SerializeField] private Image _fillBarImage;

    [SerializeField] private Image _background;
    [SerializeField] private BackgroundSpriteGroup[] _backgroundSpriteGroups;

    #endregion

    private void Awake()
    {
        if (_canvas.worldCamera != null) return;

        GameObject camGO = GameObject.FindWithTag("LoadingCamera");

        if (camGO != null)
            _canvas.worldCamera = camGO.GetComponent<Camera>();
    }

    #region Public API

    public void Show()
    {
        gameObject.SetActive(true);

        SetRandomBackground();
    }

    public void Show(int id)
    {
        gameObject.SetActive(true);

        SetRandomBackgroundFromGroup(id);
    }

    public void SetFill(float t)
    {
        _fillBarImage.fillAmount = t;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    #endregion

    #region Private Methods

    private void SetRandomBackground()
    {
        if (_backgroundSpriteGroups == null || _backgroundSpriteGroups.Length == 0) return;

        int groupIndex = Random.Range(0, _backgroundSpriteGroups.Length);

        SetRandomBackgroundFromGroup(groupIndex);
    }

    private void SetRandomBackgroundFromGroup(int groupIndex)
    {
        if (_background == null) return;
        if (_backgroundSpriteGroups == null || _backgroundSpriteGroups.Length == 0) return;
        if (groupIndex < 0 || groupIndex >= _backgroundSpriteGroups.Length) return;

        Sprite[] sprites = _backgroundSpriteGroups[groupIndex].Sprites;

        if (sprites == null || sprites.Length == 0) return;

        int spriteIndex = Random.Range(0, sprites.Length);

        _background.sprite = sprites[spriteIndex];
    }

    #endregion
}