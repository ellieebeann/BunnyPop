using UnityEngine;

public class Seed : MonoBehaviour
{
    public int seedCount = 1;
    public AudioClip seedSound;
    public int carrotCharges;
    private bool collected;
    public bool CanCollect => !collected && isActiveAndEnabled;

    // Resolve against physical surfaces, not the height of a flying enemy or the NavMesh.
    // Keep the existing collectible Animator and place its whole root above the ground.
    public void SettleOnGround(Transform ignored = null)
    {
        Vector3 origin = transform.position + Vector3.up * 120f;
        RaycastHit best = default;
        foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 1000f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || (ignored != null && hit.transform.IsChildOf(ignored))
                || hit.collider.GetComponentInParent<EnemyHealth>() != null
                || hit.collider.GetComponentInParent<BunnyHealth>() != null
                || hit.collider.GetComponentInParent<Seed>() != null || hit.normal.y < 0.5f) continue;
            // Do not leave drops on a tree canopy or a roof above the enemy.
            if (hit.point.y > transform.position.y + 2f) continue;
            if (best.collider == null || hit.point.y > best.point.y) best = hit;
        }
        if (best.collider == null) return;
        float bottom = transform.position.y;
        bool found = false;
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            bottom = found ? Mathf.Min(bottom, renderer.bounds.min.y) : renderer.bounds.min.y;
            found = true;
        }
        transform.position += Vector3.up * (best.point.y + 0.55f - bottom);
    }

    public bool Collect(BunnyHealth player)
    {
        if (!CanCollect || player == null || player.IsDead || Time.timeScale == 0f || GameManager.instance == null) return false;
        if (carrotCharges > 0)
        {
            var attack = player.GetComponent<BunnyAttack>();
            if (attack == null || !attack.AddCarrotCharges(carrotCharges)) return false;
        }
        collected = true;
        if (carrotCharges <= 0) GameManager.instance.AddSeeds(seedCount);
        if (seedSound != null) GameAudio.PlayWorld(seedSound, transform.position);
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }
}
