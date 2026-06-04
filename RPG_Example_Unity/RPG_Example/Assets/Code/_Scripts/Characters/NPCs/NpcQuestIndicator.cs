using System.Collections.Generic;
using UnityEngine;

public class NpcQuestIndicator : MonoBehaviour
{
    #region Fields

    [SerializeField] private SpriteRenderer _iconRenderer;

    [Header("Sprites")] [SerializeField] private Sprite _canStartSprite;
    [SerializeField] private Sprite _inProgressSprite;
    [SerializeField] private Sprite _canFinishSprite;

    [SerializeField] private Color _canStartColor;
    [SerializeField] private Color _inProgressColor;
    [SerializeField] private Color _canFinishColor;

    [Header("Quests")] [SerializeField] private List<string> _questIds = new();

    private QuestService _questService;
    private Transform _cam;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        _cam = Camera.main?.transform;

        if (!GameServices.TryGet<QuestService>(out _questService))
        {
            gameObject.SetActive(false);
            return;
        }

        _questService.OnQuestStateChanged += OnQuestStateChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_questService != null)
            _questService.OnQuestStateChanged -= OnQuestStateChanged;
    }

    private void LateUpdate()
    {
        if (_cam == null || !_iconRenderer.enabled) return;
        _iconRenderer.transform.forward = _cam.forward;
    }

    #endregion

    #region Private

    private void OnQuestStateChanged(Quest quest)
    {
        if (_questIds.Contains(quest.info.id))
            Refresh();
    }

    private void Refresh()
    {
        bool hasCanFinish = false;
        bool hasCanStart = false;
        bool hasInProgress = false;

        for (int i = 0; i < _questIds.Count; i++)
        {
            var quest = _questService.GetQuestById(_questIds[i]);
            if (quest == null) continue;

            switch (quest.state)
            {
                case Enums.QuestState.CanFinish: hasCanFinish = true; break;
                case Enums.QuestState.CanStart: hasCanStart = true; break;
                case Enums.QuestState.InProgress: hasInProgress = true; break;
            }
        }

        if (hasCanFinish)
        {
            SetIcon(_canFinishSprite, _canFinishColor);
            return;
        }

        if (hasCanStart)
        {
            SetIcon(_canStartSprite, _canStartColor);
            return;
        }

        if (hasInProgress)
        {
            SetIcon(_inProgressSprite, _inProgressColor);
            return;
        }

        _iconRenderer.enabled = false;
    }

    private void SetIcon(Sprite sprite, Color c)
    {
        _iconRenderer.sprite = sprite;
        _iconRenderer.enabled = sprite != null;
        _iconRenderer.color = c;
    }

    #endregion
}