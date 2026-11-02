using UnityEngine;

public class OrcAI : MonoBehaviour
{
    public Transform player;
    private Animator animator;

    [Header("AI Distances")]
    public float detectionRadius = 35f;
    public float attackRadius = 14f;
    public float roamRadius = 41f;

    [Header("Orc Movement Speeds")]
    public float walkSpeed = 10f;
    public float chaseSpeed = 15f;
    public float rotationSpeed = 10f;

    [Header("Attack Setup")]
    public float attackCooldown = 2f;
    private float lastAttackTime;

    private Vector3 startPosition;
    private Vector3 roamTarget;
    private bool isDead = false;

    void Start()
    {
        animator = GetComponent<Animator>();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        startPosition = transform.position;
        GetNewRoamTarget();
    }

    void Update()
    {
        if (isDead) return;

        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRadius)
        {
            if (distanceToPlayer <= attackRadius)
            {
                AttackPlayer();
            }
            else
            {
                ChasePlayer();
            }
        }
        else
        {
            RoamAround();
        }
    }

    void RoamAround()
    {
        float distanceToTarget = Vector3.Distance(transform.position, roamTarget);

        if (distanceToTarget > 0.5f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                roamTarget,
                walkSpeed * Time.deltaTime
            );

            Vector3 direction = (roamTarget - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }

            if (animator != null) animator.SetBool("IsWalking", true);
        }
        else
        {
            if (animator != null) animator.SetBool("IsWalking", false);
            GetNewRoamTarget();
        }
    }

    void GetNewRoamTarget()
    {
        Vector2 randomCircle = Random.insideUnitCircle * roamRadius;
        roamTarget = new Vector3(
            startPosition.x + randomCircle.x,
            startPosition.y,
            startPosition.z + randomCircle.y
        );
    }

    void ChasePlayer()
    {
        if (animator != null) animator.SetBool("IsWalking", true);

        Vector3 targetPos = new Vector3(player.position.x, transform.position.y, player.position.z);

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPos,
            chaseSpeed * Time.deltaTime
        );

        Vector3 direction = (targetPos - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }

    void AttackPlayer()
    {
        if (animator != null) animator.SetBool("IsWalking", false);

        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0f;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            if (animator != null)
            {
                animator.SetTrigger("Attack"); // Triggers Weapon Animation
            }

            lastAttackTime = Time.time;
        }
    }

    public void TakeDamage()
    {
        if (isDead) return;

        if (animator != null)
        {
            animator.SetTrigger("Hit"); // Triggers HitReact Animation
        }
    }

    public void TriggerDeath()
    {
        if (isDead) return;

        isDead = true;

        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsDead", true); // Triggers Death Animation
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(Application.isPlaying ? startPosition : transform.position, roamRadius);
    }
}