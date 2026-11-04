using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(BunnyAttack))]
public class BunnyController : MonoBehaviour
{
    public CharacterController controller;
    public Transform cameraTransform;
    [Header("Movement (world units)")]
    public float walkSpeed = 14f, sprintSpeed = 26f, crouchSpeed = 7f;
    public float rotationSpeed = 12f, acceleration = 60f;
    [Header("Jump")]
    public float jumpHeight = 8f, gravity = -60f;
    public float coyoteTime = 0.12f, jumpBuffer = 0.12f;
    [Range(45f, 85f)] public float steepJumpLimit = 75f;
    [Header("Grounding (world units)")]
    public LayerMask groundLayers = ~0;
    public float groundProbeDistance = 0.75f, groundSnapDistance = 1.2f;
    public float groundStickSpeed = 8f, airborneDelay = 0.08f;
    public bool IsGrounded { get; private set; }
    [Header("Crouch (local units)")]
    public float standingHeight = 2.69f, crouchingHeight = 1.5f;
    public LayerMask obstructionLayers = ~0;
    public bool IsDead { get; private set; }
    private Animator animator;
    private BunnyAttack attack;
    private Vector3 planarVelocity, standingCenter;
    private float verticalSpeed;
    private float lastGrounded = float.NegativeInfinity, lastJump = float.NegativeInfinity;
    private bool crouched;
    private bool jumpInProgress;
    private Vector3 moveSupportNormal;
    private bool steepSupport;
    private readonly Collider[] clearance = new Collider[32];
    private readonly RaycastHit[] groundHits = new RaycastHit[32];

