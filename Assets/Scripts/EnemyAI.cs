using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// One state machine for all enemies; derived classes provide their animation flavour.
[RequireComponent(typeof(NavMeshAgent))]
public abstract class EnemyAI : MonoBehaviour
{
    public enum BehaviourState { Idle, Patrol, Chase, Search, ReturnHome, Attack, Dead }

    public Transform player;
    [Header("Territory")]
    public float detectionRadius = 35f, attackRadius = 12f, roamRadius = 40f;
    public float minIdleTime = 0.7f, maxIdleTime = 2f, memoryDuration = 3f;
    [Header("Movement")]
    public float walkSpeed = 10f, chaseSpeed = 15f, rotationSpeed = 10f;
    public float repathInterval = 0.35f, stuckTimeout = 2f;
    [Header("Attack")]
    public float attackCooldown = 1.2f, attackWindup = 0.3f;
    public int attackDamage = 10;
    public AudioClip attackSound;
    public float detectionHeight = 45f;
    [SerializeField] private BehaviourState state;
    public BehaviourState CurrentState => state;
    public Vector3 HomePosition => home;
    public Collider BlockingCollider { get; private set; }
    public bool HasNavigation => navigationReady && agent != null && agent.enabled && agent.isOnNavMesh;
    protected Animator animator;
    protected NavMeshAgent agent;
    protected bool isDead;
    protected virtual float PatrolSpeed => walkSpeed;
    protected virtual bool Flying => false;

    private Vector3 home, lastSeen, progressPosition;
    private float nextPath, nextAttack, idleUntil, lastSight = float.NegativeInfinity;
    private float progressTime, nextBind, recoveryUntil;
    private bool pursuing, attacking, navigationReady;
    private Collider body;
    private BunnyHealth playerHealth;
    private Terrain[] terrains;
    private NavMeshPath path;
    private AudioSource actionAudio;
    private readonly RaycastHit[] obstacles = new RaycastHit[64];

    protected virtual void Start()
    {
        path = new NavMeshPath();
        terrains = Terrain.activeTerrains;
        animator = GetComponent<Animator>();
        body = GetComponentInChildren<Collider>();
        agent = GetComponent<NavMeshAgent>();
        home = transform.position;
        if (player == null)
        {
            var found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }
        if (player != null) playerHealth = player.GetComponent<BunnyHealth>();
        agent.updatePosition = true;
        agent.updateRotation = false;
        agent.acceleration = 60f;
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        if (Flying && animator != null) animator.SetBool("IsFlying", true);
        if (BindToMesh()) BeginPatrol();
    }

