using UnityEngine;

public class SceneViewCameraController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float fastMultiplier = 3f;
    [SerializeField] private float speedScrollMultiplier = 2f;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 2f;
    [SerializeField] private bool lockCursorWhileLooking = true;

    private float yaw;
    private float pitch;
    private float currentSpeed;

    private void Start()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;

        currentSpeed = moveSpeed;
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();
        HandleSpeedScroll();
    }

    private void HandleLook()
    {
        if (Input.GetMouseButtonDown(1))
        {
            if (lockCursorWhileLooking)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        if (Input.GetMouseButtonUp(1))
        {
            if (lockCursorWhileLooking)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        if (!Input.GetMouseButton(1))
            return;

        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

        yaw += mouseX;
        pitch -= mouseY;

        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void HandleMovement()
    {
        Vector3 inputDirection = Vector3.zero;

        if (Input.GetKey(KeyCode.W))
            inputDirection += transform.forward;

        if (Input.GetKey(KeyCode.S))
            inputDirection -= transform.forward;

        if (Input.GetKey(KeyCode.D))
            inputDirection += transform.right;

        if (Input.GetKey(KeyCode.A))
            inputDirection -= transform.right;

        if (Input.GetKey(KeyCode.E))
            inputDirection += Vector3.up;

        if (Input.GetKey(KeyCode.Q))
            inputDirection -= Vector3.up;

        if (inputDirection.sqrMagnitude > 1f)
            inputDirection.Normalize();

        float finalSpeed = currentSpeed;

        if (Input.GetKey(KeyCode.LeftShift))
            finalSpeed *= fastMultiplier;

        transform.position += inputDirection * finalSpeed * Time.unscaledDeltaTime;
    }

    private void HandleSpeedScroll()
    {
        float scroll = Input.mouseScrollDelta.y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        currentSpeed += scroll * speedScrollMultiplier;
        currentSpeed = Mathf.Max(0.1f, currentSpeed);
    }
}