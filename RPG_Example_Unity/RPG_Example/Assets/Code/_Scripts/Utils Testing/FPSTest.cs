#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

[ExecuteAlways]
public class FPSTest : MonoBehaviour
{
    [Header("Configuración de FPS")]
    [Tooltip("Usa las teclas 1, 2, 3... para cambiar a estos valores")]
    public int[] fpsButtons = new int[] { -1, 10, 30, 45, 60, 90, 120, 144, 240, 500 };

    void Update()
    {
        for (int i = 0; i < fpsButtons.Length; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SetFPS(fpsButtons[i]);
            }
        }
    }

    void SetFPS(int target)
    {
        Application.targetFrameRate = target;
        
        Debug.Log("Target FPS cambiado a: " + (target == -1 ? "Ilimitado" : target.ToString()));
    }
}
#endif