    bool BindToMesh()
    {
        if (!agent.enabled) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        bool placed = false;
        // Flying spawn positions can otherwise bind to tiny islands on top of trees.
        // Find the walkable terrain under the spawn, then apply the bee's hover offset.
        Vector3 spawn = transform.position;
        for (int i = 0; i < 25; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            float radius = i == 0 ? 0f : (1 + (i - 1) / 8) * Mathf.Max(4f, agent.radius * 2f);
            Vector3 candidate = GroundHint(spawn + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
            if (!NavMesh.SamplePosition(candidate, out var hit, 5f, filter) || !IsGroundSurface(hit.position)) continue;
            if (agent.Warp(hit.position)) { placed = true; break; }
        }
        if (!placed)
        {
            navigationReady = false;
            if (agent.isOnNavMesh) agent.isStopped = true;
            nextBind = Time.time + 1f;
            SetWalking(false);
            return false;
        }
        transform.position = agent.nextPosition;
        navigationReady = true;
        home = agent.nextPosition;
        progressPosition = transform.position;
        progressTime = Time.time;
        return true;
    }

    protected virtual void Update()
    {
        if (isDead || agent == null || Time.timeScale == 0f) return;
        if (!HasNavigation)
        {
            if (Time.time >= nextBind && BindToMesh()) BeginPatrol();
            return;
        }
        UpdateFacing();
        if (playerHealth != null && playerHealth.IsDead) { StopMoving(); return; }
        if (attacking) { StopMoving(); FacePlayer(); return; }

        float distance = PlayerDistance();
        float territory = Mathf.Max(roamRadius + detectionRadius, detectionRadius * 1.5f);
        bool outsideTerritory = HorizontalDistance(transform.position, home) > territory;
        bool seesPlayer = !outsideTerritory && distance <= detectionRadius
            && (player == null || Mathf.Abs(player.position.y - transform.position.y) <= detectionHeight) && CanSeePlayer();

        if (seesPlayer)
        {
            lastSeen = player.position;
            lastSight = Time.time;
            pursuing = true;
        }
        if (outsideTerritory)
        {
            pursuing = false;
            Navigate(home, PatrolSpeed, 1f, BehaviourState.ReturnHome);
            if (Arrived()) BeginIdle();
        }
        else if (pursuing && seesPlayer && distance <= attackRadius)
        {
            state = BehaviourState.Attack;
            StopMoving();
            FacePlayer();
            if (Time.time >= nextAttack) StartCoroutine(Attack());
        }
        else if (pursuing && Time.time - lastSight <= memoryDuration)
        {
            Navigate(lastSeen, chaseSpeed, seesPlayer ? Mathf.Max(0.5f, attackRadius * 0.65f) : 1f,
                seesPlayer ? BehaviourState.Chase : BehaviourState.Search);
            if (!seesPlayer && Arrived()) { pursuing = false; BeginIdle(); }
        }
        else
        {
            if (pursuing)
            {
                pursuing = false;
                nextPath = 0f;
                state = BehaviourState.ReturnHome;
            }
            if (state == BehaviourState.ReturnHome)
            {
                Navigate(home, PatrolSpeed, 1f, BehaviourState.ReturnHome);
                if (Arrived()) BeginIdle();
            }
            else if (state == BehaviourState.Idle)
            {
                StopMoving();
                if (Time.time >= idleUntil) BeginPatrol();
            }
            else if (state != BehaviourState.Patrol || Arrived()) BeginIdle();
            else
            {
                agent.isStopped = false;
                RecoverIfStuck();
            }
        }
        if (!agent.isStopped && BlockedAhead()) agent.isStopped = true;
        SetWalking(!agent.isStopped && agent.velocity.sqrMagnitude > 0.15f);
    }

    void Navigate(Vector3 destination, float speed, float stoppingDistance, BehaviourState nextState)
    {
        state = nextState;
        agent.speed = speed;
        agent.stoppingDistance = stoppingDistance;
        agent.isStopped = false;
        if (Time.time >= nextPath && Time.time >= recoveryUntil)
        {
            nextPath = Time.time + Mathf.Max(0.1f, repathInterval);
            if (TryPath(destination, 12f)) agent.SetDestination(path.corners[path.corners.Length - 1]);
            else { pursuing = false; BeginIdle(); }
        }
        RecoverIfStuck();
    }

    bool TryPath(Vector3 target, float sampleDistance)
    {
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        return NavMesh.SamplePosition(GroundHint(target), out var hit, sampleDistance, filter) && IsGroundSurface(hit.position)
            && agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    Vector3 GroundHint(Vector3 position)
    {
        foreach (var terrain in terrains)
        {
            Vector3 local = position - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z) continue;
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            break;
        }
        return position;
    }

    bool IsGroundSurface(Vector3 point) { return Mathf.Abs(point.y - GroundHint(point).y) < 2.5f; }

    void BeginPatrol()
    {
        agent.speed = PatrolSpeed;
        agent.stoppingDistance = 0.7f;
        agent.isStopped = false;
        float minimumTravel = Mathf.Min(roamRadius * 0.4f, Mathf.Max(agent.radius * 3f, 8f));
        for (int i = 0; i < 20; i++)
        {
            Vector2 offset = Random.insideUnitCircle * roamRadius;
            if (!TryPath(home + new Vector3(offset.x, 0f, offset.y), Mathf.Max(12f, agent.radius * 2f + agent.baseOffset))) continue;
            if (path.corners.Length < 2 || HorizontalDistance(path.corners[path.corners.Length - 1], transform.position) < minimumTravel) continue;
            bool insideTerritory = true;
            foreach (var corner in path.corners)
                if (HorizontalDistance(corner, home) > roamRadius) { insideTerritory = false; break; }
            if (!insideTerritory) continue;
            agent.SetDestination(path.corners[path.corners.Length - 1]);
            state = BehaviourState.Patrol;
            progressPosition = transform.position;
            progressTime = Time.time;
            return;
        }
        BeginIdle();
    }

    void BeginIdle()
    {
        state = BehaviourState.Idle;
        agent.ResetPath();
        StopMoving();
        idleUntil = Time.time + Random.Range(Mathf.Max(0.1f, minIdleTime), Mathf.Max(minIdleTime, maxIdleTime));
        OnIdle();
    }

    protected virtual void OnIdle() { }

    bool Arrived()
    {
        return !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.5f);
    }

