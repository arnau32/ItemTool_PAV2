using UnityEngine;

public class EnemyQuestMarker : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _markerRenderer;

    private EnemyDefinition _definition;

    private void Awake()
    {
        var enemyBase = GetComponent<EnemyBase>();
        _definition = enemyBase.Definition;

        _markerRenderer.enabled = false;
    }

    private void OnEnable()
    {
        Refresh();

        if (GameServices.TryGet<QuestService>(out var qs))
        {
            qs.OnQuestStateChanged += OnQuestStateChanged;
        }
    }

    private void OnDisable()
    {
        if (GameServices.TryGet<QuestService>(out var qs))
        {
            qs.OnQuestStateChanged -= OnQuestStateChanged;
        }
    }

    private void OnQuestStateChanged(Quest _) => Refresh();

    private void Refresh()
    {
        if (_markerRenderer == null || _definition == null) return;

        bool targeted = GameServices.TryGet<QuestService>(out var qs)
                        && qs.IsEnemyDefinitionTargeted(_definition);

        if (_markerRenderer.enabled != targeted)
            _markerRenderer.enabled = targeted;
    }
}
