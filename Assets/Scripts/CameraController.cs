using UnityEngine;

[DefaultExecutionOrder(30)]
public class CameraController : MonoBehaviour
{
    public Transform player, cameraPivot;
    public float mouseSensitivity = 200f, distance = 23f, height = 11f;
    public float minPitch = -35f, maxPitch = 70f;
    public float shoulderOffset = 4.5f;
    public float collisionRadius = 0.6f, returnSpeed = 8f;
    public LayerMask obstructionLayers = ~0;
    private float pitch, yaw, currentDistance;
    public Vector3 PlanarForward => Quaternion.Euler(0, yaw, 0) * Vector3.forward;
    void Start()
    {
        yaw = cameraPivot != null ? cameraPivot.eulerAngles.y : transform.eulerAngles.y;
        pitch = 8f;
        currentDistance = Mathf.Sqrt(distance * distance + shoulderOffset * shoulderOffset);
        // The rig must not inherit Bunny's movement/aim rotation or animation scale.
        if (cameraPivot != null && player != null && cameraPivot.IsChildOf(player))
            cameraPivot.SetParent(null, true);
        if (cameraPivot != null) cameraPivot.localScale = Vector3.one;
        if (transform.parent != null && (transform.IsChildOf(player) || transform.IsChildOf(cameraPivot))) transform.SetParent(null, true);
        transform.localScale = Vector3.one;
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
        Vector3 cameraOffset = cameraPivot.right * shoulderOffset - cameraPivot.forward * distance;
        float safeDistance = cameraOffset.magnitude;
        Vector3 cameraDirection = cameraOffset.normalized;
        foreach (var hit in Physics.SphereCastAll(cameraPivot.position, collisionRadius, cameraDirection, safeDistance, obstructionLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(player) || hit.collider.GetComponentInParent<EnemyHealth>() != null
                || hit.collider.GetComponentInParent<Seed>() != null || hit.collider.GetComponentInParent<WandProjectile>() != null) continue;
            safeDistance = Mathf.Min(safeDistance, Mathf.Max(0.1f, hit.distance - 0.2f));
        }
        currentDistance = safeDistance < currentDistance ? safeDistance : Mathf.Lerp(currentDistance, safeDistance, 1f - Mathf.Exp(-returnSpeed * Time.deltaTime));
        transform.SetPositionAndRotation(cameraPivot.position + cameraDirection * currentDistance, cameraPivot.rotation);
    }
}
