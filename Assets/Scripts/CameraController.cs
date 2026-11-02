using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform player;
    public Transform cameraPivot;

    public float mouseSensitivity = 200f;
    public float distance = 5f;
    public float height = 1.5f;

    public float minPitch = -80f;
    public float maxPitch = 80f;

    private float xRotation = 0f;
    private float yRotation = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraPivot != null)
        {
            yRotation = cameraPivot.eulerAngles.y;
        }
    }

    void LateUpdate()
    {
        if (player == null || cameraPivot == null) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Vertical camera rotation
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, minPitch, maxPitch);

        // Horizontal camera rotation
        yRotation += mouseX;

        // Update Camera Pivot Position and Rotation
        cameraPivot.position = player.position + Vector3.up * height;
        cameraPivot.rotation = Quaternion.Euler(xRotation, yRotation, 0f);

        // Set Camera Distance & Position relative to Pivot
        transform.position = cameraPivot.position - (cameraPivot.forward * distance);
        transform.rotation = cameraPivot.rotation;
    }
}