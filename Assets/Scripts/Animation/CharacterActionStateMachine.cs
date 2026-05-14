using UnityEngine;
using UnityEngine.Serialization;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum CharacterLocomotionState
{
    Idle,
    Walk,
    Run,
    StrafeLeft,
    StrafeRight,
    Jump,
    TurnLeft,
    TurnRight,
    TurnLeft90,
    TurnRight90
}

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterController))]
public sealed class CharacterActionStateMachine : MonoBehaviour
{
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int TurnLeftHash = Animator.StringToHash("TurnLeft");
    private static readonly int TurnRightHash = Animator.StringToHash("TurnRight");
    private static readonly int TurnLeft90Hash = Animator.StringToHash("TurnLeft90");
    private static readonly int TurnRight90Hash = Animator.StringToHash("TurnRight90");

    [Header("Control")]
    [SerializeField] private bool readKeyboardInput = true;
    [FormerlySerializedAs("moveTransform")]
    [SerializeField] private bool moveCharacterController = true;
    [SerializeField] private bool disableRootMotion = true;
    [SerializeField] private bool useCameraRelativeMovement = true;
    [SerializeField] private bool useLocalLocomotionBlend = true;
    [SerializeField] private bool rotateTowardMoveDirection = true;
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.8f;
    [SerializeField] private float runSpeed = 4.2f;
    [SerializeField] private float strafeSpeed = 1.6f;
    [SerializeField] private float turnSpeed = 540f;
    [SerializeField] private float jumpHeight = 1.15f;
    [SerializeField] private float gravity = -18f;
    [SerializeField] private float groundedStickForce = 2f;
    [SerializeField] private float parameterDampTime = 0.08f;

    private Animator animator;
    private CharacterController characterController;
    private Vector2 moveInput;
    private bool isRunning;
    private float verticalVelocity;

    public CharacterLocomotionState CurrentState { get; private set; } = CharacterLocomotionState.Idle;

    public void SetMoveInput(Vector2 input)
    {
        EnsureRuntimeReferences();
        moveInput = Vector2.ClampMagnitude(input, 1f);
        RefreshLocomotionState();
        ApplyAnimatorParameters();
    }

    public void SetCameraRelativeMoveInput(Vector2 input)
    {
        SetMoveInput(input);
    }

    public void SetCameraTransform(Transform targetCamera)
    {
        cameraTransform = targetCamera;
    }

    public void SetUseLocalLocomotionBlend(bool enabled)
    {
        EnsureRuntimeReferences();
        useLocalLocomotionBlend = enabled;
        ApplyAnimatorParameters();
    }

    public void SetRunning(bool running)
    {
        EnsureRuntimeReferences();
        isRunning = running;
        RefreshLocomotionState();
        ApplyAnimatorParameters();
    }

