using UnityEngine;
using UnityEngine.EventSystems;

public class DifficultyOptionUI : MonoBehaviour,
    IPointerEnterHandler,
    ISelectHandler
{
    [SerializeField] private DifficultyData difficulty;
    [SerializeField] private DifficultyInfoPanel infoPanel;

    public void OnPointerEnter(PointerEventData eventData)
    {
        RefreshInfo();
    }

    public void OnSelect(BaseEventData eventData)
    {
        RefreshInfo();
    }

    private void RefreshInfo()
    {
        if (difficulty == null || infoPanel == null)
            return;

        infoPanel.Show(difficulty);
    }
}