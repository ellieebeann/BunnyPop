using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BunnyAttack : MonoBehaviour
{
    public Transform attackPoint;
    public float attackRange = 9f;
    public LayerMask enemyLayers = ~0;
    public int attackDamage = 20;
    public AudioClip attackSound;
    public float attackCooldown = 0.65f, hitDelay = 0.2f;
    public bool IsAttacking { get; private set; }
    private float nextAttack;
    private bool hitApplied;
    private readonly HashSet<EnemyHealth> damaged = new HashSet<EnemyHealth>();

    public bool TryAttack(bool weapon)
    {
        var health = GetComponent<BunnyHealth>();
        if (Time.timeScale == 0f || IsAttacking || Time.time < nextAttack || (health != null && health.IsDead)) return false;
        IsAttacking = true;
        hitApplied = false;
        nextAttack = Time.time + attackCooldown;
        var animator = GetComponent<Animator>();
        if (animator != null) animator.SetTrigger(weapon ? "AttackWeapon" : "Punch");
        StartCoroutine(AttackRoutine());
        return true;
    }

    IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(hitDelay);
        PerformAttack();
        yield return new WaitForSeconds(Mathf.Max(0f, attackCooldown - hitDelay));
        IsAttacking = false;
    }

    // Also safe as an animation event: each attack can apply damage only once.
    public void PerformAttack()
    {
        if (!IsAttacking || hitApplied) return;
        hitApplied = true;
        if (attackSound != null) AudioSource.PlayClipAtPoint(attackSound, transform.position);
        Vector3 origin = attackPoint != null ? attackPoint.position : transform.position + Vector3.up * 5f + transform.forward * (attackRange * 0.5f);
        damaged.Clear();
        int mask = enemyLayers.value == 0 ? Physics.AllLayers : enemyLayers.value;
        foreach (Collider hit in Physics.OverlapSphere(origin, attackRange, mask, QueryTriggerInteraction.Ignore))
        {
            var enemy = hit.GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead || damaged.Contains(enemy)) continue;
            var controller = GetComponent<CharacterController>();
            Vector3 sightOrigin = controller != null ? controller.bounds.center : transform.position + Vector3.up;
            Vector3 direction = hit.bounds.center - sightOrigin;
            if (Vector3.Dot(transform.forward, (hit.bounds.center - transform.position).normalized) < 0f) continue;
            bool blocked = false;
            foreach (var obstruction in Physics.RaycastAll(sightOrigin, direction.normalized, direction.magnitude, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                if (obstruction.transform.IsChildOf(transform) || obstruction.collider.GetComponentInParent<EnemyHealth>() == enemy) continue;
                blocked = true;
                break;
            }
            if (!blocked) { damaged.Add(enemy); enemy.TakeDamage(attackDamage); }
        }
    }

    public void CancelAttack() { StopAllCoroutines(); IsAttacking = false; hitApplied = true; }
    void OnDisable() { CancelAttack(); }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint != null ? attackPoint.position : transform.position + Vector3.up * 5f + transform.forward * (attackRange * 0.5f), attackRange);
    }
}