    void Awake()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        attack = GetComponent<BunnyAttack>();
        standingCenter = controller.center;
    }

    void Start()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (IsDead || Time.timeScale == 0f) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }
        bool acceptInput = Cursor.lockState == CursorLockMode.Locked;
        Move(acceptInput ? new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")) : Vector2.zero,
            acceptInput && Input.GetKey(KeyCode.LeftShift), acceptInput && Input.GetKey(KeyCode.LeftControl),
            acceptInput && Input.GetButtonDown("Jump"));
        if (acceptInput && Input.GetMouseButtonDown(0)) attack.TryAttack(false);
        if (acceptInput && Input.GetKeyDown(KeyCode.Q)) attack.TryAttack(true);
    }

    void Move(Vector2 movementInput, bool sprintRequested, bool crouchRequested, bool jumpRequested)
    {
        Vector3 groundNormal = moveSupportNormal;
        bool groundedBeforeMove = verticalSpeed <= 0f
            && (ProbeGround(groundProbeDistance, out _, out groundNormal) || UseMoveSupport(ref groundNormal));
        if (groundedBeforeMove)
        {
            lastGrounded = Time.time;
            verticalSpeed = -groundStickSpeed;
            jumpInProgress = false;
        }
        if (jumpRequested) lastJump = Time.time;
        bool wantsCrouch = crouchRequested;
        crouched = wantsCrouch || (crouched && !CanStand());
        float height = Mathf.MoveTowards(controller.height, crouched ? crouchingHeight : standingHeight, 5f * Time.deltaTime);
        if (Mathf.Abs(controller.height - height) > 0.0001f)
        {
            controller.height = height;
            // Avoid rebuilding the controller on every grounded frame.
            controller.center = standingCenter + Vector3.up * ((height - standingHeight) * 0.5f);
        }
        Vector2 input = Vector2.ClampMagnitude(movementInput, 1f);
        float yaw = cameraTransform != null ? cameraTransform.eulerAngles.y : transform.eulerAngles.y;
        Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
        bool sprinting = !crouched && sprintRequested && input.sqrMagnitude > 0f;
        float speed = crouched ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed;
        planarVelocity = Vector3.MoveTowards(planarVelocity, direction * speed, acceleration * Time.deltaTime);
        if (direction.sqrMagnitude > 0.001f && !attack.IsAttacking)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
        bool jumpFromSteepSlope = !groundedBeforeMove && verticalSpeed <= 0f && steepSupport;
        bool launched = !crouched && (jumpFromSteepSlope || Time.time - lastGrounded <= coyoteTime)
            && Time.time - lastJump <= jumpBuffer;
        if (launched)
        {
            verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastJump = lastGrounded = float.NegativeInfinity;
            jumpInProgress = true;
        }
        verticalSpeed += gravity * Time.deltaTime;
        Vector3 movement = planarVelocity + Vector3.up * verticalSpeed;
        if (groundedBeforeMove && !jumpInProgress)
        {
            // Follow the surface instead of pushing horizontal input down into an incline.
            Vector3 tangent = Vector3.ProjectOnPlane(planarVelocity, groundNormal);
            movement = tangent.normalized * planarVelocity.magnitude - groundNormal * groundStickSpeed;
        }
        moveSupportNormal = Vector3.zero;
        steepSupport = false;
        // Lift clear of steep contact before feeding uphill input back to the controller.
        if (launched && jumpFromSteepSlope) movement = Vector3.up * verticalSpeed;
        CollisionFlags flags = controller.Move(movement * Time.deltaTime);
        if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
        if (groundedBeforeMove && !jumpInProgress && verticalSpeed <= 0f && moveSupportNormal == Vector3.zero
            && ProbeGround(groundSnapDistance, out float gap, out _) && gap > 0.01f)
            controller.Move(Vector3.down * gap);
        IsGrounded = verticalSpeed <= 0f
            && (moveSupportNormal != Vector3.zero || ProbeGround(groundProbeDistance, out _, out _));
        if (IsGrounded)
        {
            lastGrounded = Time.time;
            jumpInProgress = false;
            verticalSpeed = -groundStickSpeed;
        }
        if (animator == null) return;
        animator.SetBool("IsMoving", planarVelocity.sqrMagnitude > 0.1f);
        animator.SetBool("IsSprinting", sprinting);
        animator.SetBool("IsCrouching", crouched);
        animator.SetBool("IsJumping", jumpInProgress || (!IsGrounded && Time.time - lastGrounded > airborneDelay));
    }

    bool UseMoveSupport(ref Vector3 normal)
    {
        normal = moveSupportNormal;
        return normal != Vector3.zero;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if ((groundLayers.value & (1 << hit.gameObject.layer)) == 0
            || hit.point.y >= controller.bounds.center.y
            || hit.collider.GetComponentInParent<EnemyHealth>() != null)
            return;

        if (hit.normal.y >= Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad))
            moveSupportNormal = hit.normal;
        else if (verticalSpeed <= 0f && hit.normal.y >= Mathf.Cos(steepJumpLimit * Mathf.Deg2Rad))
            steepSupport = true;
    }

    bool ProbeGround(float distance, out float gap, out Vector3 normal)
    {
        Vector3 scale = transform.lossyScale;
        float fullRadius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float halfHeight = Mathf.Max(fullRadius, controller.height * Mathf.Abs(scale.y) * 0.5f);
        float radius = fullRadius * 0.9f;
        const float lift = 0.2f;
        Vector3 feet = transform.TransformPoint(controller.center) - Vector3.up * halfHeight;
        float offset = fullRadius - radius + lift;
        Vector3 origin = feet + Vector3.up * (fullRadius + lift);
        int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, groundHits,
            offset + Mathf.Max(0f, distance), groundLayers, QueryTriggerInteraction.Ignore);
        gap = float.PositiveInfinity;
        normal = Vector3.up;
        if (count == groundHits.Length) return false;
        float minimumUp = Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad);
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<EnemyHealth>() != null) continue;
            // Initial overlaps have no reliable support normal (a wall can report up).
            if (hit.distance <= 0.0001f) continue;
            if (Vector3.Dot(hit.normal, Vector3.up) < minimumUp) continue;
            float hitGap = Mathf.Max(0f, hit.distance - offset);
            if (hitGap >= gap) continue;
            gap = hitGap;
            normal = hit.normal;
        }
        return gap <= distance;
    }

    bool CanStand()
    {
        float scale = Mathf.Abs(transform.lossyScale.y);
        float radius = controller.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)) * 0.95f;
        Vector3 center = transform.TransformPoint(standingCenter);
        Vector3 top = center + Vector3.up * Mathf.Max(0f, standingHeight * scale * 0.5f - radius);
        Vector3 bottom = top - Vector3.up * Mathf.Max(0f, (standingHeight - crouchingHeight) * scale - radius);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, clearance, obstructionLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!clearance[i].transform.IsChildOf(transform)) return false;
        return count < clearance.Length;
    }

    public void TriggerDeath()
    {
        if (IsDead) return;
        IsDead = true;
        planarVelocity = Vector3.zero;
        attack.CancelAttack();
        if (animator == null) return;
        animator.ResetTrigger("Punch");
        animator.ResetTrigger("AttackWeapon");
        animator.ResetTrigger("Hit");
        animator.SetBool("IsMoving", false);
        animator.SetBool("IsSprinting", false);
        animator.SetBool("IsJumping", false);
        animator.SetBool("IsDead", true);
    }
}
