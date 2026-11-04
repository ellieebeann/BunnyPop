using TMPro;
using UnityEngine;

[RequireComponent(typeof(BunnyHealth))]
public class BunnyInteraction : MonoBehaviour
{
    public float interactionRange = 12f;
    public TMP_Text prompt;
    private BunnyHealth health;
    private readonly Collider[] nearby = new Collider[64];

    void Awake() { health = GetComponent<BunnyHealth>(); }

    void Update()
    {
        string message = "";
        if (!health.IsDead && Time.timeScale > 0f && Cursor.lockState == CursorLockMode.Locked)
        {
            var controller = GetComponent<CharacterController>();
            Vector3 origin = controller != null ? controller.bounds.center : transform.position;
            Component selected = FindTarget(origin);
            if (selected is Seed seed)
            {
                message = seed.carrotCharges > 0 ? "Press E to collect wand charge" : "Press E to collect seed";
                if (Input.GetKeyDown(KeyCode.E) && seed.Collect(health)) message = "";
            }
            else if (selected is ForestGate gate)
            {
                message = "Press E to unlock forest gate";
                if (Input.GetKeyDown(KeyCode.E) && gate.Unlock(health)) message = "";
            }
        }
        if (prompt != null)
        {
            prompt.text = message;
            prompt.gameObject.SetActive(message.Length > 0);
        }
    }

    Component FindTarget(Vector3 origin)
    {
        Component selected = null;
        float nearest = interactionRange * interactionRange;
        int count = Physics.OverlapSphereNonAlloc(origin, interactionRange, nearby, Physics.AllLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider collider = nearby[i];
            var seed = collider.GetComponentInParent<Seed>();
            var gate = collider.GetComponentInParent<ForestGate>();
            Component target = seed != null && seed.CanCollect ? seed : null;
            if (target == null && gate != null && !gate.IsUnlocked) target = gate;
            if (target == null) continue;

            // ClosestPoint only supports primitive and convex colliders.
            float distance = (collider.bounds.ClosestPoint(origin) - origin).sqrMagnitude;
            if (distance >= nearest || !HasSight(origin, collider.bounds.center, target.transform)) continue;
            nearest = distance;
            selected = target;
        }
        return selected;
    }

    bool HasSight(Vector3 origin, Vector3 target, Transform interactable)
    {
        Vector3 delta = target - origin;
        foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(interactable)) continue;
            return false;
        }
        return true;
    }

    void OnDisable() { if (prompt != null) prompt.gameObject.SetActive(false); }
}
