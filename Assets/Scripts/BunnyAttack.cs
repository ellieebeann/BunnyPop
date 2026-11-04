using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class BunnyAttack : MonoBehaviour
{
    public Transform attackPoint;
    public float attackRange = 9f;
    public LayerMask enemyLayers = ~0;
    public int attackDamage = 20;
    public AudioClip attackSound;
    public float attackCooldown = 0.65f, hitDelay = 0.2f;
    [Header("Carrot wand")]
    [FormerlySerializedAs("carrotProjectile"), FormerlySerializedAs("bubbleProjectile")]
    [Tooltip("Drag in a prefab with WandProjectile on its root.")]
    public WandProjectile projectilePrefab;
    [FormerlySerializedAs("carrotDamage"), FormerlySerializedAs("bubbleDamage")]
    public int projectileDamage = 25;
    public Camera aimCamera;
    public GameObject crosshair;
    public int maxCarrotCharges = 5;
    public int CarrotCharges { get; private set; }
    private bool rangedAttack;

    private BunnyHealth health;
    private CharacterController body;

    void Awake()
    {
        CarrotCharges = maxCarrotCharges;
        health = GetComponent<BunnyHealth>();
        body = GetComponent<CharacterController>();
    }

    void LateUpdate()
    {
        if (crosshair == null) return;
        bool visible = Time.timeScale > 0f && Cursor.lockState == CursorLockMode.Locked
            && (health == null || !health.IsDead);
        if (crosshair.activeSelf != visible) crosshair.SetActive(visible);
    }

    public bool AddCarrotCharges(int amount)
    {
        if (amount <= 0 || CarrotCharges >= maxCarrotCharges) return false;
        CarrotCharges = Mathf.Min(maxCarrotCharges, CarrotCharges + amount);
        return true;
    }

    public bool IsAttacking { get; private set; }
    private float nextAttack;
    private bool hitApplied;
    private readonly HashSet<EnemyHealth> damaged = new HashSet<EnemyHealth>();

    public bool TryAttack(bool weapon)
    {
        if (Time.timeScale == 0f || IsAttacking || Time.time < nextAttack || (health != null && health.IsDead)) return false;
        if (weapon && (projectilePrefab == null || CarrotCharges <= 0)) return false;
        rangedAttack = weapon;
        if (weapon && aimCamera != null) FaceAim();
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
        if (rangedAttack)
        {
            CarrotCharges--;
            if (aimCamera != null) FaceAim();
            Vector3 launchPoint = body != null ? body.bounds.center : transform.position + Vector3.up;
            var projectile = Instantiate(projectilePrefab, launchPoint, Quaternion.LookRotation(AimDirection(launchPoint)));
            projectile.Launch(transform, projectileDamage, enemyLayers.value == 0 ? Physics.AllLayers : enemyLayers);
            return;
        }
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
    void OnDisable()
    {
        CancelAttack();
        if (crosshair != null) crosshair.SetActive(false);
    }

    void FaceAim()
    {
        Vector3 forward = Vector3.ProjectOnPlane(aimCamera.transform.forward, Vector3.up);
        if (forward.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(forward);
    }

    Vector3 AimDirection(Vector3 launchPoint)
    {
        if (aimCamera == null) return transform.forward;
        Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        float muzzleDepth = Mathf.Max(0f, Vector3.Dot(launchPoint - ray.origin, ray.direction));
        float nearest = muzzleDepth + projectilePrefab.range;
        foreach (var hit in Physics.RaycastAll(ray, nearest, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.distance <= muzzleDepth + 0.05f || hit.distance >= nearest) continue;
            nearest = hit.distance;
        }
        return (ray.GetPoint(nearest) - launchPoint).normalized;
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint != null ? attackPoint.position : transform.position + Vector3.up * 5f + transform.forward * (attackRange * 0.5f), attackRange);
    }
}
