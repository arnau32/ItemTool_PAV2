using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ScrollToSelectionCentered : MonoBehaviour
{
    public ScrollRect scrollRect;
    public float smoothSpeed = 10f;

    private RectTransform content;
    private RectTransform viewport;

    private float targetNormalizedPos;

    void Awake()
    {
        content = scrollRect.content;
        viewport = scrollRect.viewport;
    }

    void Update()
    {
        var selected = EventSystem.current.currentSelectedGameObject;

        if (selected == null) return;

        RectTransform target = selected.GetComponent<RectTransform>();
        if (target == null || !target.IsChildOf(content)) return;

        UpdateTargetPosition(target);

        // ⭐ 平滑移动
        scrollRect.verticalNormalizedPosition = Mathf.Lerp(
            scrollRect.verticalNormalizedPosition,
            targetNormalizedPos,
            Time.deltaTime * smoothSpeed
        );
    }

    void UpdateTargetPosition(RectTransform target)
    {
        Canvas.ForceUpdateCanvases();

        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;

        // 当前元素在 content 中的位置（从上往下）
        float itemPosY = Mathf.Abs(target.anchoredPosition.y);

        // ⭐ 核心：让它居中
        float centerOffset = viewportHeight * 0.5f;

        float targetY = itemPosY - centerOffset;

        float normalized = 1f - Mathf.Clamp01(
            targetY / (contentHeight - viewportHeight)
        );

        targetNormalizedPos = normalized;
    }
}