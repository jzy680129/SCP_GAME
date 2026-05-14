using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterController))]
public sealed class ThirdPersonLocomotionController : MonoBehaviour
{
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int Turn180Hash = Animator.StringToHash("Turn180");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");
    private static readonly int LandingSoonHash = Animator.StringToHash("LandingSoon");
    private static readonly int GroundDistanceHash = Animator.StringToHash("GroundDistance");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    [Header("输入")]
    [SerializeField, InspectorName("读取键盘输入")]
    [Tooltip("开启后脚本直接读取 WASD、Shift、Space、Q。关闭后可由外部输入系统调用 SetMoveInput、SetRunning 等接口。")]
    private bool readKeyboardInput = true;

    [SerializeField, InspectorName("后退自动 180 转身")]
    [Tooltip("奔跑时从前进快速切到后退，会触发 180 度转身。适合原神式第三人称操作。")]
    private bool autoTurn180OnBackInput = true;

    [SerializeField, InspectorName("允许倒退走")]
    [Tooltip("开启后按 S 会倒退走；关闭后按 S 会朝相机后方转身移动。")]
    private bool allowBackpedal = false;

    [SerializeField, InspectorName("转身时锁定移动")]
    [Tooltip("180 度转身期间限制移动方向，避免转身动画和角色位移打架。")]
    private bool lockMovementDuringTurn = true;

    [Header("引用")]
    [SerializeField, InspectorName("相机参考")]
    [Tooltip("移动方向会相对这个 Transform 计算。通常填 PlayerCameraRig。")]
    private Transform cameraTransform;

    [SerializeField, InspectorName("禁用 Root Motion")]
    [Tooltip("开启后由脚本控制角色位移，动画只负责表现。")]
    private bool disableRootMotion = true;

    [Header("地面移动")]
    [SerializeField, InspectorName("行走速度")]
    [Tooltip("普通移动速度，单位约为米/秒。")]
    private float walkSpeed = 1.8f;

    [SerializeField, InspectorName("奔跑速度")]
    [Tooltip("按住奔跑键时的移动速度，单位约为米/秒。")]
    private float runSpeed = 4.8f;

    [SerializeField, InspectorName("加速度")]
    [Tooltip("角色从慢到快的速度变化率。越大越跟手。")]
    private float acceleration = 16f;

    [SerializeField, InspectorName("减速度")]
    [Tooltip("松开移动键后的停下速度。越大越不滑步。")]
    private float deceleration = 42f;

    [SerializeField, InspectorName("停止吸附速度")]
    [Tooltip("当前速度低于该值且无输入时，直接归零，减少脚底滑步。")]
    private float stopSnapSpeed = 0.08f;

    [SerializeField, InspectorName("最大转向速度")]
    [Tooltip("角色朝移动方向旋转的最大角速度，单位度/秒。")]
    private float rotationSpeed = 720f;

    [SerializeField, InspectorName("转向平滑时间")]
    [Tooltip("角色转向的平滑时间。越小越利落，越大越柔和。")]
    private float rotationSmoothTime = 0.08f;

    [SerializeField, InspectorName("倒退速度倍率")]
    [Tooltip("允许倒退走时，倒退速度相对行走速度的倍率。")]
    private float backpedalSpeedMultiplier = 0.6f;

    [SerializeField, InspectorName("倒退输入阈值")]
    [Tooltip("纵向输入低于该值时判定为倒退输入。")]
    private float backpedalInputThreshold = -0.2f;

    [SerializeField, InspectorName("基础重力")]
    [Tooltip("角色垂直方向的基础重力。绝对值越大，跳跃越重、下落越快。")]
    private float gravity = -26f;

    [SerializeField, InspectorName("贴地力度")]
    [Tooltip("角色在地面时给一个向下速度，帮助 CharacterController 稳定贴地。")]
    private float groundedStickForce = 2f;

    [Header("跳跃")]
    [SerializeField, InspectorName("允许跳跃")]
    [Tooltip("关闭后 Space 或外部 TriggerJump 不会触发跳跃。")]
    private bool allowJump = true;

    [SerializeField, InspectorName("跳跃高度")]
    [Tooltip("目标跳跃高度，单位约为米。")]
    private float jumpHeight = 1.15f;

    [SerializeField, InspectorName("跳跃输入缓冲")]
    [Tooltip("提前按下跳跃后，在这段时间内一旦满足起跳条件就会起跳。")]
    private float jumpBufferTime = 0.12f;

