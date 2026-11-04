using UnityEngine;
using UnityEngine.AI;

public sealed class ForestGate : MonoBehaviour
{
    public Transform hinge;
    public Collider gateCollider;
    public NavMeshObstacle obstacle;
    public EnemyAI sentry;
    public float openingSpeed = 180f;
    public bool IsUnlocked { get; private set; }
    private BunnyHealth player;
    private Quaternion openRotation;

    public bool Unlock(BunnyHealth opener)
    {
        if (IsUnlocked || opener == null || opener.IsDead || Time.timeScale == 0f) return false;
        player = opener;
        IsUnlocked = true;
        openRotation = hinge.localRotation * Quaternion.Euler(0f, -95f, 0f);
        if (gateCollider != null) gateCollider.enabled = false;
        if (obstacle != null) obstacle.enabled = false;
        return true;
    }

    void Update()
    {
        if (!IsUnlocked) return;
        hinge.localRotation = Quaternion.RotateTowards(hinge.localRotation, openRotation, openingSpeed * Time.deltaTime);
        if (sentry == null || sentry.gameObject.activeSelf || player == null || player.IsDead) return;

        Vector3 entry = transform.InverseTransformPoint(player.transform.position);
        if (entry.z < 2f || Mathf.Abs(entry.x) > 12f) return;
        sentry.player = player.transform;
        sentry.gameObject.SetActive(true);
    }
}
