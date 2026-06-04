using UnityEngine;
using UnityEngine.InputSystem;

public class CinematicFastForward : MonoBehaviour
{
    [SerializeField] private float _fastScale = 2f;

    private void Update()
    {
        bool held = Gamepad.current != null && Gamepad.current.buttonSouth.isPressed;
        Time.timeScale = held ? _fastScale : 1f;
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
    }
}
