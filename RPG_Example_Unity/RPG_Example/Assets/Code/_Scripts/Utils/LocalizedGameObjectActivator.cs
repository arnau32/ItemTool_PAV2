using UnityEngine;

public class LocalizedGameObjectActivator : MonoBehaviour
{
    [SerializeField] private GameObject _englishObject;
    [SerializeField] private GameObject _spanishObject;

    private void Awake()
    {
        bool isSpanish = GameServices.Get<LanguageManager>().CurrentLanguage == GameLanguage.Spanish;

        if (_englishObject != null) _englishObject.SetActive(!isSpanish);
        if (_spanishObject != null) _spanishObject.SetActive(isSpanish);
    }
}