    [SerializeField, InspectorName("土狼时间")]
    [Tooltip("刚离开地面后仍允许起跳的时间，提升边缘跳跃手感。")]
    private float coyoteTime = 0.08f;

    [SerializeField, InspectorName("跳跃冷却")]
    [Tooltip("两次跳跃之间的最短间隔，防止连发误触。")]
    private float jumpCooldown = 0.18f;

    [SerializeField, InspectorName("空中控制倍率")]
    [Tooltip("空中水平加减速相对地面的倍率。越小越有重量，越大越灵活。")]
    private float airControlMultiplier = 0.4f;

    [SerializeField, InspectorName("下落重力倍率")]
    [Tooltip("角色开始下落后使用的重力倍率。越大，下落越快。")]
    private float fallGravityMultiplier = 1.35f;

    [SerializeField, InspectorName("移动跳下落重力倍率")]
    [Tooltip("移动或跑步跳跃下落时使用的重力倍率。低于普通下落倍率可以让跑跳落地不那么急。")]
    private float movingFallGravityMultiplier = 1.05f;

    [SerializeField, InspectorName("松开跳跃重力倍率")]
    [Tooltip("上升阶段松开跳跃键时使用的重力倍率，用于短跳。")]
    private float jumpCutGravityMultiplier = 1.75f;

    [SerializeField, InspectorName("最大下落速度")]
    [Tooltip("限制角色最大下落速度，避免高速穿透或落地过猛。")]
    private float maxFallSpeed = 18f;

    [Header("落地探测")]
    [SerializeField, InspectorName("地面层级")]
    [Tooltip("落地探测会检测这些 Layer。默认 Everything。")]
    private LayerMask groundMask = ~0;

    [SerializeField, InspectorName("落地探测距离")]
    [Tooltip("从角色脚底向下探测地面的最大距离。")]
    private float landingProbeDistance = 2f;

    [SerializeField, InspectorName("进入落地距离")]
    [Tooltip("静止下落时，离地低于该值会提前进入落地动画。")]
    private float landingEnterDistance = 0.38f;

    [SerializeField, InspectorName("落地提前时间")]
    [Tooltip("预计还有多少秒接触地面时，允许提前进入落地动画。")]
    private float landingLeadTime = 0.12f;

    [SerializeField, InspectorName("落地最小下落速度")]
    [Tooltip("下落速度超过该值时，落地预测才会触发。避免小坡、小台阶误判。")]
    private float landingMinFallSpeed = 2.6f;

    [SerializeField, InspectorName("探测球半径倍率")]
    [Tooltip("落地 SphereCast 半径相对 CharacterController 半径的倍率。")]
    private float landingProbeRadiusMultiplier = 0.75f;

    [SerializeField, InspectorName("最小地面法线 Y")]
    [Tooltip("命中的表面法线 Y 值低于该值时，不认为是可站立地面。")]
    private float minLandingNormalY = 0.55f;

