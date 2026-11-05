using UnityEngine;

public sealed class WandProjectile : MonoBehaviour
{
    public float speed = 58f;
    public float range = 65f;
    public float radius = 0.55f;
    public bool instantKill;
    public ParticleSystem popEffect;
    public AudioClip popSound;
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
        foreach (var hit in Physics.OverlapSphere(transform.position, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (owner != null && hit.transform.IsChildOf(owner)) continue;
            Impact(hit, transform.position);
            return;
        }
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
            Impact(closest.collider, closest.point);
            return;
        }

        transform.position += transform.forward * step;
        travelled += step;
    }

    void Impact(Collider collider, Vector3 point)
    {
        if ((enemyLayers.value & (1 << collider.gameObject.layer)) != 0)
        {
            var enemy = collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null) enemy.TakeDamage(instantKill ? enemy.maxHealth : damage);
        }
        Pop(point); Destroy(gameObject);
    }

    void Pop(Vector3 point)
    {
        if (popSound != null) GameAudio.PlayWorld(popSound, point, 0.8f);
        if (popEffect == null) return;
        var effect = Instantiate(popEffect, point, Quaternion.identity);
        effect.Play();
        Destroy(effect.gameObject, 3f);
    }
}
