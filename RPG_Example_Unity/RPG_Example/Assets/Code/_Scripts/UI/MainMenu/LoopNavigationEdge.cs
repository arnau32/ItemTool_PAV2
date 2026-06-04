using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class LoopNavigationEdge : MonoBehaviour, IMoveHandler
{
    public Selectable targetSelectable;

    public MoveDirection triggerDirection;

    public void OnMove(AxisEventData eventData)
    {
        if (eventData.moveDir == triggerDirection)
        {
            if (targetSelectable != null)
            {
                EventSystem.current.SetSelectedGameObject(targetSelectable.gameObject);
                eventData.Use();
            }
        }
    }
}