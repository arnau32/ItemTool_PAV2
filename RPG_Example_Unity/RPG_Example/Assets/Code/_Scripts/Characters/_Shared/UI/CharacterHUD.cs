using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class CharacterHUD
{
    [SerializeField] protected Image _lifeBar;

    public void UpdateHealthBar(float health01)
    {
        _lifeBar.fillAmount = health01;
    }
}
