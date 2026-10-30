using UnityEngine;

public class SlimeAI : MonoBehaviour
{
    public Transform player;
    private Animator animator;

    [Header("AI Distances")]
    public float detectionRadius = 28.8f;
    public float attackRadius = 8.47f;
    public float roamRadius = 33.5f;

    [Header("Slime Speeds")]
    public float walkSpeed = 10f;
    public float chaseSpeed = 15f;
    public float rotationSpeed = 10f;

    [Header("Attack Setup")]
    public float attackCooldown = 1.2f;
    private float lastAttackTime;

    [Header("Random Idle/Emote Timing")]
    public float minEmoteInterval = 2f;
    public float maxEmoteInterval = 5f;
    private float nextEmoteTime;

    private Vector3 startPosition;
    private Vector3 roamTarget;
    private bool isChasing = false;
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
        ScheduleNextRandomAction();
    }

    void Update()
    {
        if (isDead) return;

        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // State 1: Pumasok ang Player sa Detection Radius -> Chase o Attack
        if (distanceToPlayer <= detectionRadius)
        {
            isChasing = true;

            if (distanceToPlayer <= attackRadius)
            {
                AttackPlayer();
            }
            else
            {
                ChasePlayer();
            }
        }
        // State 2: Malayo ang Player -> Idle, Roam, at Random Emotes (Dance, Jump, Walk)
        else
        {
            isChasing = false;
            HandleRandomBehavior();
        }
    }

    void HandleRandomBehavior()
    {
        // Kapag oras na para sa panibagong random action habang hindi nag-cha-chase
        if (Time.time >= nextEmoteTime)
        {
            PerformRandomEmote();
            ScheduleNextRandomAction();
        }

        // Maglakad patungo sa roamTarget
        RoamAround();
    }

    void PerformRandomEmote()
    {
        if (animator == null) return;

        // Random pick mula 0 hanggang 2:
        // 0 = Dance, 1 = Jump, 2 = Bagong Roam Destination
        int randomAction = Random.Range(0, 3);

        switch (randomAction)
        {
            case 0:
                animator.SetTrigger("Dance");
                break;
            case 1:
                animator.SetTrigger("Jump");
                break;
            case 2:
                GetNewRoamTarget();
                break;
        }
    }

    void ScheduleNextRandomAction()
    {
        nextEmoteTime = Time.time + Random.Range(minEmoteInterval, maxEmoteInterval);
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
                animator.SetTrigger("Attack"); // Triggers Bite_Front
            }

            lastAttackTime = Time.time;
        }
    }

    public void TakeDamage()
    {
        if (isDead) return;

        if (animator != null)
        {
            animator.SetTrigger("Hit"); // Triggers HitRecieve
        }
    }

    public void TriggerDeath()
    {
        if (isDead) return;

        isDead = true;

        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsDead", true); // Triggers Death
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