using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public enum RewardType { Item, AuroraDust }

public class RewardEntry
{
    public RewardType type;
    public Sprite icon;
    public string label;
    public int quantity;
    public bool isRandom;
    public Enums.ItemRarity rarity;
}
public class QuestRewardUI : MonoBehaviour
{
    [SerializeField] private QuestRewardCard cardPrefab;
    [SerializeField] private Transform container;

    [SerializeField] private CollectableItemData AuroraDustData;

    [Header("Queue Settings")]
    [SerializeField] private float delayBetweenCards = 0.15f;
    [SerializeField] private int maxVisibleCards = 5;

    private int _activeCardCount = 0;
    private readonly Queue<RewardEntry> _pendingEntries = new();
    private CancellationTokenSource _cts;

    private void Start()
    {
        if (GameServices.TryGet<QuestService>(out var qs))
            qs.OnRewardsGranted += HandleRewardsGranted;
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();

        if (GameServices.TryGet<QuestService>(out var qs))
            qs.OnRewardsGranted -= HandleRewardsGranted;
    }

    private void HandleRewardsGranted(List<RewardEntry> entries)
    {
        foreach (var entry in entries)
            _pendingEntries.Enqueue(entry);

        // ������д���ѭ��û���ܣ�������
        _cts ??= new CancellationTokenSource();
        ProcessQueueAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid ProcessQueueAsync(CancellationToken ct)
    {
        while (_pendingEntries.Count > 0)
        {
            // �ȵ��п�λ
            await UniTask.WaitUntil(() => _activeCardCount < maxVisibleCards, cancellationToken: ct);

            if (_pendingEntries.Count == 0) break;

            var entry = _pendingEntries.Dequeue();
            SpawnCard(entry, ct);

            if (_pendingEntries.Count > 0)
                await UniTask.Delay(TimeSpan.FromSeconds(delayBetweenCards), cancellationToken: ct);
        }
    }

    private void SpawnCard(RewardEntry entry, CancellationToken ct)
    {
        _activeCardCount++;

        var card = Instantiate(cardPrefab, container);
        if (entry.type == RewardType.AuroraDust)
        {
            entry.icon = AuroraDustData.icon;
            entry.label = AuroraDustData.localizedDisplayName.GetLocalizedString();
            entry.rarity = AuroraDustData.itemRarity;
        }
        card.Setup(entry);
        card.PlayAndDestroy(ct, onDestroyed: () => _activeCardCount--).Forget();
    }
}
