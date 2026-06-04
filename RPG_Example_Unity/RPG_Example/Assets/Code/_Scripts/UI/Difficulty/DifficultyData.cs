using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(menuName = "Game/Difficulty")]
public class DifficultyData : ScriptableObject
{
    public LocalizedString displayName;
    public LocalizedString description;
    public Sprite icon;
}