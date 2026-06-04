using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DifficultyInfoPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private Image iconImage;
    [SerializeField] private Animator animator;
    [SerializeField] private string showAnimationStateName = "In";

    private DifficultyData currentDifficulty;

    public void Show(DifficultyData difficulty)
    {
        if (difficulty == null)
            return;

        bool changed = currentDifficulty != difficulty;

        ClearBindings();

        currentDifficulty = difficulty;

        currentDifficulty.displayName.StringChanged += UpdateName;
        currentDifficulty.description.StringChanged += UpdateDescription;

        currentDifficulty.displayName.RefreshString();
        currentDifficulty.description.RefreshString();

        iconImage.sprite = difficulty.icon;
        iconImage.enabled = difficulty.icon != null;

        if (animator != null)
        {
            animator.Play(showAnimationStateName, 0, 0f);
            animator.Update(0f);
        }
    }

    private void UpdateName(string value)
    {
        nameLabel.text = value;
    }

    private void UpdateDescription(string value)
    {
        descriptionLabel.text = value;
    }

    private void OnDisable()
    {
        ClearBindings();
    }

    private void OnDestroy()
    {
        ClearBindings();
    }

    private void ClearBindings()
    {
        if (currentDifficulty == null)
            return;

        currentDifficulty.displayName.StringChanged -= UpdateName;
        currentDifficulty.description.StringChanged -= UpdateDescription;

        currentDifficulty = null;
    }
}