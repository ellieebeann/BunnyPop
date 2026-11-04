using UnityEngine;

public sealed class WandProjectile : MonoBehaviour
{
    public float speed = 58f;
    public float range = 65f;
    public float radius = 0.55f;
    private Transform owner;
    private int damage;
    private LayerMask enemyLayers;
    private float travelled;

    public void Launch(Transform source, int hitDamage, LayerMask layers)
    {
        owner = source;
        damage = hitDamage;
        enemyLayers = layers;
    }

    void Update()
    {
        float step = Mathf.Min(speed * Time.deltaTime, range - travelled);
        if (step <= 0f)
        {
            if (travelled >= range) Destroy(gameObject);
            return;
        }

        RaycastHit closest = default;
        foreach (var hit in Physics.SphereCastAll(transform.position, radius, transform.forward, step,
                     Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (owner != null && hit.transform.IsChildOf(owner)) continue;
            if (closest.collider == null || hit.distance < closest.distance) closest = hit;
        }

        if (closest.collider != null)
        {
            if ((enemyLayers.value & (1 << closest.collider.gameObject.layer)) != 0)
                closest.collider.GetComponentInParent<EnemyHealth>()?.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        transform.position += transform.forward * step;
        travelled += step;
    }
}
