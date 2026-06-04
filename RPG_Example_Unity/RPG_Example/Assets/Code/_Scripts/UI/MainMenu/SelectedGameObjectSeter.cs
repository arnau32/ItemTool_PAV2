using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectedGameObjectSeter : MonoBehaviour
{
    public GameObject firstSelectedButton;

    private Coroutine _selectRoutine;

    private void OnEnable()
    {
        Application.focusChanged += OnApplicationFocus;
        SetFirstSelected();
    }

    private void OnDisable()
    {
        Application.focusChanged -= OnApplicationFocus;
        StopSelectRoutine();
    }

    public void SetFirstSelected()
    {
        StopSelectRoutine();
        _selectRoutine = StartCoroutine(SelectRoutine());
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            SetFirstSelected();
    }

    private IEnumerator SelectRoutine()
    {
        yield return null;

        if (firstSelectedButton == null || EventSystem.current == null) yield break;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(firstSelectedButton);
        _selectRoutine = null;
    }

    private void StopSelectRoutine()
    {
        if (_selectRoutine != null)
        {
            StopCoroutine(_selectRoutine);
            _selectRoutine = null;
        }
    }
}
