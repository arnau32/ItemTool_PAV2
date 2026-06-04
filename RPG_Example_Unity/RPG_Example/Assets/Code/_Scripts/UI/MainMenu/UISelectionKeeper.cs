using UnityEngine;
using UnityEngine.EventSystems;

public class UISelectionKeeper : MonoBehaviour
{
    [SerializeField] public GameObject defaultSelected;

    void Update()
    {
        var es = EventSystem.current;

        if (es == null) return;

        if (es.currentSelectedGameObject == null)
        {
            if (HasGamepadInput())
            {
                es.SetSelectedGameObject(defaultSelected);
            }
        }
    }

    private bool HasGamepadInput()
    {
        return Input.GetAxis("Horizontal") != 0 ||
               Input.GetAxis("Vertical") != 0 ||
               Input.GetButtonDown("Submit");
    }
}