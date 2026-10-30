using UnityEngine;

public class BunnyController : MonoBehaviour
{
    public CharacterController controller;
    public Transform cameraTransform;

    private Animator animator;

    [Header("Movement")]
    public float walkSpeed = 10f;
    public float sprintSpeed = 20f;
    public float crouchSpeed = 5f;
    public float rotationSpeed = 10f;

    [Header("Jump")]
    public float jumpHeight = 2f;
    public float gravity = -20f;

    [Header("Crouch")]
    public float standingHeight = 2f;
    public float crouchingHeight = 1f;

    [Header("Camera Control")]
    public float cameraSensitivity = 3f;
    public float cameraDistance = 5f;
    public float minVerticalAngle = -10f;
    public float maxVerticalAngle = 85f; 
    private float cameraYaw = 0f;
    private float cameraPitch = 20f;

    private Vector3 velocity;
    private bool isDead = false;


    void Start()
    {
        animator = GetComponent<Animator>();

        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (cameraTransform != null)
        {
            Vector3 angles = cameraTransform.eulerAngles;
            cameraYaw = angles.y;
            cameraPitch = angles.x;
        }

        controller.height = standingHeight;
        controller.center = new Vector3(0, standingHeight / 2f, 0);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (isDead) return;

        HandleCameraControl();
        Move();
        HandleCombat();
    }

    void HandleCameraControl()
    {
        if (Input.GetMouseButton(1))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Horizontal Movement = Circular 360 rotation around player
            cameraYaw += Input.GetAxis("Mouse X") * cameraSensitivity;

            // Vertical Movement = Tilt up for Top-Down View or Down for Low Angle View
            cameraPitch -= Input.GetAxis("Mouse Y") * cameraSensitivity;
            cameraPitch = Mathf.Clamp(cameraPitch, minVerticalAngle, maxVerticalAngle);

            if (cameraTransform != null)
            {
                Quaternion rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
                Vector3 targetPosition = transform.position + Vector3.up * 1.5f;

                cameraTransform.position = targetPosition - (rotation * Vector3.forward * cameraDistance);
                cameraTransform.LookAt(targetPosition);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    void Move()
    {
        bool grounded = controller.isGrounded;

        if (animator != null)
        {
            animator.SetBool("IsJumping", !grounded);
        }

        if (grounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        bool isCrouching = Input.GetKey(KeyCode.LeftControl);
        float targetHeight = isCrouching ? crouchingHeight : standingHeight;

        controller.height = Mathf.Lerp(
            controller.height,
            targetHeight,
            10f * Time.deltaTime
        );

        controller.center = new Vector3(
            0,
            controller.height / 2f,
            0
        );

        if (animator != null)
        {
            animator.SetBool("IsCrouching", isCrouching);
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 moveDirection = Vector3.zero;

        if (cameraTransform != null)
        {
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0f;
            camRight.y = 0f;

            camForward.Normalize();
            camRight.Normalize();

            moveDirection =
                (camForward * vertical + camRight * horizontal).normalized;
        }
        else
        {
            moveDirection =
                (transform.forward * vertical +
                 transform.right * horizontal).normalized;
        }

        bool IsMoving = moveDirection.magnitude > 0.1f;
        bool IsSprinting =
            IsMoving &&
            Input.GetKey(KeyCode.LeftShift) &&
            !isCrouching;

        if (animator != null)
        {
            animator.SetBool("IsMoving", IsMoving);
            animator.SetBool("IsSprinting", IsSprinting);
        }

        if (IsMoving)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(moveDirection);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        float currentSpeed = walkSpeed;

        if (isCrouching)
        {
            currentSpeed = crouchSpeed;
        }
        else if (IsSprinting)
        {
            currentSpeed = sprintSpeed;
        }

        if (Input.GetButtonDown("Jump") && grounded && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(
                jumpHeight * -2f * gravity
            );
        }

        velocity.y += gravity * Time.deltaTime;

        Vector3 finalMove = moveDirection * currentSpeed;
        finalMove.y = velocity.y;

        controller.Move(finalMove * Time.deltaTime);
    }

    void HandleCombat()
    {
        if (animator == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            animator.SetTrigger("Punch");
        }
        if (Input.GetKeyDown(KeyCode.Q))
        {
            animator.SetTrigger("AttackWeapon");
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            animator.SetTrigger("Hit");
        }
        if (Input.GetKeyDown(KeyCode.V))
        {
            TriggerDeath();
        }
    }
    public void TriggerDeath()
    {
        isDead = true;

        if (animator != null)
        {
            animator.SetBool("IsDead", true);
        }
    }
}