    [SerializeField, InspectorName("触发器检测方式")]
    [Tooltip("落地探测是否检测 Trigger Collider。通常保持 Ignore。")]
    private QueryTriggerInteraction landingTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("动画接地判定")]
    [SerializeField, InspectorName("楼梯接地距离")]
    [Tooltip("脚下距离小于该值时，即使 CharacterController 短暂离地，Animator 仍视为接地。")]
    private float stairGroundedDistance = 0.5f;

    [SerializeField, InspectorName("下落动画延迟")]
    [Tooltip("短暂离地时延迟进入 Fall Idle，避免下楼梯和小台阶误触发。")]
    private float fallAnimationDelay = 0.16f;

    [SerializeField, InspectorName("进入下落最小距离")]
    [Tooltip("脚下距离超过该值后，才更倾向进入真正的空中下落状态。")]
    private float fallAnimationMinDistance = 0.65f;

    [Header("落地动画")]
    [SerializeField, InspectorName("移动输入阈值")]
    [Tooltip("移动输入超过该值时，落地会跳过原地落地动画，直接接走跑。")]
    private float landingMoveInputThreshold = 0.1f;

    [SerializeField, InspectorName("移动速度阈值")]
    [Tooltip("水平速度超过该值时，落地会跳过原地落地动画，直接接走跑。")]
    private float landingMoveSpeedThreshold = 0.25f;

    [SerializeField, InspectorName("落地冲击最小速度")]
    [Tooltip("下落速度超过该值才应用落地冲击削速。")]
    private float landingImpactMinSpeed = 5.5f;

    [SerializeField, InspectorName("落地水平速度保留")]
    [Tooltip("落地冲击时保留的水平速度比例。小于 1 会有轻微顿挫感。")]
    private float landingPlanarSpeedMultiplier = 0.92f;

    [Header("疾跑转身")]
    [SerializeField, InspectorName("180 转身时长")]
    [Tooltip("疾跑 180 度转身持续时间。")]
    private float turn180Duration = 0.55f;

    [SerializeField, InspectorName("180 转身冷却")]
    [Tooltip("两次 180 度转身之间的最短间隔。")]
    private float turn180Cooldown = 0.7f;

    [SerializeField, InspectorName("180 转身输入阈值")]
    [Tooltip("从前进切到后退时，输入超过该阈值才触发疾跑转身。")]
    private float turn180InputThreshold = 0.65f;

    [Header("动画参数")]
    [SerializeField, InspectorName("动画参数阻尼")]
    [Tooltip("MoveX、MoveY、Speed 写入 Animator 时的平滑时间。")]
    private float animatorDampTime = 0.08f;

    private Animator animator;
    private CharacterController characterController;
    private Vector2 moveInput;
    private Vector3 lastMoveDirection = Vector3.forward;
    private float currentPlanarSpeed;
    private float verticalVelocity;
    private bool isRunning;
    private bool wasForwardRunning;
    private float turnRemainingTime;
    private float turnAngularSpeed;
    private float nextTurnTime;
    private float rotationVelocity;
    private float jumpBufferTimer;
    private float coyoteTimer;
    private float nextJumpTime;
    private float groundDistance = float.PositiveInfinity;
    private float rawUngroundedTime;
    private bool jumpStartedThisFrame;
    private bool landingSoon;
    private bool animatorGrounded = true;
    private readonly RaycastHit[] landingProbeHits = new RaycastHit[8];

    public Vector2 MoveInput => moveInput;
    public bool IsRunning => isRunning;
    public bool IsTurning180 => turnRemainingTime > 0f;
    public bool IsGrounded => IsControllerGrounded();
    public bool LandingSoon => landingSoon;
    public float GroundDistance => groundDistance;
    public bool IsMovingForLanding => IsMovingEnoughForLandingBypass();
    public bool AnimatorGrounded => animatorGrounded;

    public void SetCameraTransform(Transform targetCamera)
    {
        cameraTransform = targetCamera;
    }

    public void SetMoveInput(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    public void SetRunning(bool running)
    {
        isRunning = running;
    }

    public void SetLocalInputEnabled(bool enabled)
    {
        readKeyboardInput = enabled;

        if (enabled)
        {
            return;
        }

        moveInput = Vector2.zero;
        isRunning = false;
        wasForwardRunning = false;
        jumpBufferTimer = 0f;
        currentPlanarSpeed = 0f;
    }

    public void TriggerTurn180()
    {
        EnsureReferences();

        if (Time.time < nextTurnTime)
        {
            return;
        }

        nextTurnTime = Time.time + turn180Cooldown;
        turnRemainingTime = Mathf.Max(0.05f, turn180Duration);
        turnAngularSpeed = 180f / turnRemainingTime;

        if (animator != null)
        {
            animator.ResetTrigger(Turn180Hash);
            animator.SetTrigger(Turn180Hash);
        }
    }

    public void TriggerJump()
    {
        if (!allowJump)
        {
            return;
        }

        jumpBufferTimer = Mathf.Max(0.01f, jumpBufferTime);
    }

    private void Awake()
    {
        EnsureReferences();

        if (disableRootMotion && animator != null)
        {
            animator.applyRootMotion = false;
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        jumpStartedThisFrame = false;
        UpdateGroundedTimer();

        if (readKeyboardInput)
        {
            ReadInput();
        }

        TryConsumeJump();
        DecayJumpBuffer();
        TryAutoTurn180();
        MoveCharacter();
        UpdateLandingProbe();
        UpdateAnimatorGroundedState();
        UpdateAnimator();
    }

    private void ReadInput()
    {
        moveInput = ReadMoveInput();
        isRunning = ReadRunInput();

        if (WasTurn180PressedThisFrame())
        {
            TriggerTurn180();
        }

        if (WasJumpPressedThisFrame())
        {
            TriggerJump();
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

    private bool WasTurn180PressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return keyboard.qKey.wasPressedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Q);
#else
        return false;
#endif
    }

    private bool WasJumpPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return keyboard.spaceKey.wasPressedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space);
#else
        return false;
#endif
    }

    private bool IsJumpHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return keyboard.spaceKey.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.Space);