    void RecoverIfStuck()
    {
        if (agent.pathPending || !agent.hasPath) return;
        if ((transform.position - progressPosition).sqrMagnitude > 1f)
        {
            progressPosition = transform.position;
            progressTime = Time.time;
        }
        if (Time.time - progressTime < Mathf.Max(0.5f, stuckTimeout)) return;
        progressTime = Time.time;
        // Choose a reachable side step rather than continually retrying a blocked corner.
        Vector3 forward = agent.steeringTarget - transform.position;
        forward.y = 0;
        if (forward.sqrMagnitude < 0.01f) forward = transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, forward.normalized);
        for (int i = 0; i < 4; i++)
        {
            float sign = i % 2 == 0 ? 1f : -1f;
            Vector3 target = transform.position + side * sign * (agent.radius * 3f + 4f + i * 2f);
            if (HorizontalDistance(target, home) > roamRadius + (pursuing ? detectionRadius : 0f) || !TryPath(target, 3f)) continue;
            agent.SetDestination(path.corners[path.corners.Length - 1]);
            recoveryUntil = Time.time + 1.5f;
            return;
        }
        if (!pursuing) BeginPatrol();
        else { pursuing = false; BeginIdle(); }
    }

    void UpdateFacing()
    {
        if (attacking) return;
        Vector3 direction = agent.velocity;
        direction.y = 0;
        if (direction.sqrMagnitude > 0.1f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
    }

    bool BlockedAhead()
    {
        BlockingCollider = null;
        if (body == null || agent.desiredVelocity.sqrMagnitude < 0.01f) return false;
        Bounds bounds = body.bounds;
        float radius = Mathf.Max(0.1f, Mathf.Min(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 0.9f);
        float half = Mathf.Max(0f, bounds.extents.y - radius);
        Vector3 direction = agent.desiredVelocity.normalized;
        float lookAhead = agent.speed * Time.deltaTime + 0.3f;
        int count = Physics.CapsuleCastNonAlloc(bounds.center - Vector3.up * half, bounds.center + Vector3.up * half,
            radius, direction, obstacles, lookAhead, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = obstacles[i];
            if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<EnemyAI>() != null) continue;
            // Walkable ground and painted trees are handled by the collider-based,
            // size-specific NavMesh. Check other solid objects as a safety net.
            if (hit.collider is TerrainCollider || Vector3.Dot(hit.normal, Vector3.up) > 0.5f) continue;
            if (hit.collider.bounds.max.y <= GroundHint(transform.position).y + 2f) continue;
            if (player != null && hit.transform.IsChildOf(player)) continue;
            BlockingCollider = hit.collider;
            return true;
        }
        return count == obstacles.Length;
    }

    bool CanSeePlayer()
    {
        if (player == null) return false;
        Vector3 origin = body != null ? body.bounds.center : transform.position + Vector3.up;
        var controller = player.GetComponent<CharacterController>();
        Vector3 target = controller != null ? controller.bounds.center : player.position;
        Vector3 delta = target - origin;
        int count = Physics.RaycastNonAlloc(origin, delta.normalized, obstacles, delta.magnitude, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        if (count == obstacles.Length) return false;
        for (int i = 0; i < count; i++)
        {
            var hit = obstacles[i];
            if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(player)
                || hit.collider.GetComponentInParent<EnemyAI>() != null) continue;
            return false;
        }
        return true;
    }

    IEnumerator Attack()
    {
        attacking = true;
        nextAttack = Time.time + Mathf.Max(attackCooldown, attackWindup + 0.1f);
        if (animator != null) animator.SetTrigger("Attack");
        PlayActionSound(attackSound);
        yield return new WaitForSeconds(attackWindup);
        if (!isDead && playerHealth != null && !playerHealth.IsDead
            && PlayerDistance() <= attackRadius && CanSeePlayer())
            playerHealth.TakeDamage(attackDamage);
        attacking = false;
    }

    float PlayerDistance()
    {
        if (player == null) return float.PositiveInfinity;
        if (!Flying) return Vector3.Distance(transform.position, player.position);
        // Hover height must not prevent acquiring a player down a gentle bank.
        // Keep a vertical melee limit so a bee cannot bite through a cliff.
        float horizontal = HorizontalDistance(transform.position, player.position);
        return Mathf.Max(horizontal, Mathf.Abs(transform.position.y - player.position.y) * 0.65f);
    }

    void FacePlayer()
    {
        if (player == null) return;
        Vector3 direction = player.position - transform.position;
        direction.y = 0;
        if (direction.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
    }

    void SetWalking(bool value) { if (!Flying && animator != null) animator.SetBool("IsWalking", value); }
    void StopMoving() { agent.isStopped = true; SetWalking(false); }
    public void TakeDamage()
    {
        if (isDead) return;
        StopAllCoroutines();
        attacking = false;
        if (actionAudio != null) actionAudio.Stop();
        nextAttack = Mathf.Max(nextAttack, Time.time + 0.35f);
        if (animator != null) { animator.ResetTrigger("Attack"); animator.SetTrigger("Hit"); }
    }
    public void TriggerDeath()
    {
        if (isDead) return;
        isDead = true;
        state = BehaviourState.Dead;
        StopAllCoroutines();
        attacking = false;
        StopOwnedAudio(true);
        if (HasNavigation) { StopMoving(); agent.ResetPath(); }
        if (agent != null) agent.enabled = false;
        if (animator != null) { animator.ResetTrigger("Attack"); animator.SetBool("IsDead", true); }
    }
    protected virtual void OnDisable()
    {
        StopAllCoroutines();
        attacking = false;
        StopOwnedAudio(false);
        if (HasNavigation) { agent.isStopped = true; agent.nextPosition = transform.position; }
        SetWalking(false);
    }
    public void PlayActionSound(AudioClip clip)
    {
        if (isDead || clip == null || !isActiveAndEnabled) return;
        if (actionAudio == null)
        {
            var child = new GameObject("Enemy Action Audio"); child.transform.SetParent(transform, false);
            actionAudio = child.AddComponent<AudioSource>();
            actionAudio.playOnAwake = false; actionAudio.spatialBlend = 1;
            actionAudio.rolloffMode = AudioRolloffMode.Logarithmic;
            actionAudio.minDistance = 12; actionAudio.maxDistance = 160; actionAudio.volume = 0.65f;
        }
        // Long supplied recordings must not stack on every attack cooldown.
        if (actionAudio.isPlaying) return;
        actionAudio.clip = clip; actionAudio.Play();
    }
    void StopOwnedAudio(bool mute)
    {
        foreach (var source in GetComponentsInChildren<AudioSource>(true))
        {
            source.Stop();
            if (mute) { source.mute = true; source.playOnAwake = false; }
        }
    }
    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0;
        return Vector3.Distance(a, b);
    }
    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? home : transform.position;
        Gizmos.color = Color.green; Gizmos.DrawWireSphere(center, roamRadius);
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRadius);
        if (Application.isPlaying && HasNavigation && agent.hasPath)
        {
            Gizmos.color = Color.cyan;
            var corners = agent.path.corners;
            for (int i = 1; i < corners.Length; i++) Gizmos.DrawLine(corners[i - 1] + Vector3.up, corners[i] + Vector3.up);
        }
    }
}
