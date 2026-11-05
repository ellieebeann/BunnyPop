using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using TMPro; // Required for TextMeshProUGUI

[DefaultExecutionOrder(-30)]
public class BunnyAttack : MonoBehaviour
{
    public Transform attackPoint;
    public float attackRange = 9f;
    public LayerMask enemyLayers = ~0;
    public int attackDamage = 20;
    public AudioClip attackSound;
    public float attackCooldown = 0.65f, hitDelay = 0.2f;

    [Header("Carrot Wand")]
    [FormerlySerializedAs("carrotProjectile"), FormerlySerializedAs("bubbleProjectile")]
    [Tooltip("Drag in a prefab with WandProjectile on its root.")]
    public WandProjectile projectilePrefab;
    [FormerlySerializedAs("carrotDamage"), FormerlySerializedAs("bubbleDamage")]
    public int projectileDamage = 25;
    public AudioClip wandAttackSound; // Custom sound effect for carrot wand attack
    public ParticleSystem castEffect;
    public Transform wandTip;
    public bool requiresWandChest;
    [Tooltip("Drag the held carrot's renderers here. Hidden until the wand is unlocked; the animation rig stays active.")]
    public Renderer[] wandRenderers;
    public bool HasWand { get; private set; }
    public bool IsAiming => isAiming;

    [Header("UI Reference")]
    [Tooltip("Drag your TextMeshProUGUI component here (e.g., UI text under BunnyInventory).")]
    public TextMeshProUGUI chargesText;
    [Tooltip("Prefix display text before charge count.")]
    public string prefixText = "Bubbles Left: ";

    [Header("Aim & Camera Settings")]
    public Camera aimCamera;
    public GameObject crosshair;
    public KeyCode aimKey = KeyCode.Q;
    public float normalFOV = 60f;
    public float zoomedFOV = 35f;
    public float zoomSpeed = 10f;

    [Header("Carrot Charges")]
    public int maxCarrotCharges = 5;
    public int CarrotCharges { get; private set; }

    private bool isAiming;
    private bool rangedAttack;
    private BunnyHealth health;
    private CharacterController body;

    void Awake()
    {
        CarrotCharges = maxCarrotCharges;
        HasWand = !requiresWandChest;
        UpdateWandVisibility();
        health = GetComponent<BunnyHealth>();
        body = GetComponent<CharacterController>();

        if (aimCamera != null && normalFOV <= 0f)
        {
            normalFOV = aimCamera.fieldOfView;
        }

        UpdateChargesUI();
    }

    void Update()
    {
        if (Time.timeScale == 0f || (health != null && health.IsDead) || Cursor.lockState != CursorLockMode.Locked)
        {
            isAiming = false;
            return;
        }

        // Check if Q key is held down for Aim Stance
        isAiming = HasWand && Input.GetKey(aimKey);

        // Single Left Click Input Handler
        if (Input.GetMouseButtonDown(0))
        {
            // Holding Q -> Ranged Wand Attack (true)
            // Releasing Q -> Melee Punch (false)
            TryAttack(isAiming);
        }

        // Smooth camera FOV zooming while aiming with Q
        if (aimCamera != null)
        {
            float targetFOV = isAiming ? zoomedFOV : normalFOV;
            aimCamera.fieldOfView = Mathf.Lerp(aimCamera.fieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
        }
    }

    void LateUpdate()
    {
        if (crosshair == null) return;

        // Display crosshair only while holding Q key and alive
        bool visible = Time.timeScale > 0f && (health == null || !health.IsDead) && isAiming;

        if (crosshair.activeSelf != visible)
        {
            crosshair.SetActive(visible);
        }

    }

    public void UnlockWand()
    {
        HasWand = true;
        UpdateWandVisibility();
        CarrotCharges = maxCarrotCharges;
        UpdateChargesUI();
    }

    void UpdateWandVisibility()
    {
        if (wandRenderers == null) return;
        foreach (var renderer in wandRenderers)
            if (renderer != null) renderer.forceRenderingOff = !HasWand;
    }

    public bool AddCarrotCharges(int amount)
    {
        if (amount <= 0 || CarrotCharges >= maxCarrotCharges) return false;
        CarrotCharges = Mathf.Min(maxCarrotCharges, CarrotCharges + amount);
        UpdateChargesUI();
        return true;
    }

    public bool IsAttacking { get; private set; }
    private float nextAttack;
    private bool hitApplied;
    private readonly HashSet<EnemyHealth> damaged = new HashSet<EnemyHealth>();

    public bool TryAttack(bool weapon)
    {
        if (Time.timeScale == 0f || IsAttacking || Time.time < nextAttack || (health != null && health.IsDead)) return false;

        // Check projectile availability for ranged attack
        if (weapon && (!HasWand || projectilePrefab == null || CarrotCharges <= 0)) return false;

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

    public void PerformAttack()
    {
        if (!IsAttacking || hitApplied || (health != null && health.IsDead)) return;
        hitApplied = true;

        // 1. RANGED CARROT WAND ATTACK (Active when holding Q)
        if (rangedAttack)
        {
            AudioClip soundToPlay = wandAttackSound != null ? wandAttackSound : attackSound;
            if (soundToPlay != null) GameAudio.PlayWorld(soundToPlay, transform.position);

            CarrotCharges--;
            UpdateChargesUI();

            if (aimCamera != null) FaceAim();
            Vector3 launchPoint = body != null ? body.bounds.center : transform.position + Vector3.up;
            if (wandTip != null) launchPoint = wandTip.position;
            var projectile = Instantiate(projectilePrefab, launchPoint, Quaternion.LookRotation(AimDirection(launchPoint)));
            projectile.Launch(transform, projectileDamage, enemyLayers.value == 0 ? Physics.AllLayers : enemyLayers);
            if (castEffect != null)
            {
                var effect = Instantiate(castEffect, launchPoint, projectile.transform.rotation);
                effect.Play();
                Destroy(effect.gameObject, 3f);
            }
            return;
        }

        // 2. MELEE PUNCH ATTACK (Active when Q is released)
        if (attackSound != null) GameAudio.PlayWorld(attackSound, transform.position);

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

    private void UpdateChargesUI()
    {
        if (chargesText != null)
        {
            chargesText.text = HasWand ? prefixText + CarrotCharges + " / " + maxCarrotCharges : "Find the wand chest";
        }
    }

    private Vector3 origin => attackPoint != null ? attackPoint.position : transform.position + Vector3.up * 5f + transform.forward * (attackRange * 0.5f);

    public void CancelAttack()
    {
        StopAllCoroutines();
        IsAttacking = false;
        hitApplied = true;
    }

    void OnDisable()
    {
        CancelAttack();
        isAiming = false;
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
        Gizmos.DrawWireSphere(origin, attackRange);
    }
}
