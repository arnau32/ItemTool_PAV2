using Cysharp.Threading.Tasks;
using static VfxPoolService;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class QuestRewardCard : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private CanvasGroup canvasGroup;

    [SerializeField] private Image glowSmall;
    [SerializeField] private Image glowLarge;
    [SerializeField] private Image iconBg;
    [SerializeField] private Image bannerBg;

    [SerializeField] private RarityColorConfig glowSmallColor;
    [SerializeField] private RarityColorConfig glowLargeColor;
    [SerializeField] private RarityColorConfig iconBgColor;
    [SerializeField] private RarityColorConfig bannerBgColor;

    [Header("Timing")]
    [SerializeField] private float fadeInDuration;
    [SerializeField] private float stayDuration;
    [SerializeField] private float fadeOutDuration;

    public float TotalDuration => fadeInDuration + stayDuration + fadeOutDuration;

    public void Setup(RewardEntry entry)
    {
        iconImage.sprite = entry.icon;
        labelText.text = entry.label;
        quantityText.text = entry.quantity > 1 ? $"x{entry.quantity}" : "";
        canvasGroup.alpha = 0f;

        ApplyRarityColors(entry.rarity);
    }

    public async UniTaskVoid PlayAndDestroy(CancellationToken ct)
    {
        try
        {
            await Fade(0f, 1f, fadeInDuration, ct);

            await UniTask.Delay(
                TimeSpan.FromSeconds(stayDuration),
                cancellationToken: ct);

            await Fade(1f, 0f, fadeOutDuration, ct);
        }
        finally
        {
            Destroy(gameObject);
        }
    }

    private async UniTask Fade(float from, float to, float duration, CancellationToken ct)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            await UniTask.NextFrame(ct);
        }
        canvasGroup.alpha = to;
    }

    private void ApplyRarityColors(Enums.ItemRarity rarity)
    {
        if (glowSmall != null) glowSmall.color = glowSmallColor.GetColor(rarity);
        if (glowLarge != null) glowLarge.color = glowLargeColor.GetColor(rarity);
        if (iconBg != null) iconBg.color = iconBgColor.GetColor(rarity);
        if (bannerBg != null) bannerBg.color = bannerBgColor.GetColor(rarity);
    }
    public async UniTaskVoid PlayAndDestroy(CancellationToken ct, Action onDestroyed = null)
    {
        try
        {
            await Fade(0f, 1f, fadeInDuration, ct);

            await UniTask.Delay(
                TimeSpan.FromSeconds(stayDuration),
                cancellationToken: ct);

            await Fade(1f, 0f, fadeOutDuration, ct);
        }
        finally
        {
            onDestroyed?.Invoke();
            Destroy(gameObject);
        }
    }
}