    public void TriggerJump()
    {
        EnsureRuntimeReferences();
        CurrentState = CharacterLocomotionState.Jump;

        if (characterController == null || characterController.isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        if (animator != null)
        {
            animator.SetTrigger(JumpHash);
        }
    }

    public void ForceState(CharacterLocomotionState state)
    {
        switch (state)
        {
            case CharacterLocomotionState.Idle:
                SetRunning(false);
                SetMoveInput(Vector2.zero);
                break;
            case CharacterLocomotionState.Walk:
                SetRunning(false);
                SetMoveInput(Vector2.up);
                break;
            case CharacterLocomotionState.Run:
                SetRunning(true);
                SetMoveInput(Vector2.up);
                break;
            case CharacterLocomotionState.StrafeLeft:
                SetRunning(false);
                SetMoveInput(Vector2.left);
                break;
            case CharacterLocomotionState.StrafeRight:
                SetRunning(false);
                SetMoveInput(Vector2.right);
                break;
            case CharacterLocomotionState.Jump:
                TriggerJump();
                break;
            case CharacterLocomotionState.TurnLeft:
                TriggerTurn(TurnLeftHash, CharacterLocomotionState.TurnLeft);
                break;
            case CharacterLocomotionState.TurnRight:
                TriggerTurn(TurnRightHash, CharacterLocomotionState.TurnRight);
                break;
            case CharacterLocomotionState.TurnLeft90:
                TriggerTurn(TurnLeft90Hash, CharacterLocomotionState.TurnLeft90);
                break;
            case CharacterLocomotionState.TurnRight90:
                TriggerTurn(TurnRight90Hash, CharacterLocomotionState.TurnRight90);
                break;
        }
    }

    private void Awake()
    {
        EnsureRuntimeReferences();

        if (disableRootMotion && animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    private void Update()
    {
        if (readKeyboardInput)
        {
            ReadKeyboardControls();
        }

        ApplyAnimatorParameters();

        if (moveCharacterController)
        {
            MoveCharacterController();
        }
    }

    private void ReadKeyboardControls()
    {
        SetMoveInput(ReadMoveInput());
        SetRunning(ReadRunInput());

        if (WasPressedThisFrame(KeyBinding.Jump))
        {
            TriggerJump();
        }

        if (WasPressedThisFrame(KeyBinding.TurnLeft))
        {
            TriggerTurn(TurnLeftHash, CharacterLocomotionState.TurnLeft);
        }

        if (WasPressedThisFrame(KeyBinding.TurnRight))
        {
            TriggerTurn(TurnRightHash, CharacterLocomotionState.TurnRight);
        }

        if (WasPressedThisFrame(KeyBinding.TurnLeft90))
        {
            TriggerTurn(TurnLeft90Hash, CharacterLocomotionState.TurnLeft90);
        }

        if (WasPressedThisFrame(KeyBinding.TurnRight90))
        {
            TriggerTurn(TurnRight90Hash, CharacterLocomotionState.TurnRight90);
        }
    }

    private Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            Vector2 input = Vector2.zero;
            input.x += keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f;
            input.x -= keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f;
            input.y += keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f;
            input.y -= keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f;
            return Vector2.ClampMagnitude(input, 1f);
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#else
        return Vector2.zero;
#endif
    }

    private bool ReadRunInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
        return false;
#endif
    }

    private bool WasPressedThisFrame(KeyBinding binding)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return binding switch
            {
                KeyBinding.Jump => keyboard.spaceKey.wasPressedThisFrame,
                KeyBinding.TurnLeft => keyboard.qKey.wasPressedThisFrame,
                KeyBinding.TurnRight => keyboard.eKey.wasPressedThisFrame,
                KeyBinding.TurnLeft90 => keyboard.zKey.wasPressedThisFrame,
                KeyBinding.TurnRight90 => keyboard.cKey.wasPressedThisFrame,
                _ => false
            };
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return binding switch
        {
            KeyBinding.Jump => Input.GetKeyDown(KeyCode.Space),
            KeyBinding.TurnLeft => Input.GetKeyDown(KeyCode.Q),
            KeyBinding.TurnRight => Input.GetKeyDown(KeyCode.E),
            KeyBinding.TurnLeft90 => Input.GetKeyDown(KeyCode.Z),
            KeyBinding.TurnRight90 => Input.GetKeyDown(KeyCode.C),
            _ => false
        };
#else
        return false;
#endif
    }

    private void TriggerTurn(int triggerHash, CharacterLocomotionState state)
    {
        EnsureRuntimeReferences();
        CurrentState = state;
        if (animator != null)
        {
            animator.SetTrigger(triggerHash);
        }
    }

    private void RefreshLocomotionState()
    {
        if (moveInput.sqrMagnitude < 0.01f)
        {
            CurrentState = CharacterLocomotionState.Idle;
            return;
        }

        if (useCameraRelativeMovement)
        {
            CurrentState = isRunning
                ? CharacterLocomotionState.Run
                : CharacterLocomotionState.Walk;
            return;
        }

        if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
        {
            CurrentState = moveInput.x < 0f
                ? CharacterLocomotionState.StrafeLeft
                : CharacterLocomotionState.StrafeRight;
            return;
        }

        CurrentState = isRunning && moveInput.y > 0f
            ? CharacterLocomotionState.Run
            : CharacterLocomotionState.Walk;
    }

    private void ApplyAnimatorParameters()
    {
        EnsureRuntimeReferences();
        if (animator == null)
        {
            return;
        }

        Vector2 animationInput = GetAnimationInput();
        float targetSpeed = animationInput.magnitude;
        if (isRunning && moveInput.sqrMagnitude > 0.01f)
        {
            targetSpeed = 2f;
        }

        animator.SetFloat(MoveXHash, animationInput.x, parameterDampTime, Time.deltaTime);
        animator.SetFloat(MoveYHash, animationInput.y, parameterDampTime, Time.deltaTime);
        animator.SetFloat(SpeedHash, targetSpeed, parameterDampTime, Time.deltaTime);
        animator.SetBool(IsRunningHash, isRunning);
    }

