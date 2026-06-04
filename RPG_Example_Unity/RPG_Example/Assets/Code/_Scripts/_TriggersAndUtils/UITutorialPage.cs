using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITutorialPage : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _mainTitle;
    [SerializeField] private TextMeshProUGUI _tutorialDescription;

    public void SetImage(Sprite sprite) => _image.sprite = sprite;
    public void SetTitle(string title) => _mainTitle.text = title;
    public void SetDescription(string desc) => _tutorialDescription.text = desc;
}
