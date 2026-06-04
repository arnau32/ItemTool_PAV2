using UnityEngine;

public class InventoryCursorSensitivity : MonoBehaviour
{
    public static InventoryCursorSensitivity Instance;

    [Header("Runtime Values")]
    public float moveRepeatDelay = 0.25f;
    public float moveRepeatRate = 0.25f;
    public float deadZone = 0.3f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    public void ApplySensitivity(float t)
    {
        t = Mathf.SmoothStep(0, 1, t);

        moveRepeatDelay = Mathf.Lerp(0.25f, 0.05f, t);

        moveRepeatRate = Mathf.Lerp(0.25f, 0.03f, t);

        deadZone = Mathf.Lerp(0.4f, 0.2f, t);

    }
}
