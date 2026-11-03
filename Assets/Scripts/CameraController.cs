using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform player, cameraPivot;
    public float mouseSensitivity = 200f, distance = 23f, height = 11f;
    public float minPitch = -35f, maxPitch = 70f;
    public float collisionRadius = 0.6f, returnSpeed = 8f;
    public LayerMask obstructionLayers = ~0;
    private float pitch, yaw, currentDistance;
    void Start()
    {
        yaw = cameraPivot != null ? cameraPivot.eulerAngles.y : transform.eulerAngles.y;
        pitch = 15f;
        currentDistance = distance;
    }
    void LateUpdate()
    {
        if (player == null || cameraPivot == null || Time.timeScale == 0f) return;
        if (Cursor.lockState == CursorLockMode.Locked && !(player.GetComponent<BunnyHealth>()?.IsDead ?? false))
        {
            // Legacy mouse axes already represent frame mouse delta.
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity * 0.01f;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity * 0.01f, minPitch, maxPitch);
        }
        cameraPivot.position = player.position + Vector3.up * height;
        cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
        float safeDistance = distance;
        foreach (var hit in Physics.SphereCastAll(cameraPivot.position, collisionRadius, -cameraPivot.forward, distance, obstructionLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(player)) continue;
            safeDistance = Mathf.Min(safeDistance, Mathf.Max(0.1f, hit.distance - 0.2f));
        }
        currentDistance = safeDistance < currentDistance ? safeDistance : Mathf.Lerp(currentDistance, safeDistance, 1f - Mathf.Exp(-returnSpeed * Time.deltaTime));
        transform.SetPositionAndRotation(cameraPivot.position - cameraPivot.forward * currentDistance, cameraPivot.rotation);
    }
}