    private Vector2 GetAnimationInput()
    {
        if (useCameraRelativeMovement && !useLocalLocomotionBlend)
        {
            float forwardBlend = moveInput.magnitude;
            if (isRunning && moveInput.sqrMagnitude > 0.01f)
            {
                forwardBlend *= 2f;
            }

            return new Vector2(0f, forwardBlend);
        }

        Vector2 animationInput = useCameraRelativeMovement
            ? GetLocalLocomotionInput()
            : moveInput;

        if (isRunning && animationInput.y > 0.35f)
        {
            animationInput.y = Mathf.Clamp(animationInput.y * 2f, 0f, 2f);
        }

        return animationInput;
    }

    private Vector2 GetLocalLocomotionInput()
    {
        Vector3 worldMove = GetWorldMoveDirection();
        if (worldMove.sqrMagnitude < 0.01f)
        {
            return Vector2.zero;
        }

        Vector3 localMove = transform.InverseTransformDirection(worldMove);
        Vector2 localInput = new Vector2(localMove.x, localMove.z) * moveInput.magnitude;

        if (localInput.y < -0.35f && Mathf.Abs(localInput.x) < 0.35f)
        {
            return new Vector2(0f, moveInput.magnitude);
        }

        if (localInput.y < 0f)
        {
            localInput.y = 0f;
        }

        localInput.x = Mathf.Clamp(localInput.x, -1f, 1f);
        localInput.y = Mathf.Clamp(localInput.y, 0f, 1f);
        return localInput;
    }

    private void MoveCharacterController()
    {
        EnsureRuntimeReferences();

        Vector3 moveDirection = GetWorldMoveDirection();
        Vector3 horizontalVelocity = Vector3.zero;
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            float speed = GetCurrentMoveSpeed();
            horizontalVelocity = moveDirection * speed;

            if (rotateTowardMoveDirection)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    turnSpeed * Time.deltaTime);
            }
        }

        float turnInput = ReadTurnAxis();
        if (Mathf.Abs(turnInput) > 0.01f)
        {
            transform.Rotate(0f, turnInput * turnSpeed * Time.deltaTime, 0f, Space.Self);
        }

        if (characterController == null)
        {
            transform.position += horizontalVelocity * Time.deltaTime;
            return;
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -groundedStickForce;
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        CollisionFlags flags = characterController.Move(velocity * Time.deltaTime);

        if ((flags & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
        {
            verticalVelocity = -groundedStickForce;
        }
    }

    private Vector3 GetWorldMoveDirection()
    {
        Vector3 localMove = new Vector3(moveInput.x, 0f, moveInput.y);
        if (localMove.sqrMagnitude < 0.01f)
        {
            return Vector3.zero;
        }

        if (!useCameraRelativeMovement || cameraTransform == null)
        {
            return transform.TransformDirection(localMove).normalized;
        }

        Vector3 cameraForward = cameraTransform.forward;
        cameraForward.y = 0f;
        cameraForward.Normalize();

        Vector3 cameraRight = cameraTransform.right;
        cameraRight.y = 0f;
        cameraRight.Normalize();

        Vector3 worldMove = cameraRight * moveInput.x + cameraForward * moveInput.y;
        return worldMove.sqrMagnitude > 0.01f ? worldMove.normalized : Vector3.zero;
    }

    private float GetCurrentMoveSpeed()
    {
        if (useCameraRelativeMovement)
        {
            return isRunning && moveInput.sqrMagnitude > 0.01f ? runSpeed : walkSpeed;
        }

        return Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y)
            ? strafeSpeed
            : (isRunning && moveInput.y > 0f ? runSpeed : walkSpeed);
    }

    private float ReadTurnAxis()
    {
        if (!readKeyboardInput)
        {
            return 0f;
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            float input = 0f;
            input -= keyboard.qKey.isPressed ? 1f : 0f;
            input += keyboard.eKey.isPressed ? 1f : 0f;
            return input;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        float legacyInput = 0f;
        legacyInput -= Input.GetKey(KeyCode.Q) ? 1f : 0f;
        legacyInput += Input.GetKey(KeyCode.E) ? 1f : 0f;
        return legacyInput;
#else
        return 0f;
#endif
    }

    private void EnsureRuntimeReferences()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
    }

    private enum KeyBinding
    {
        Jump,
        TurnLeft,
        TurnRight,
        TurnLeft90,
        TurnRight90
    }
}
