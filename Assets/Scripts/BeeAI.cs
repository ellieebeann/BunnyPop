using UnityEngine;

public class BeeAI : MonoBehaviour
{
    public Transform player;
    private Animator animator;

    [Header("AI Distances")]
    public float detectionRadius = 54f;
    public float attackRadius = 17f;
    public float roamRadius = 68f;

    [Header("Bee Movement Speeds")]
    public float moveSpeed = 3.5f;
    public float chaseSpeed = 15f;
    public float rotationSpeed = 10f;

    [Header("Attack Setup")]
    public float attackCooldown = 1f;
    private float lastAttackTime;

    private Vector3 startPosition;
    private Vector3 roamTarget;
    private bool isDead = false;

    void Start()
    {
        animator = GetComponent<Animator>();

        // Hanapin ang Player gamit ang Tag na "Player"
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

        if (animator != null)
        {
            animator.SetBool("IsFlying", true);
        }
    }

    void Update()
    {
        if (isDead) return;

        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // State 1: Kapag nasa loob ng perimeter/detection radius ang Player -> Chase o Attack
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
        // State 2: Kapag malayo ang Player -> Mag-roam lang sa designated area
        else
        {
            RoamAround();
        }
    }

    void RoamAround()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            roamTarget,
            moveSpeed * Time.deltaTime
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

        if (Vector3.Distance(transform.position, roamTarget) < 0.5f)
        {
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
        // Face player habang nag-a-attack
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
                animator.SetTrigger("Attack");
            }

            lastAttackTime = Time.time;
        }
    }

    // Tawagin ito mula sa Player script kapag natamaan ng Carrot/Punch ang Bee
    public void TakeDamage()
    {
        if (isDead) return;

        if (animator != null)
        {
            animator.SetTrigger("Hit");
        }
    }

    public void TriggerDeath()
    {
        if (isDead) return;

        isDead = true;

        if (animator != null)
        {
            animator.SetBool("IsDead", true);
        }
    }

    // Visual Gizmos para makita ang perimeter radius sa Scene view
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