#else
        return true;
#endif
    }

    private void UpdateGroundedTimer()
    {
        if (IsControllerGrounded())
        {
            coyoteTimer = Mathf.Max(0.01f, coyoteTime);
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.deltaTime);
        }
    }

    private void DecayJumpBuffer()
    {
        if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
        }
    }

    private void TryConsumeJump()
    {
        if (!allowJump
            || jumpBufferTimer <= 0f
            || coyoteTimer <= 0f
            || Time.time < nextJumpTime
            || IsTurning180)
        {
            return;
        }

        nextJumpTime = Time.time + Mathf.Max(0f, jumpCooldown);
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        jumpStartedThisFrame = true;

        verticalVelocity = Mathf.Sqrt(Mathf.Max(0.01f, jumpHeight) * -2f * gravity);

        if (animator == null)
        {
            return;
        }

        if (HasParameter(IsMovingHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(IsMovingHash, IsMovingEnoughForLandingBypass());
        }

        if (HasParameter(JumpHash, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(JumpHash);
            animator.SetTrigger(JumpHash);
        }
    }

    private void TryAutoTurn180()
    {
        if (!autoTurn180OnBackInput || !isRunning || !IsControllerGrounded())
        {
            wasForwardRunning = false;
            return;
        }

        bool forwardRunning = moveInput.y > turn180InputThreshold && moveInput.sqrMagnitude > 0.1f;
        bool requestedBackTurn = wasForwardRunning && moveInput.y < -turn180InputThreshold;

        if (requestedBackTurn)
        {
            TriggerTurn180();
            moveInput = new Vector2(moveInput.x, Mathf.Abs(moveInput.y));
            wasForwardRunning = false;
            return;
        }

        wasForwardRunning = forwardRunning;
    }

    private void MoveCharacter()
    {
        EnsureReferences();

        bool grounded = IsControllerGrounded();
        bool backpedaling = IsBackpedaling();
        Vector3 moveDirection = backpedaling ? GetBackpedalMoveDirection() : GetWorldMoveDirection();

        bool turning = IsTurning180;
        if (turning)
        {
            float delta = Mathf.Min(Time.deltaTime, turnRemainingTime);
            transform.Rotate(0f, turnAngularSpeed * delta, 0f, Space.Self);
            turnRemainingTime -= delta;

            if (lockMovementDuringTurn)
            {
                moveDirection = transform.forward;
            }
        }

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            lastMoveDirection = moveDirection;

            if (!turning && !backpedaling)
            {
                SmoothRotateToward(moveDirection);
            }
        }

        float targetSpeed = GetTargetPlanarSpeed(backpedaling);

        if (turning && lockMovementDuringTurn)
        {
            targetSpeed = Mathf.Min(targetSpeed, runSpeed * 0.35f);
        }

        float speedChangeRate = targetSpeed > currentPlanarSpeed ? acceleration : deceleration;
        if (!grounded)
        {
            speedChangeRate *= Mathf.Clamp01(airControlMultiplier);
        }

        currentPlanarSpeed = Mathf.MoveTowards(
            currentPlanarSpeed,
            targetSpeed,
            speedChangeRate * Time.deltaTime);

        if (targetSpeed <= 0f && currentPlanarSpeed <= stopSnapSpeed)
        {
            currentPlanarSpeed = 0f;
        }

        if (characterController != null && grounded && verticalVelocity < 0f && !jumpStartedThisFrame)
        {
            verticalVelocity = -groundedStickForce;
        }

        verticalVelocity = Mathf.Max(
            verticalVelocity + GetEffectiveGravity() * Time.deltaTime,
            -Mathf.Max(1f, maxFallSpeed));
        Vector3 planarVelocity = (moveDirection.sqrMagnitude > 0.001f ? moveDirection : lastMoveDirection) * currentPlanarSpeed;
        Vector3 velocity = planarVelocity + Vector3.up * verticalVelocity;

        if (characterController != null)
        {
            float impactVelocity = verticalVelocity;
            CollisionFlags flags = characterController.Move(velocity * Time.deltaTime);
            if ((flags & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            {
                ApplyLandingImpact(impactVelocity);
                verticalVelocity = -groundedStickForce;
            }
        }
        else
        {
            transform.position += velocity * Time.deltaTime;
        }
    }

    private float GetEffectiveGravity()
    {
        float multiplier = 1f;
        if (verticalVelocity < 0f)
        {
            float fallMultiplier = IsMovingEnoughForLandingBypass()
                ? movingFallGravityMultiplier
                : fallGravityMultiplier;
            multiplier = Mathf.Max(1f, fallMultiplier);
        }
        else if (verticalVelocity > 0f && !IsJumpHeld())
        {
            multiplier = Mathf.Max(1f, jumpCutGravityMultiplier);
        }

        return gravity * multiplier;
    }

    private void ApplyLandingImpact(float impactVelocity)
    {
        if (impactVelocity > -Mathf.Max(0f, landingImpactMinSpeed))
        {
            return;
        }

        currentPlanarSpeed *= Mathf.Clamp01(landingPlanarSpeedMultiplier);
    }

    private void UpdateLandingProbe()
    {
        EnsureReferences();

        groundDistance = landingProbeDistance + 1f;
        landingSoon = false;

        bool grounded = IsControllerGrounded();
        if (grounded)
        {
            groundDistance = 0f;
            return;
        }

        if (!TryGetGroundDistance(out float hitDistance))
        {
            return;
        }

        groundDistance = hitDistance;
        if (verticalVelocity > -landingMinFallSpeed)
        {
            return;
        }

        float timeToGround = hitDistance / Mathf.Max(0.01f, -verticalVelocity);
        landingSoon = hitDistance <= landingEnterDistance || timeToGround <= landingLeadTime;
    }

    private void UpdateAnimatorGroundedState()
    {
        bool rawGrounded = IsControllerGrounded();
        if (rawGrounded)
        {
            rawUngroundedTime = 0f;
            animatorGrounded = true;
            return;
        }

        rawUngroundedTime += Time.deltaTime;
        animatorGrounded = ShouldUseGroundedAnimation(
            false,
            rawUngroundedTime,
            groundDistance,
            verticalVelocity);
    }

    private bool ShouldUseGroundedAnimation(
        bool rawGrounded,
        float ungroundedTime,
        float sampledGroundDistance,
        float sampledVerticalVelocity)
    {
        if (rawGrounded)
        {
            return true;
        }

        bool fallingOrFlat = sampledVerticalVelocity <= 0.1f;
        bool closeToStep = sampledGroundDistance <= Mathf.Max(0f, stairGroundedDistance);
        bool shortGroundedGap = ungroundedTime <= Mathf.Max(0f, fallAnimationDelay);
        bool stillNearWalkableGround = sampledGroundDistance <= Mathf.Max(stairGroundedDistance, fallAnimationMinDistance);

        return fallingOrFlat && (closeToStep || shortGroundedGap || stillNearWalkableGround);
    }

    private bool TryGetGroundDistance(out float hitDistance)
    {
        hitDistance = landingProbeDistance + 1f;
        Vector3 origin;
        float radius;

        if (characterController != null)
        {
            Vector3 worldCenter = transform.TransformPoint(characterController.center);
            float halfHeight = Mathf.Max(characterController.height * 0.5f, characterController.radius);
            float bottomSphereOffset = halfHeight - characterController.radius;
            origin = worldCenter - transform.up * bottomSphereOffset + Vector3.up * characterController.skinWidth;
            radius = Mathf.Max(0.03f, characterController.radius * Mathf.Clamp01(landingProbeRadiusMultiplier));
        }
        else
        {
            origin = transform.position + Vector3.up * 0.25f;
            radius = 0.2f;
        }

        float castDistance = Mathf.Max(0.05f, landingProbeDistance + (characterController != null ? characterController.skinWidth : 0f));
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            radius,
            Vector3.down,
            landingProbeHits,
            castDistance,
            groundMask,
            landingTriggerInteraction);

        bool foundGround = false;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = landingProbeHits[i];
            if (hit.collider == null
                || hit.collider.transform.IsChildOf(transform)
                || hit.normal.y < minLandingNormalY)
            {
                continue;
            }

            float adjustedDistance = Mathf.Max(0f, hit.distance - (characterController != null ? characterController.skinWidth : 0f));
            if (adjustedDistance < nearestDistance)
            {
                nearestDistance = adjustedDistance;
                foundGround = true;
            }
        }

        if (!foundGround)
        {
            return false;
        }

        hitDistance = nearestDistance;
        return true;
    }

    private Vector3 GetWorldMoveDirection()
    {
        Vector3 localMove = new Vector3(moveInput.x, 0f, moveInput.y);
        if (localMove.sqrMagnitude < 0.001f)
        {
            return Vector3.zero;
        }

        Transform relativeTransform = cameraTransform != null ? cameraTransform : transform;
        Vector3 forward = relativeTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = relativeTransform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 worldMove = right * moveInput.x + forward * moveInput.y;
        return worldMove.sqrMagnitude > 0.001f ? worldMove.normalized : Vector3.zero;
    }

    private Vector3 GetBackpedalMoveDirection()
    {
        Vector3 backward = -transform.forward * Mathf.Abs(moveInput.y);
        Vector3 right = GetPlanarRight() * moveInput.x;
        Vector3 move = backward + right;
        return move.sqrMagnitude > 0.001f ? move.normalized : Vector3.zero;
    }

    private Vector3 GetPlanarRight()
    {
        Transform relativeTransform = cameraTransform != null ? cameraTransform : transform;
        Vector3 right = relativeTransform.right;
        right.y = 0f;
        return right.sqrMagnitude > 0.001f ? right.normalized : transform.right;
    }

    private float GetTargetPlanarSpeed(bool backpedaling)
    {
        if (moveInput.sqrMagnitude <= 0.001f)
        {
            return 0f;
        }

        if (backpedaling)
        {
            return walkSpeed * backpedalSpeedMultiplier * moveInput.magnitude;
        }

        return (isRunning ? runSpeed : walkSpeed) * moveInput.magnitude;
    }

    private bool IsBackpedaling()
    {
        return allowBackpedal
            && !IsTurning180
            && moveInput.y < backpedalInputThreshold;
    }

    private void SmoothRotateToward(Vector3 moveDirection)
    {
        float targetYaw = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
        float smoothTime = Mathf.Max(0.001f, rotationSmoothTime);
        float smoothedYaw = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            targetYaw,
            ref rotationVelocity,
            smoothTime,
            rotationSpeed);

        transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
    }

    private void UpdateAnimator()
    {
        EnsureReferences();
        if (animator == null)
        {
            return;
        }

        float speedBlend = moveInput.sqrMagnitude > 0.001f
            ? (isRunning ? 2f : 1f) * moveInput.magnitude
            : 0f;

        bool backpedaling = IsBackpedaling();
        Vector2 localBlend = backpedaling ? GetBackpedalAnimatorBlend() : GetLocalAnimatorBlend();
        animator.SetFloat(MoveXHash, localBlend.x, animatorDampTime, Time.deltaTime);
        animator.SetFloat(MoveYHash, localBlend.y, animatorDampTime, Time.deltaTime);
        animator.SetFloat(SpeedHash, speedBlend, animatorDampTime, Time.deltaTime);
        animator.SetBool(IsRunningHash, isRunning);

        if (HasParameter(GroundedHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(GroundedHash, animatorGrounded);
        }

        if (HasParameter(VerticalVelocityHash, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(VerticalVelocityHash, verticalVelocity);
        }

        if (HasParameter(LandingSoonHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(LandingSoonHash, landingSoon);
        }

        if (HasParameter(GroundDistanceHash, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(GroundDistanceHash, groundDistance);
        }

        if (HasParameter(IsMovingHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(IsMovingHash, IsMovingEnoughForLandingBypass());
        }
    }

    private Vector2 GetLocalAnimatorBlend()
    {
        Vector3 worldMove = GetWorldMoveDirection();
        if (worldMove.sqrMagnitude < 0.001f)
        {
            return Vector2.zero;
        }

        Vector3 localMove = transform.InverseTransformDirection(worldMove);
        return Vector2.ClampMagnitude(new Vector2(localMove.x, localMove.z), 1f);
    }

    private Vector2 GetBackpedalAnimatorBlend()
    {
        return Vector2.ClampMagnitude(new Vector2(moveInput.x, moveInput.y), 1f);
    }

    private bool HasParameter(int nameHash, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.parameters == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == nameHash && parameter.type == type)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureReferences()
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

    private bool IsControllerGrounded()
    {
        return characterController == null || characterController.isGrounded;
    }

    private bool IsMovingEnoughForLandingBypass()
    {
        float inputThreshold = Mathf.Max(0f, landingMoveInputThreshold);
        float speedThreshold = Mathf.Max(0f, landingMoveSpeedThreshold);
        return moveInput.sqrMagnitude > inputThreshold * inputThreshold
            || currentPlanarSpeed > speedThreshold;
    }
}
