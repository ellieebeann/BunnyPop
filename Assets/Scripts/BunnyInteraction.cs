using TMPro;
using UnityEngine;

// Player-owned selection prevents one E press collecting several overlapping seeds.
[RequireComponent(typeof(BunnyHealth))]
public class BunnyInteraction : MonoBehaviour
{
    public float interactionRange = 12f;
    public TMP_Text prompt;
    private Seed selected;
    private BunnyHealth health;
    private readonly Collider[] nearby = new Collider[64];
    void Awake() { health = GetComponent<BunnyHealth>(); }
    void Update()
    {
        selected = null;
        if (!health.IsDead && Time.timeScale > 0f && Cursor.lockState == CursorLockMode.Locked)
        {
            var controller = GetComponent<CharacterController>();
            Vector3 origin = controller != null ? controller.bounds.center : transform.position;
            float nearest = interactionRange * interactionRange;
            int count = Physics.OverlapSphereNonAlloc(origin, interactionRange, nearby, Physics.AllLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var seed = nearby[i].GetComponentInParent<Seed>();
                if (seed == null || !seed.CanCollect) continue;
                Vector3 target = nearby[i].ClosestPoint(origin);
                float distance = (target - origin).sqrMagnitude;
                if (distance >= nearest || !HasSight(origin, nearby[i].bounds.center, seed)) continue;
                nearest = distance;
                selected = seed;
            }
            if (selected != null && Input.GetKeyDown(KeyCode.E)) { selected.Collect(health); selected = null; }
        }
        if (prompt != null)
        {
            prompt.text = selected != null ? "Press E to collect seed" : "";
            prompt.gameObject.SetActive(selected != null);
        }
    }
    bool HasSight(Vector3 origin, Vector3 target, Seed seed)
    {
        Vector3 delta = target - origin;
        foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<Seed>() == seed) continue;
            return false;
        }
        return true;
    }
    void OnDisable() { if (prompt != null) prompt.gameObject.SetActive(false); }
}
